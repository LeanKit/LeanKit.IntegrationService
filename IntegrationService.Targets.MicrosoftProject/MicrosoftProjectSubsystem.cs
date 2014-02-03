using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IntegrationService.Util;
using LeanKit.API.Client.Library;
using LeanKit.API.Client.Library.TransferObjects;
using net.sf.mpxj;
using net.sf.mpxj.ExtensionMethods;
using net.sf.mpxj.reader;
using Task = net.sf.mpxj.Task;

namespace IntegrationService.Targets.MicrosoftProject 
{
	public class MicrosoftProject : TargetBase 
	{
	    private const string ServiceName = "MicrosoftProject";

	    public MicrosoftProject(IBoardSubscriptionManager subscriptions) : base(subscriptions) { }

		public MicrosoftProject(IBoardSubscriptionManager subscriptions, 
							IConfigurationProvider<Configuration> configurationProvider, 
							ILocalStorage<AppSettings> localStorage, 
							ILeanKitClientFactory leanKitClientFactory) 
			: base(subscriptions, configurationProvider, localStorage, leanKitClientFactory) { }


		public override void Init() 
		{
			Log.Debug("Initializing Microsoft Project integration...");
		}

		protected override void Synchronize(BoardMapping boardMapping) 
		{
			Log.Debug("Polling Microsoft Project for Tasks");

			ProjectReader reader = ProjectReaderUtility.getProjectReader(Configuration.Target.Host);
			ProjectFile mpx = reader.read(Configuration.Target.Host);

			var futureDate = DateTime.Now.AddDays(7);

			// for now we'll only get child tasks. 
			// TODO: add tasks as a card, add any child tasks to a taskboard on the card
			var tasks = (from Task task in mpx.AllTasks.ToIEnumerable() 
								where ((task.Start != null && task.Start.ToDateTime() < futureDate)
										|| (task.BaselineStart != null && task.BaselineStart.ToDateTime() < futureDate)
										|| (task.EarlyStart != null && task.EarlyStart.ToDateTime() < futureDate))
									&& (task.Summary || task.Milestone) 
									&& (task.ChildTasks == null || task.ChildTasks.isEmpty())
								select task).ToList();

			if (!tasks.Any())
			{
				Log.Info("No tasks start within target date range.");
				return;
			}

			Log.Info("\nQueried [{0}] at {1} for tasks starting before {2}", mpx.ProjectHeader.Name, QueryDate, futureDate);

			foreach (var task in tasks) 
			{
				if (task.UniqueID.ToNullableInt() > 0) 
				{
					Log.Info("Task [{0}]: {1}, {2}, {3}", task.UniqueID.ToString(), task.Name, "", task.ResourceGroup);

					//does this task have a corresponding card?
					var card = LeanKit.GetCardByExternalId(boardMapping.Identity.LeanKit, task.UniqueID.ToString());

					if (card == null || card.ExternalSystemName != ServiceName) 
					{
						Log.Debug("Create new card for Task [{0}]", task.UniqueID.ToString());
						CreateCardFromTask(boardMapping, task);
					} 
					else 
					{
						Log.Debug("Previously created a card for Task [{0}]", task.UniqueID.ToString());
							if (boardMapping.UpdateCards)
								TaskUpdated(task, card, boardMapping);
							else
								Log.Info("Skipped card update because 'UpdateCards' is disabled.");
					}
				}
			}
			Log.Info("{0} item(s) queried.\n", tasks.Count);
		}

