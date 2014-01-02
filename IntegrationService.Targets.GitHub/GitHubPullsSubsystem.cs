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
    public class GitHubPulls : TargetBase
    {
	    private readonly IRestClient _restClient;
		private string _externalUrlTemplate;
	    private const string ServiceName = "GitHub";

	    public GitHubPulls(IBoardSubscriptionManager subscriptions) : base(subscriptions)
        {
			_restClient = new RestClient
				{
					BaseUrl = "https://api.github.com",
					Authenticator = new HttpBasicAuthenticator(Configuration.Target.User, Configuration.Target.Password)
				};
        }

		public GitHubPulls(IBoardSubscriptionManager subscriptions, 
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
            Log.Debug("Initializing GitHub-Pulls...");
			_externalUrlTemplate = "https://github.com/" + Configuration.Target.Host + "/{0}/pull/{1}";
        }

        protected override void CardUpdated(Card updatedCard, List<string> updatedItems, BoardMapping boardMapping)
        {
			if (updatedCard.ExternalSystemName != ServiceName)
				return;

			if (string.IsNullOrEmpty(updatedCard.ExternalCardID))
				return;

			long issueNumber;

			string target = boardMapping.Identity.Target;

			// use external card id to get the GitHub Issue
			try 
			{
				issueNumber = Convert.ToInt32(updatedCard.ExternalCardID.Split('|')[1]);
			} 
			catch (Exception) 
			{
				Log.Debug("Ignoring card [{0}] with missing external id value.", updatedCard.Id);
				return;
			}

			//"https://api.github.com/repos/{0}/{1}/pulls/{2}
			var request = new RestRequest(string.Format("repos/{0}/{1}/pulls/{2}", Configuration.Target.Host, target, issueNumber), Method.GET);
			var ghResp = _restClient.Execute(request);

			if (ghResp.StatusCode != HttpStatusCode.OK) 
			{
				var serializer = new JsonSerializer<ErrorMessage>();
				var errorMessage = serializer.DeserializeFromString(ghResp.Content);
				Log.Error(string.Format("Unable to get Pull Request from GitHub, Error: {0}. Check your board mapping configuration.", errorMessage.Message));
			} 
			else 
			{
				var pullToUpdate = new JsonSerializer<Pull>().DeserializeFromString(ghResp.Content);

				if (pullToUpdate != null && pullToUpdate.Number == issueNumber) 
				{
					bool isDirty = false;

					if (updatedItems.Contains("Title") && pullToUpdate.Title != updatedCard.Title) 
					{
						pullToUpdate.Title = updatedCard.Title;
						isDirty = true;
					}

					if (updatedItems.Contains("Description") && pullToUpdate.Body != updatedCard.Description) 
					{
						pullToUpdate.Body = updatedCard.Description;
						isDirty = true;
					}

					// Do nothing with Priority, DueDate, Size, Blocked, or Tags because GitHub pull requests do not have comments
					//if (updatedItems.Contains("Priority")) 
					//if (updatedItems.Contains("DueDate")) 
					//if (updatedItems.Contains("Size")) 
					//if (updatedItems.Contains("Blocked")) 
					//if (updatedItems.Contains("Tags")) 

					if (isDirty) 
					{
						try 
						{
							//"https://api.github.com/repos/{0}/{1}/pulls/{2}
							var updateRequest = new RestRequest(string.Format("repos/{0}/{1}/pulls/{2}", Configuration.Target.Host, target, issueNumber), Method.PATCH);						
							updateRequest.AddParameter(
									"application/json",
									"{ \"title\": \"" + pullToUpdate.Title + "\", \"body\": \"" + pullToUpdate.Body + "\"}",
									ParameterType.RequestBody
								);

							var resp = _restClient.Execute(updateRequest);

							if (resp.StatusCode != HttpStatusCode.OK) 
							{
								var serializer = new JsonSerializer<ErrorMessage>();
								var errorMessage = serializer.DeserializeFromString(resp.Content);
								Log.Error(string.Format("Unable to update Pull Request [{0}], Description: {1}, Message: {2}", issueNumber, resp.StatusDescription, errorMessage.Message));
							} 
							else 
							{
								Log.Debug(String.Format("Updated Pull Request [{0}]", issueNumber));
							}
						} 
						catch (Exception ex) 
						{
							Log.Error(string.Format("Unable to update Pull Request [{0}], Exception: {1}", issueNumber, ex.Message));
						}
					}
				}
			}
        }

		private void PullUpdated(Pull pull, Card card, BoardMapping boardMapping) 
		{
			Log.Info("Pull [{0}] updated, comparing to corresponding card...", pull.Id);

			long boardId = boardMapping.Identity.LeanKit;

			// sync and save those items that are different (of title, description, priority)
			bool saveCard = false;
			if (pull.Title != card.Title) 
			{
				card.Title = pull.Title;
				saveCard = true;
			}

			if (pull.Body != card.Description) 
			{
				card.Description = pull.Body;
				saveCard = true;
			}

			var priority = pull.LeanKitPriority();
			if (priority != card.Priority) 
			{
				card.Priority = priority;
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

			if (saveCard) 
			{
				Log.Info("Updating card [{0}]", card.Id);
				LeanKit.UpdateCard(boardId, card);
			}
		}

        protected override void Synchronize(BoardMapping project)
        {
			Log.Debug("Polling GitHub for Pull Requests");

			var queryAsOfDate = QueryDate.AddMilliseconds(Configuration.PollingFrequency * -1.5);

			//https://api.github.com/repos/{0}/{1}/pulls?state=Open
			var request = new RestRequest(string.Format("repos/{0}/{1}/pulls", Configuration.Target.Host, project.Identity.Target), Method.GET);
	        request.AddParameter("state", project.QueryStates[0]);

			var resp = _restClient.Execute(request);

			if (resp.StatusCode != HttpStatusCode.OK) 
			{
				var serializer = new JsonSerializer<ErrorMessage>();
				var errorMessage = serializer.DeserializeFromString(resp.Content);
				Log.Error(string.Format("Unable to get Pull Requests from GitHub, Error: {0}. Check your board/repo mapping configuration.", errorMessage.Message));
				return;
			}

			var pulls = new JsonSerializer<List<Pull>>().DeserializeFromString(resp.Content);

			Log.Info("\nQueried [{0}] at {1} for changes after {2}", project.Identity.Target, QueryDate, queryAsOfDate.ToString("o"));

	        if (pulls != null && pulls.Any() && pulls[0].Id > 0)
	        {
		        foreach (var pull in pulls)
		        {
			        if (pull.Id > 0)
			        {
				        Log.Info("Pull Requests [{0}]: {1}, {2}, {3}", pull.Number, pull.Title, pull.User.Login, pull.State);

				        // does this workitem have a corresponding card?
				        var card = LeanKit.GetCardByExternalId(project.Identity.LeanKit, pull.Id + "|" + pull.Number.ToString());

				        if (card == null || card.ExternalSystemName != ServiceName)
				        {
					        Log.Debug("Create new card for Pull Request [{0}]", pull.Number);
					        CreateCardFromItem(project, pull);
				        }
				        else
				        {
					        Log.Debug("Previously created a card for Pull Request [{0}]", pull.Number);
							if (project.UpdateCards)
								PullUpdated(pull, card, project);
							else
								Log.Info("Skipped card update because 'UpdateCards' is disabled.");
				        }
			        }
		        }
		        Log.Info("{0} item(s) queried.\n", pulls.Count);
	        }
        }

        private void CreateCardFromItem(BoardMapping project, Pull pull)
        {
			if (pull == null) return;
        
            var boardId = project.Identity.LeanKit;
        
            var mappedCardType = pull.LeanKitCardType(project);
            var laneId = project.LanesFromState(pull.State).First();
            var card = new Card
            {
			    Active = true,
                Title = pull.Title,
                Description = pull.Body,
				Priority = pull.LeanKitPriority(),
                TypeId = mappedCardType.Id,
                TypeName = mappedCardType.Name,
                LaneId = laneId,
                ExternalCardID = pull.Id.ToString() + "|" + pull.Number.ToString(),
                ExternalSystemName = ServiceName, 				
				ExternalSystemUrl = string.Format(_externalUrlTemplate, project.Identity.Target, pull.Number.ToString())
            };

			var assignedUserId = pull.LeanKitAssignedUser(boardId, LeanKit);
			if (assignedUserId != null)
				card.AssignedUserIds = new[] { assignedUserId.Value };

			if ((card.Tags == null || !card.Tags.Contains(ServiceName)) && project.TagCardsWithTargetSystemName) 
			{
				if (string.IsNullOrEmpty(card.Tags))
					card.Tags = ServiceName;
				else
					card.Tags += "," + ServiceName;
			}

            Log.Info("Creating a card of type [{0}] for Pull Request [{1}] on Board [{2}] on Lane [{3}]", mappedCardType.Name, pull.Number, boardId, laneId);

	        CardAddResult cardAddResult = null;

			int tries = 0;
			bool success = false;
			while (tries < 10 && !success) 
			{
				if (tries > 0) 
				{
					Log.Error(string.Format("Attempting to create card for Pull Request [{0}] attempt number [{1}]", pull.Id,
											 tries));
					// wait 5 seconds before trying again
					Thread.Sleep(new TimeSpan(0, 0, 5));
				}

				try {
					cardAddResult = LeanKit.AddCard(boardId, card, "New Card From GitHub Pull Request");
					success = true;
				} catch (Exception ex) {
					Log.Error(string.Format("An error occurred: {0} - {1} - {2}", ex.GetType(), ex.Message, ex.StackTrace));
				}
				tries++;
			}
	        if (cardAddResult != null) card.Id = cardAddResult.CardId;

	        Log.Info("Created a card [{0}] of type [{1}] for Pull Request [{2}] on Board [{3}] on Lane [{4}]", card.Id, mappedCardType.Name, pull.Number, boardId, laneId);
        }

	    protected override void UpdateStateOfExternalItem(Card card, List<string> states, BoardMapping boardMapping)
	    {
			UpdateStateOfExternalItem(card, states, boardMapping, false);
	    }

        protected void UpdateStateOfExternalItem(Card card, List<string> states, BoardMapping boardMapping, bool runOnlyOnce) 
		{
			if (card.ExternalSystemName != ServiceName)
				return;

			if (string.IsNullOrEmpty(card.ExternalCardID))
				return;

			if (states == null || states.Count == 0)
				return;

        	long issueNumber;

			string target = boardMapping.Identity.Target;

        	// use external card id to get the GitHub Pull Request
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
					Log.Error(string.Format("Attempting to update external Pull Request [{0}] attempt number [{1}]",issueNumber, tries));
					// wait 5 seconds before trying again
					Thread.Sleep(new TimeSpan(0, 0, 5));
				}

				//"https://api.github.com/repos/{0}/{1}/pulls/{2}
				var request = new RestRequest(string.Format("repos/{0}/{1}/pulls/{2}", Configuration.Target.Host, target, issueNumber), Method.GET);
				var ghResp = _restClient.Execute(request);

				if (ghResp.StatusCode != HttpStatusCode.OK)
				{
					var serializer = new JsonSerializer<ErrorMessage>();
					var errorMessage = serializer.DeserializeFromString(ghResp.Content);
					Log.Error(string.Format("Unable to get Pull Request from GitHub, Error: {0}. Check your board mapping configuration.", errorMessage.Message));
				}
				else
				{
					var pullToUpdate = new JsonSerializer<Pull>().DeserializeFromString(ghResp.Content);

					if (pullToUpdate != null && pullToUpdate.Number == issueNumber)
					{
						if (pullToUpdate.State.ToLowerInvariant() == states[0].ToLowerInvariant()) 
						{
							Log.Debug(string.Format("Pull Request [{0}] is already in state [{1}]", pullToUpdate.Id, states[0]));
							return;
						}

						pullToUpdate.State = states[0];

						try
						{
							//"https://api.github.com/repos/{0}/{1}/pulls/{2}
							var updateRequest = new RestRequest(string.Format("repos/{0}/{1}/pulls/{2}", Configuration.Target.Host, target, issueNumber), Method.PATCH);
							updateRequest.AddParameter("application/json", "{ \"state\": \"" + pullToUpdate.State + "\"}", ParameterType.RequestBody);

							var resp = _restClient.Execute(updateRequest);

							if (resp.StatusCode != HttpStatusCode.OK)
							{
								var serializer = new JsonSerializer<ErrorMessage>();
								var errorMessage = serializer.DeserializeFromString(resp.Content);
								Log.Error(string.Format("Unable to update Pull Request [{0}] to [{1}], Description: {2}, Message: {3}", issueNumber, pullToUpdate.State, resp.StatusDescription, errorMessage.Message));
							}
							else
							{
								success = true;
								Log.Debug(String.Format("Updated state for Pull Request [{0}] to [{1}]", issueNumber, pullToUpdate.State));
							}
						}
						catch (Exception ex)
						{
							Log.Error(string.Format("Unable to update Pull Request [{0}] to [{1}], Exception: {2}", issueNumber, pullToUpdate.State, ex.Message));
						}
					}
					else
					{
						Log.Debug(String.Format("Could not retrieve Pull Request [{0}] for updating state to [{1}]", issueNumber, pullToUpdate.State));
					}
				}
				tries++;
			}
		}

		protected override void CreateNewItem(Card card, BoardMapping boardMapping) 
		{
			// do nothing
		}

		#region object model

		public class Pull
		{
			public long Id { get; set; }
			public long Number { get; set; }
			public string Title { get; set; }
			public string Body { get; set; }
			public string State { get; set; }
			public User User { get; set; }
			public string Url { get; set; }
			public Branch Head { get; set; }
			public Branch Base { get; set; }

			public Pull()
			{
				Id = 0;
				Number = 0;
				Title = "";
				Body = "";
				State = "";
				User = new User();
				Head = new Branch();
				Base = new Branch();
				Url = "";
			}
		}

		public class Branch
		{
			public User User { get; set; }

			public Branch()
			{
				User = new User();
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
