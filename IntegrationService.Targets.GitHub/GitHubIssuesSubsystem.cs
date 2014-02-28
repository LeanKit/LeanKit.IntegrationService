//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using IntegrationService.Util;
using LeanKit.API.Client.Library;
using LeanKit.API.Client.Library.TransferObjects;
using RestSharp;
using ServiceStack.Text;

namespace IntegrationService.Targets.GitHub
{
    public class GitHubIssues : TargetBase
    {
	    private readonly IRestClient _restClient;
		private string _externalUrlTemplate;
	    private const string ServiceName = "GitHub";

	    public GitHubIssues(IBoardSubscriptionManager subscriptions) : base(subscriptions)
        {
			_restClient = new RestClient
				{
					BaseUrl = "https://api.github.com",
					Authenticator = new HttpBasicAuthenticator(Configuration.Target.User, Configuration.Target.Password)
				};
        }

		public GitHubIssues(IBoardSubscriptionManager subscriptions, 
							IConfigurationProvider<Configuration> configurationProvider, 
							ILocalStorage<AppSettings> localStorage, 
							ILeanKitClientFactory leanKitClientFactory, 
							IRestClient restClient) 
			: base(subscriptions, configurationProvider, localStorage, leanKitClientFactory)
		{
			_restClient = restClient;
		}

        public override void Init()
        {
            Log.Debug("Initializing GitHub-Issues...");
			_externalUrlTemplate = "https://github.com/" + Configuration.Target.Host + "/{0}/issues/{1}";
        }


        protected override void CardUpdated(Card updatedCard, List<string> updatedItems, BoardMapping boardMapping)
        {
	        if (!updatedCard.ExternalSystemName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase))
		        return;

			long issueNumber;

			string target = boardMapping.Identity.Target;

			// use external card id to get the GitHub Issue
			try {
				issueNumber = Convert.ToInt32(updatedCard.ExternalCardID.Split('|')[1]);
			} catch (Exception) {
				Log.Debug("Ignoring card [{0}] with missing external id value.", updatedCard.Id);
				return;
			}

			//"https://api.github.com/repos/{0}/{1}/issues/{2}
			var request = new RestRequest(string.Format("repos/{0}/{1}/issues/{2}", Configuration.Target.Host, target, issueNumber), Method.GET);
			var ghResp = _restClient.Execute(request);

