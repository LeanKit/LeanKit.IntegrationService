using System;
using System.Collections.Generic;
using System.IO;
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

			ProjectReader reader = ProjectReaderUtility.getProjectReader(Path.Combine(Configuration.Target.Host, boardMapping.Identity.Target));
			ProjectFile mpx = reader.read(Path.Combine(Configuration.Target.Host, boardMapping.Identity.Target));

			var futureDate = DateTime.Now.AddDays(boardMapping.QueryDaysOut);

			var importFields = GetImportFields(boardMapping);

			var startDates = importFields.GetTargetFieldsFor(LeanKitField.StartDate);

			// for now we'll only get child tasks. 
			// TODO: add tasks as a card, add any child tasks to a taskboard on the card?
			var tasks = (from Task task in mpx.AllTasks.ToIEnumerable() 
								where ((1 == 1) 
									&& FilterTasks(task, boardMapping.Filters)
									&& ((startDates.Contains("Start") && task.Start != null && task.Start.ToDateTime() < futureDate)
										|| (startDates.Contains("BaselineStart") && task.BaselineStart != null && task.BaselineStart.ToDateTime() < futureDate)
										|| (startDates.Contains("EarlyStart") && task.EarlyStart != null && task.EarlyStart.ToDateTime() < futureDate)))
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
				if (task.UniqueID.intValue() > 0) 
				{
					Log.Info("Task [{0}]: {1}, {2}, {3}", task.UniqueID.ToString(), task.Name, "", task.ResourceGroup);

					//does this task have a corresponding card?
					var card = LeanKit.GetCardByExternalId(boardMapping.Identity.LeanKit, task.UniqueID.ToString());

					if (card == null || card.ExternalSystemName != ServiceName) 
					{
						Log.Debug("Create new card for Task [{0}]", task.UniqueID.ToString());
						CreateCardFromTask(boardMapping, task, importFields);
					} 
					else 
					{
						Log.Debug("Previously created a card for Task [{0}]", task.UniqueID.ToString());
							if (boardMapping.UpdateCards)
								TaskUpdated(task, card, boardMapping, importFields);
							else
								Log.Info("Skipped card update because 'UpdateCards' is disabled.");
					}
				}
			}
			Log.Info("{0} item(s) queried.\n", tasks.Count);
		}

		private bool FilterTasks(Task task, List<Filter> filters)
		{
			if (!filters.Any())
				return true;

			return FilterIncludeTasks(task, filters.Where(x => x.FilterType == FilterType.Include).ToList())
			       && FilterExcludeTasks(task, filters.Where(x => x.FilterType == FilterType.Exclude).ToList());
		}

		private bool FilterIncludeTasks(Task task, List<Filter> filters)
		{
			if (!filters.Any())
				return true;

			foreach (var filter in filters)
			{
				var res = task.GetText(filter.TargetFieldName);
				if (res != null && !string.IsNullOrEmpty(res) && !string.IsNullOrEmpty(filter.FilterValue))
				{
					if (res.ToLowerInvariant() == filter.FilterValue.ToLowerInvariant())
						return true;
				}
			}
			return false;		
		}

		private bool FilterExcludeTasks(Task task, List<Filter> filters)
		{
			if (!filters.Any())
				return true;

			foreach (var filter in filters) 
			{
				var res = task.GetText(filter.TargetFieldName);
				if (res != null && !string.IsNullOrEmpty(res) && !string.IsNullOrEmpty(filter.FilterValue)) {
					if (res.ToLowerInvariant() == filter.FilterValue.ToLowerInvariant())
						return true;
				}
			}
			return true;				
		}

		private void CreateCardFromTask(BoardMapping project, Task task, Dictionary<LeanKitField, List<string>> importFields) 
		{
			if (task == null) return;

			var boardId = project.Identity.LeanKit;

			var mappedCardType = task.LeanKitCardType(project, importFields);
			var laneId = project.LanesFromState("All Tasks").First();
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

			var dueDate = task.GetDueDate(importFields);
			if (dueDate.HasValue) 
			{
				card.DueDate = dueDate.Value.ToString(dateFormat);
			}

			var startDate = task.GetStartDate(importFields);
			if (startDate.HasValue)
			{
				card.StartDate = startDate.Value.ToString(dateFormat);
			}

			if (task.GetIsBlocked(importFields))
			{
				card.IsBlocked = true;
				card.BlockReason = task.GetBlockedReason(importFields) ?? "Task is blocked in Microsoft Project.";
			}

			var cos = task.GetClassOfService(importFields);
			if (!string.IsNullOrEmpty(cos))
			{
				var board = LeanKit.GetBoard(boardId);
				if (board != null && board.ClassOfServiceEnabled)
				{
					var classOfService = board.ClassesOfService.FirstOrDefault(x => x.Title.ToLowerInvariant() == cos);
					if (classOfService != null)
					{
						card.ClassOfServiceId = classOfService.Id;
					}
				}
			}

			var size = task.GetSize(importFields);
			if (size > 0)
			{
				card.Size = size;
			}

			if (!string.IsNullOrEmpty(task.Hyperlink)) 
			{
				card.ExternalSystemUrl = task.Hyperlink;
			}

			var tags = task.GetTags(importFields);
			if (!string.IsNullOrEmpty(tags))
			{
				card.Tags = tags;
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

		private void TaskUpdated(Task task, Card card, BoardMapping boardMapping, Dictionary<LeanKitField, List<string>> importFields) 
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

			var dueDate = task.GetDueDate(importFields);
			if (dueDate.HasValue) 
			{
				var dueDateString = dueDate.Value.ToString(dateFormat);
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

			var startDate = task.GetStartDate(importFields);
			if (startDate.HasValue) 
			{
				var startDateString = startDate.Value.ToString(dateFormat);
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
			var tags = task.GetTags(importFields);
			if (!string.IsNullOrEmpty(tags)) 
			{
				if (card.Tags != tags)
				{
					card.Tags = tags;
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

			var isBlocked = task.GetIsBlocked(importFields);
			if (card.IsBlocked != isBlocked)
			{
				card.IsBlocked = isBlocked;
				card.BlockReason = task.GetBlockedReason(importFields) ?? "Task is blocked/unblocked in Microsoft Project.";
				saveCard = true;
			}

			var cos = task.GetClassOfService(importFields);
			if (!string.IsNullOrEmpty(cos)) 
			{
				var board = LeanKit.GetBoard(boardId);
				if (board != null && board.ClassOfServiceEnabled) 
				{
					var classOfService = board.ClassesOfService.FirstOrDefault(x => x.Title.ToLowerInvariant() == cos.ToLowerInvariant());
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

		private Dictionary<LeanKitField, List<string>> GetImportFields(BoardMapping boardMapping)
		{
			var importFields = new Dictionary<LeanKitField, List<string>>();
			foreach (var field in (LeanKitField[]) Enum.GetValues(typeof(LeanKitField)))
			{
				importFields.Add(field, boardMapping.GetTargetFieldFor(field, SyncDirection.ToLeanKit));
			}
			return importFields;
		}
	}
}