		private void CreateCardFromTask(BoardMapping project, Task task) 
		{
			if (task == null) return;

			var boardId = project.Identity.LeanKit;

			var mappedCardType = task.LeanKitCardType(project);
			var laneId = project.LanesFromState("").First();
			var card = new Card
			{
				Active = true,
				Title = task.Name,
				Description = task.Notes,
				Priority = task.LeanKitPriority(),
				TypeId = mappedCardType.Id,
				TypeName = mappedCardType.Name,
				LaneId = laneId,
				ExternalCardID = task.UniqueID.ToString(),
				ExternalSystemName = ServiceName				
			};

			string dateFormat = "MM/dd/yyyy";
			if (CurrentUser != null) 
			{
				dateFormat = CurrentUser.DateFormat ?? "MM/dd/yyyy";
			}

			if (task.GetDueDate().HasValue) 
			{
				card.DueDate = task.GetDueDate().Value.ToString(dateFormat);
			}

			if (task.GetStartDate().HasValue)
			{
				card.StartDate = task.GetStartDate().Value.ToString(dateFormat);
			}

			if (task.GetSize() > 0)
			{
				card.Size = task.GetSize();
			}

			if (!string.IsNullOrEmpty(task.Hyperlink)) 
			{
				card.ExternalSystemUrl = task.Hyperlink;
			}

			Log.Info("Creating a card of type [{0}] for Task [{1}] on Board [{2}] on Lane [{3}]", mappedCardType.Name, task.UniqueID.toString(), boardId, laneId);

			CardAddResult cardAddResult = null;

			int tries = 0;
			bool success = false;
			while (tries < 10 && !success) 
			{
				if (tries > 0) 
				{
					Log.Error(string.Format("Attempting to create card for Task [{0}] attempt number [{1}]", task.UniqueID.toString(),
											 tries));
					// wait 5 seconds before trying again
					Thread.Sleep(new TimeSpan(0, 0, 5));
				}

				try {
					cardAddResult = LeanKit.AddCard(boardId, card, "New Card imported from Microsoft Project");
					success = true;
				} catch (Exception ex) {
					Log.Error(string.Format("An error occurred: {0} - {1} - {2}", ex.GetType(), ex.Message, ex.StackTrace));
				}
				tries++;
			}
			card.Id = cardAddResult.CardId;

			Log.Info("Created a card [{0}] of type [{1}] for Task [{2}] on Board [{3}] on Lane [{4}]", card.Id, mappedCardType.Name, task.UniqueID.toString(), boardId, laneId);
		}

		private void TaskUpdated(Task task, Card card, BoardMapping boardMapping) 
		{
			Log.Info("Task [{0}] updated, comparing to corresponding card...", task.UniqueID.toString());

			long boardId = boardMapping.Identity.LeanKit;

			// sync and save those items that are different (of title, description, priority)
			bool saveCard = false;
			if (task.Name != card.Title) 
			{
				card.Title = task.Name;
				saveCard = true;
			}

			if (task.Notes != card.Description) 
			{
				card.Description = task.Notes;
				saveCard = true;
			}

			var priority = task.LeanKitPriority();
			if (priority != card.Priority) 
			{
				card.Priority = priority;
				saveCard = true;
			}

			string dateFormat = "MM/dd/yyyy";
			if (CurrentUser != null) {
				dateFormat = CurrentUser.DateFormat ?? "MM/dd/yyyy";
			}

			if (task.GetDueDate().HasValue) 
			{
				var dueDateString = task.GetDueDate().Value.ToString(dateFormat);
				if (card.DueDate != dueDateString) 
				{
					card.DueDate = dueDateString;
					saveCard = true;
				}
			} 
			else if (!string.IsNullOrEmpty(card.DueDate)) 
			{
				card.DueDate = "";
				saveCard = true;
			}

			if (task.GetStartDate().HasValue) 
			{
				var startDateString = task.GetStartDate().Value.ToString(dateFormat);
				if (card.StartDate != startDateString) 
				{
					card.StartDate = startDateString;
					saveCard = true;
				}
			} 
			else if (!string.IsNullOrEmpty(card.StartDate)) 
			{
				card.StartDate = "";
				saveCard = true;
			}

			if (saveCard) {
				Log.Info("Updating card [{0}]", card.Id);
				LeanKit.UpdateCard(boardId, card);
			}
		}

		protected override void UpdateStateOfExternalItem(LeanKit.API.Client.Library.TransferObjects.Card card, List<string> states, BoardMapping boardMapping) 
		{
			Log.Debug(String.Format("TODO: Update state of Task from Card [{0}]", card.Id));
		}

		protected override void CardUpdated(LeanKit.API.Client.Library.TransferObjects.Card card, List<string> updatedItems, BoardMapping boardMapping) 
		{
			Log.Debug(String.Format("TODO: Update a Task from Card [{0}]", card.Id));
		}

		protected override void CreateNewItem(LeanKit.API.Client.Library.TransferObjects.Card card, BoardMapping boardMapping) 
		{
			Log.Debug(String.Format("TODO: Create a Task from Card [{0}]", card.Id));
		}


	}
}