	        if (ghResp.StatusCode != HttpStatusCode.OK)
	        {
		        var serializer = new JsonSerializer<ErrorMessage>();
		        var errorMessage = serializer.DeserializeFromString(ghResp.Content);
		        Log.Error(string.Format("Unable to get issue from GitHub, Error: {0}. Check your board mapping configuration.", errorMessage.Message));
	        }
	        else
	        {
		        var issueToUpdate = new JsonSerializer<Issue>().DeserializeFromString(ghResp.Content);

		        if (issueToUpdate != null && issueToUpdate.Number == issueNumber)
		        {
			        bool isDirty = false;

			        if (updatedItems.Contains("Title") && issueToUpdate.Title != updatedCard.Title)
			        {
				        issueToUpdate.Title = updatedCard.Title;
				        isDirty = true;
			        }

			        string updateJson = "{ \"title\": \"" + issueToUpdate.Title + "\"";

			        if (updatedItems.Contains("Description") && issueToUpdate.Body.SanitizeCardDescription() != updatedCard.Description)
			        {
				        updateJson += ", \"body\": \"" + updatedCard.Description + "\"";
				        isDirty = true;
			        }

					if (updatedItems.Contains("Tags"))
					{						
						var newLabels = updatedCard.Tags.Split(',');
						string updateLabels = "";
						int ctr = 0;
						foreach (string newLabel in newLabels)
						{
							if (ctr > 0)
								updateLabels += ", ";

							updateLabels += "{ \"name\": \"" + newLabel.Trim() + "\"}";

							ctr++;
						}
						updateJson += ", \"labels\": [" + updateLabels + "]";
						isDirty = true;
					}

					updateJson += "}";

			        string comment = "";
					if (updatedItems.Contains("Priority"))
					{
						comment += "LeanKit card Priority changed to " + updatedCard.Priority + ".<br />";
					}
			        if (updatedItems.Contains("DueDate"))
			        {
				        comment += "LeanKit card DueDate changed to " + updatedCard.DueDate + ".<br />";
			        }
			        if (updatedItems.Contains("Size"))
			        {
						comment += "LeanKit card Size changed to " + updatedCard.Size + ".<br />";
			        }
			        if (updatedItems.Contains("Blocked"))
			        {
						if (updatedCard.IsBlocked)
							comment += "LeanKit card is blocked: " + updatedCard.BlockReason + ".<br />";
						else
							comment += "LeanKit card is no longer blocked: " + updatedCard.BlockReason + ".<br />";
			        }

					if (isDirty)
					{
						try 
						{
							//"https://api.github.com/repos/{0}/{1}/issues/{2}
							var updateRequest = new RestRequest(string.Format("repos/{0}/{1}/issues/{2}", Configuration.Target.Host, target, issueNumber), Method.PATCH);
							updateRequest.AddParameter(
									"application/json",
									updateJson, 
									ParameterType.RequestBody
								);

							var resp = _restClient.Execute(updateRequest);

							if (resp.StatusCode != HttpStatusCode.OK) 
							{
								var serializer = new JsonSerializer<ErrorMessage>();
								var errorMessage = serializer.DeserializeFromString(resp.Content);
								Log.Error(string.Format("Unable to update Issue [{0}], Description: {1}, Message: {2}", issueNumber, resp.StatusDescription, errorMessage.Message));
							} 
							else 
							{
								Log.Debug(String.Format("Updated Issue [{0}]", issueNumber));
							}
						} 
						catch (Exception ex) 
						{
							Log.Error(string.Format("Unable to update Issue [{0}], Exception: {1}", issueNumber, ex.Message));
						}						
					}				

					if (!string.IsNullOrEmpty(comment))
					{
						try 
						{
							//"https://api.github.com/repos/{0}/{1}/issues/{2}/comments
							var newCommentRequest = new RestRequest(string.Format("repos/{0}/{1}/issues/{2}/comments", Configuration.Target.Host, target, issueNumber), Method.POST);
							newCommentRequest.AddParameter(
									"application/json",
									"{ \"body\": \"" + comment + "\"}",
									ParameterType.RequestBody
								);

							var resp = _restClient.Execute(newCommentRequest);

							if (resp.StatusCode != HttpStatusCode.OK || resp.StatusCode != HttpStatusCode.Created) 
							{
								var serializer = new JsonSerializer<ErrorMessage>();
								var errorMessage = serializer.DeserializeFromString(resp.Content);
								Log.Error(string.Format("Unable to create comment on updated Issue [{0}], Description: {1}, Message: {2}", issueNumber, resp.StatusDescription, errorMessage.Message));
							} 
							else 
							{
								Log.Debug(String.Format("Created comment on Updated Issue [{0}]", issueNumber));
							}
						} 
						catch (Exception ex) 
						{
							Log.Error(string.Format("Unable to create comment on updated Issue [{0}], Exception: {1}", issueNumber, ex.Message));
						}							
					}
		        }
	        }         
        }

