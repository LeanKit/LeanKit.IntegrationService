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
			// TODO: add tasks as a card, add any child tasks to a taskboard on the card?
			// Flag2 = Exclude from LeanKit
			var tasks = (from Task task in mpx.AllTasks.ToIEnumerable() 
								where (!task.GetFlag(2) && (task.Start != null && task.Start.ToDateTime() < futureDate)
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
			var laneId = project.LanesFromState("Ready").First();
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

			// Flag3 = IsBlocked, Text3 = Blocked Reason
			if (task.GetFlag(3))
			{
				card.IsBlocked = true;
				card.BlockReason = task.GetText(3) ?? "Task is blocked in Microsoft Project.";
			}

			// Text2 = Class of Service
			if (!string.IsNullOrEmpty(task.GetText(2)))
			{
				var board = LeanKit.GetBoard(boardId);
				if (board != null && board.ClassOfServiceEnabled)
				{
					var classOfService = board.ClassesOfService.FirstOrDefault(x => x.Title.ToLowerInvariant() == task.GetText(2).ToLowerInvariant());
					if (classOfService != null)
					{
						card.ClassOfServiceId = classOfService.Id;
					}
				}
			}

			if (task.GetSize() > 0)
			{
				card.Size = task.GetSize();
			}

			if (!string.IsNullOrEmpty(task.Hyperlink)) 
			{
				card.ExternalSystemUrl = task.Hyperlink;
			}

			// Text4 = tags
			if (!string.IsNullOrEmpty(task.GetText(4)))
			{
				card.Tags = task.getText(4);
			}

			if (task.ResourceAssignments != null)
			{
				var assignedUserIds = new List<long>();

				var emails = task.ResourceAssignments.ToIEnumerable<ResourceAssignment>()
				                       .Where(x => x != null && 
												   x.Resource != null && 
												   !string.IsNullOrEmpty(x.Resource.EmailAddress))
									   .Select(x => x.Resource.EmailAddress)
									   .ToList();

				foreach (var email in emails)
				{
					var assignedUserId = CalculateAssignedUserId(boardId, email);
					if (assignedUserId > 0)
					{
						if (!assignedUserIds.Contains(assignedUserId))
						{
							assignedUserIds.Add(assignedUserId);
						}
					}
				}

				if (assignedUserIds.Any())
					card.AssignedUserIds = assignedUserIds.ToArray();
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

			// Text4 = tags
			if (!string.IsNullOrEmpty(task.GetText(4))) 
			{
				if (card.Tags != task.GetText(4))
				{
					card.Tags = task.GetText(4);
					saveCard = true;
				}
			}
			else if (!string.IsNullOrEmpty(card.Tags))
			{
				card.Tags = "";
				saveCard = true;
			}

			if ((card.Tags == null || !card.Tags.Contains(ServiceName)) && boardMapping.TagCardsWithTargetSystemName) 
			{
				if (string.IsNullOrEmpty(card.Tags))
					card.Tags = ServiceName;
				else
					card.Tags += "," + ServiceName;
				saveCard = true;
			}

			// Flag3 = IsBlocked, Text3 = Blocked Reason
			var isBlocked = task.GetFlag(3);
			if (card.IsBlocked != isBlocked)
			{
				card.IsBlocked = isBlocked;
				card.BlockReason = task.GetText(3) ?? "Task is blocked/unblocked in Microsoft Project.";
				saveCard = true;
			}			

			// Text2 = Class of Service
			if (!string.IsNullOrEmpty(task.GetText(2))) 
			{
				var board = LeanKit.GetBoard(boardId);
				if (board != null && board.ClassOfServiceEnabled) 
				{
					var classOfService = board.ClassesOfService.FirstOrDefault(x => x.Title.ToLowerInvariant() == task.GetText(2).ToLowerInvariant());
					if (classOfService != null && card.ClassOfServiceId != classOfService.Id) 
					{
						card.ClassOfServiceId = classOfService.Id;
						saveCard = true;
					}
				}
			}
			else if (card.ClassOfServiceId.HasValue)
			{
				card.ClassOfServiceId = null;
				saveCard = true;
			}

			var assignedUserIds = new List<long>();
			if (task.ResourceAssignments != null) 
			{

				var emails = task.ResourceAssignments.ToIEnumerable<ResourceAssignment>()
									   .Where(x => x != null &&
												   x.Resource != null &&
												   !string.IsNullOrEmpty(x.Resource.EmailAddress))
									   .Select(x => x.Resource.EmailAddress)
									   .ToList();

				foreach (var email in emails) {
					var assignedUserId = CalculateAssignedUserId(boardId, email);
					if (assignedUserId > 0) 
					{
						if (!assignedUserIds.Contains(assignedUserId)) 
						{
							assignedUserIds.Add(assignedUserId);
						}
					}
				}
			}
			if (assignedUserIds.Any())
			{
				if (card.AssignedUserIds != assignedUserIds.ToArray())
				{
					card.AssignedUserIds = assignedUserIds.ToArray();
					saveCard = true;
				}
			}
			else if (card.AssignedUserIds.Any())
			{
				card.AssignedUserIds = new long[0];
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

		private long CalculateAssignedUserId(long boardId, string emailAddress)
		{
			long userId = 0;

			if (!string.IsNullOrEmpty(emailAddress))
			{
				var lkUser = LeanKit.GetBoard(boardId).BoardUsers.FirstOrDefault(x => x != null &&
											(((!String.IsNullOrEmpty(x.EmailAddress)) && x.EmailAddress.ToLowerInvariant() == emailAddress.ToLowerInvariant())));
				if (lkUser != null)
					userId = lkUser.Id;				
			}

			return userId;
		}


	}
}