		private void IssueUpdated(Issue issue, Card card, BoardMapping boardMapping) 
		{
			Log.Info("Issue [{0}] updated, comparing to corresponding card...", issue.Id);

			long boardId = boardMapping.Identity.LeanKit;

			// sync and save those items that are different (of title, description, priority)
			bool saveCard = false;
			if (issue.Title != card.Title) 
			{
				card.Title = issue.Title;
				saveCard = true;
			}

			if (issue.Body.SanitizeCardDescription() != card.Description) 
			{
				card.Description = issue.Body.SanitizeCardDescription();
				saveCard = true;
			}

			var priority = issue.LeanKitPriority();
			if (priority != card.Priority) 
			{
				card.Priority = priority;
				saveCard = true;
			}

			if (issue.Labels != null && issue.Labels.Count > 0)
			{
				var tags = string.Join(",", issue.Labels.Select(x => x.Name));
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

			if (issue.Milestone != null && issue.Milestone.Due_On != null) 
			{
				if (CurrentUser != null)
				{
					var dateFormat = CurrentUser.DateFormat ?? "MM/dd/yyyy";
					var dueDateString = issue.Milestone.Due_On.Value.ToString(dateFormat);
					if (card.DueDate != dueDateString)
					{
						card.DueDate = dueDateString;
						saveCard = true;
					}
				}
			} 
			else if (!string.IsNullOrEmpty(card.DueDate)) 
			{
				card.DueDate = "";
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

			var lanes = boardMapping.LanesFromState(issue.State);
			if (lanes.Count > 0 && lanes.All(x => x != card.LaneId))
			{
				card.LaneId = lanes.First();
				saveCard = true;
			}

			if (saveCard) 
			{
				Log.Info("Updating card [{0}]", card.Id);
				LeanKit.UpdateCard(boardId, card);
			}
		}

        protected override void Synchronize(BoardMapping project)
        {
			Log.Debug("Polling GitHub for Issues");

			var queryAsOfDate = QueryDate.AddMilliseconds(Configuration.PollingFrequency * -1.5);

			// GitHub will only let us query one state at a time :(
	        foreach (var state in project.QueryStates)
	        {
				//https://api.github.com/repos/{0}/{1}/issues?state=Open&since={2}			
				var request = new RestRequest(string.Format("repos/{0}/{1}/issues", Configuration.Target.Host, project.Identity.Target), Method.GET);
				request.AddParameter("state", state);
				request.AddParameter("since", queryAsOfDate.ToString("o"));

				var resp = _restClient.Execute(request);

				if (resp.StatusCode != HttpStatusCode.OK)
				{
					var serializer = new JsonSerializer<ErrorMessage>();
					var errorMessage = serializer.DeserializeFromString(resp.Content);
					Log.Error(string.Format("Unable to get issues from GitHub, Error: {0}. Check your board/repo mapping configuration.", errorMessage.Message));
					return;
				}

				var issues = new JsonSerializer<List<Issue>>().DeserializeFromString(resp.Content);

				Log.Info("\nQueried [{0}] at {1} for changes after {2}", project.Identity.Target, QueryDate, queryAsOfDate.ToString("o"));

		        if (issues == null || !issues.Any() || issues[0].Id <= 0) continue;

		        foreach (var issue in issues.Where(issue => issue.Id > 0))
		        {
			        Log.Info("Issue [{0}]: {1}, {2}, {3}", issue.Number, issue.Title, issue.User.Login, issue.State);

			        // does this workitem have a corresponding card?
			        var card = LeanKit.GetCardByExternalId(project.Identity.LeanKit, issue.Id + "|" + issue.Number.ToString());

					if (card == null || !card.ExternalSystemName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase))
			        {
				        Log.Debug("Create new card for Issue [{0}]", issue.Number);
				        CreateCardFromItem(project, issue);
			        }
			        else
			        {
				        Log.Debug("Previously created a card for Issue [{0}]", issue.Number);
				        if (project.UpdateCards)
					        IssueUpdated(issue, card, project);
				        else
					        Log.Info("Skipped card update because 'UpdateCards' is disabled.");
			        }
		        }
		        Log.Info("{0} item(s) queried.\n", issues.Count);
	        }
        }

        private void CreateCardFromItem(BoardMapping project, Issue issue)
        {
			if (issue == null) return;
        
            var boardId = project.Identity.LeanKit;
        
            var mappedCardType = issue.LeanKitCardType(project);
            var laneId = project.LanesFromState(issue.State).First();
            var card = new Card
            {
			    Active = true,
                Title = issue.Title,
				Description = issue.Body.SanitizeCardDescription(),
                Priority = issue.LeanKitPriority(),
                TypeId = mappedCardType.Id,
                TypeName = mappedCardType.Name,
                LaneId = laneId,
                ExternalCardID = issue.Id + "|" + issue.Number,
                ExternalSystemName = ServiceName,
				ExternalSystemUrl = string.Format(_externalUrlTemplate, project.Identity.Target, issue.Number)
            };

			var assignedUserId = issue.LeanKitAssignedUserId(boardId, LeanKit);
			if (assignedUserId != null)
				card.AssignedUserIds = new[] { assignedUserId.Value };

			if (issue.Milestone != null && issue.Milestone.Due_On != null) 
			{
				if (CurrentUser != null) 
				{
					var dateFormat = CurrentUser.DateFormat ?? "MM/dd/yyyy";
					card.DueDate = issue.Milestone.Due_On.Value.ToString(dateFormat);
				}
			}

			if (issue.Labels != null && issue.Labels.Any())
			{
				card.Tags = string.Join(",", issue.Labels.Select(x => x.Name).ToList());
			}

			if ((card.Tags == null || !card.Tags.Contains(ServiceName)) && project.TagCardsWithTargetSystemName) 
			{
				if (string.IsNullOrEmpty(card.Tags))
					card.Tags = ServiceName;
				else
					card.Tags += "," + ServiceName;
			}

            Log.Info("Creating a card of type [{0}] for Issue [{1}] on Board [{2}] on Lane [{3}]", mappedCardType.Name, issue.Number, boardId, laneId);

	        CardAddResult cardAddResult = null;

			int tries = 0;
			bool success = false;
			while (tries < 10 && !success) 
			{
				if (tries > 0) 
				{
					Log.Error(string.Format("Attempting to create card for issue [{0}] attempt number [{1}]", issue.Id,
											 tries));
					// wait 5 seconds before trying again
					Thread.Sleep(new TimeSpan(0, 0, 5));
				}

				try {
					cardAddResult = LeanKit.AddCard(boardId, card, "New Card From GitHub Issue");
					success = true;
				} catch (Exception ex) {
					Log.Error(string.Format("An error occurred: {0} - {1} - {2}", ex.GetType(), ex.Message, ex.StackTrace));
				}
				tries++;
			}
			card.Id = cardAddResult.CardId;
        
            Log.Info("Created a card [{0}] of type [{1}] for Issue [{2}] on Board [{3}] on Lane [{4}]", card.Id, mappedCardType.Name, issue.Number, boardId, laneId);
        }

	    protected override void UpdateStateOfExternalItem(Card card, List<string> states, BoardMapping boardMapping)
	    {
			UpdateStateOfExternalItem(card, states, boardMapping, false);
	    }

        protected void UpdateStateOfExternalItem(Card card, List<string> states, BoardMapping boardMapping, bool runOnlyOnce)
	    {
		    if (!card.ExternalSystemName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase))
			    return;

		    if (string.IsNullOrEmpty(card.ExternalCardID))
			    return;

			if (states == null || states.Count == 0)
				return;

			long issueNumber;

			string target = boardMapping.Identity.Target;

        	// use external card id to get the GitHub issue
        	try {
        		issueNumber = Convert.ToInt32(card.ExternalCardID.Split('|')[1]);
        	} catch (Exception) {
        		Log.Debug("Ignoring card [{0}] with missing external id value.", card.Id);
        		return;
        	}

			int tries = 0;
			bool success = false;
			while (tries < 10 && !success && (!runOnlyOnce || tries == 0))
			{
				if (tries > 0)
				{
					Log.Error(string.Format("Attempting to update external issue [{0}] attempt number [{1}]",issueNumber, tries));
					// wait 5 seconds before trying again
					Thread.Sleep(new TimeSpan(0, 0, 5));
				}

				//"https://api.github.com/repos/{0}/{1}/issues/{2}
				var request = new RestRequest(string.Format("repos/{0}/{1}/issues/{2}", Configuration.Target.Host, target, issueNumber), Method.GET);
				var ghResp = _restClient.Execute(request);

				if (ghResp.StatusCode != HttpStatusCode.OK)
				{
					var serializer = new JsonSerializer<ErrorMessage>();
					var errorMessage = serializer.DeserializeFromString(ghResp.Content);
					Log.Error(string.Format("Unable to get issue from GitHub, Error: {0}. Check your board mapping configuration.", errorMessage.Message));
				}
				else
				{
					var issueToUpdate = new JsonSerializer<Issue>().DeserializeFromString(ghResp.Content);

					if (issueToUpdate != null && issueToUpdate.Number == issueNumber)
					{
						if (issueToUpdate.State.ToLowerInvariant() == states[0].ToLowerInvariant())
						{
							Log.Debug(string.Format("Issue [{0}] is already in state [{1}]", issueToUpdate.Id, states[0]));
							return;											
						}

						issueToUpdate.State = states[0];

						try
						{
							//"https://api.github.com/repos/{0}/{1}/issues/{2}
							var updateRequest = new RestRequest(string.Format("repos/{0}/{1}/issues/{2}", Configuration.Target.Host, target, issueNumber), Method.PATCH);
							updateRequest.AddParameter("application/json", "{ \"state\": \"" + issueToUpdate.State + "\"}", ParameterType.RequestBody);

							var resp = _restClient.Execute(updateRequest);

							if (resp.StatusCode != HttpStatusCode.OK)
							{
								var serializer = new JsonSerializer<ErrorMessage>();
								var errorMessage = serializer.DeserializeFromString(resp.Content);
								Log.Error(string.Format("Unable to update Issue [{0}] to [{1}], Description: {2}, Message: {3}", issueNumber, issueToUpdate.State, resp.StatusDescription, errorMessage.Message));
							}
							else
							{
								success = true;
								Log.Debug(String.Format("Updated state for Issue [{0}] to [{1}]", issueNumber, issueToUpdate.State));
							}
						}
						catch (Exception ex)
						{
							Log.Error(string.Format("Unable to update Issue [{0}] to [{1}], Exception: {2}", issueNumber, issueToUpdate.State, ex.Message));
						}
					}
					else
					{
						Log.Debug(String.Format("Could not retrieve Issue [{0}] for updating state to [{1}]", issueNumber, issueToUpdate.State));
					}
				}
				tries++;
			}
		}

	    protected override void CreateNewItem(Card card, BoardMapping boardMapping)
	    {
			Log.Debug(String.Format("TODO: Create an Issue from Card [{0}]", card.Id));
	    }

		#region object model

		public class Issue
		{
			public long Id { get; set; }
			public long Number { get; set; }
			public string Title { get; set; }
			public string Body { get; set; }
			public string State { get; set; }
			public User User { get; set; }
			public User Assignee { get; set; }
			public string Url { get; set; }
			public List<Label> Labels { get; set; }
			public Milestone Milestone { get; set; }

			public Issue()
			{
				Id = 0;
				Number = 0;
				Title = "";
				Body = "";
				State = "";
				User = new User();
				Assignee = new User();
				Url = "";
				Labels = new List<Label>();
				Milestone = new Milestone();
			}
		}

		public class User
		{
			public string Login { get; set; }

			public User()
			{
				Login = "";
			}
		}

		public class Label
		{
			public string Name { get; set; }

			public Label()
			{
				Name = "";
			}
		}

		public class Milestone
		{
			public string Title { get; set; }
			public string Description { get; set; }
			public DateTime? Due_On { get; set; }

			public Milestone()
			{
				Title = "";
				Description = "";
			}
		}

		public class ErrorMessage
		{
			public string Message { get; set; }

			public ErrorMessage()
			{
				Message = "";
			}
		}

		#endregion
	}
}
