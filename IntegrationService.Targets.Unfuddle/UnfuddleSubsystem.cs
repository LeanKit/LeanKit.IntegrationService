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
using Kanban.API.Client.Library;
using Kanban.API.Client.Library.TransferObjects;
using RestSharp;
using ServiceStack.Text;

namespace IntegrationService.Targets.Unfuddle
{
    public class Unfuddle : TargetBase
    {
	    private readonly IRestClient _restClient;
		private string _externalUrlTemplate;
	    private const string ServiceName = "Unfuddle";

	    public Unfuddle(IBoardSubscriptionManager subscriptions) : base(subscriptions)
        {
			_restClient = new RestClient
				{
					BaseUrl = Configuration.Target.Url,
					Authenticator = new HttpBasicAuthenticator(Configuration.Target.User, Configuration.Target.Password)
				};
        }

		public Unfuddle(IBoardSubscriptionManager subscriptions, 
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
			if (Configuration != null) 
			{
				_externalUrlTemplate = Configuration.Target.Url + "/a#/projects/{0}/tickets/by_number/{1}";
			}
		}

        protected override void CardUpdated(Card updatedCard, List<string> updatedItems, BoardMapping boardMapping)
        {
			if (updatedCard.ExternalSystemName != ServiceName)
				return;

	        var target = boardMapping.Identity.Target;

			if (string.IsNullOrEmpty(updatedCard.ExternalCardID)) 
			{
				Log.Debug("Ignoring card [{0}] with missing external id value.", updatedCard.ExternalCardID);
				return;
			}

			//https://mysubdomain.unfuddle.com/api/v1/projects/{id}/tickets/{id}
			var request = new RestRequest(string.Format("/api/v1/projects/{0}/tickets/{1}", target, updatedCard.ExternalCardID), Method.GET);
			var unfuddleResp = _restClient.Execute(request);

			if (unfuddleResp.StatusCode != HttpStatusCode.OK)
			{
				var serializer = new JsonSerializer<ErrorMessage>();
				var errorMessage = serializer.DeserializeFromString(unfuddleResp.Content);
				Log.Error(string.Format("Unable to get tickets from Unfuddle, Error: {0}. Check your board/repo mapping configuration.", errorMessage.Message));
			}
			else
			{
				var ticketToUpdate = new JsonSerializer<Ticket>().DeserializeFromString(unfuddleResp.Content);

				if (ticketToUpdate != null && ticketToUpdate.Id.ToString() == updatedCard.ExternalCardID)
				{
					bool isDirty = false;

					string summaryXml = "";
					string descriptionXml = "";
					string priorityXml = "";
					string dueDateXml = "";

					if (updatedItems.Contains("Title") && ticketToUpdate.Summary != updatedCard.Title)
					{
						summaryXml = "<summary>" + updatedCard.Title + "</summary>";
						isDirty = true;
					}

					if (updatedItems.Contains("Description") && ticketToUpdate.Description != updatedCard.Description)
					{
						string updatedDescription = updatedCard.Description;
						if (!string.IsNullOrEmpty(updatedDescription))
						{
							updatedDescription = updatedDescription.Replace("<p>", "").Replace("</p>", "");
						}
						descriptionXml = "<description>" + updatedDescription + "</description>";
						isDirty = true;
					}

					if (updatedItems.Contains("Priority"))
					{
						priorityXml = "<priority>" + GetPriority(updatedCard.Priority) + "</priority>";
						isDirty = true;
					}

					if (updatedItems.Contains("DueDate"))
					{
						DateTime updatedDate;
						var isDate = DateTime.TryParse(updatedCard.DueDate, out updatedDate);
						if (isDate)
						{
							dueDateXml = "<due-on>" + updatedDate.ToString("o") + "</due-on>";
							isDirty = true;
						}
					}

					string comment = "";
					if (updatedItems.Contains("Size")) 
					{
						comment += "LeanKit card Size changed to " + updatedCard.Size + ". ";
					}
					if (updatedItems.Contains("Tags")) 
					{
						comment += "LeanKit card Tags changed to " + updatedCard.Tags + ". ";
					}
					if (updatedItems.Contains("Blocked")) 
					{
						if (updatedCard.IsBlocked)
							comment += "LeanKit card is blocked: " + updatedCard.BlockReason + ". ";
						else
							comment += "LeanKit card is no longer blocked: " + updatedCard.BlockReason + ". ";
					}

					if (isDirty) 
					{
						try 
						{
							//https://mysubdomain.unfuddle.com/api/v1/api/v1/projects/{id}/tickets/{id}
							var updateRequest = new RestRequest(string.Format("/api/v1/projects/{0}/tickets/{1}", target, updatedCard.ExternalCardID), Method.PUT);
							updateRequest.AddHeader("Accept", "application/json");
							updateRequest.AddHeader("Content-type", "application/xml");
							updateRequest.AddParameter(
									"application/xml", 
									string.Format("<ticket>{0}{1}{2}{3}</ticket>", summaryXml, descriptionXml, priorityXml, dueDateXml), 
									ParameterType.RequestBody
								);

							var resp = _restClient.Execute(updateRequest);

							if (resp.StatusCode != HttpStatusCode.OK) 
							{
								if (resp.Content != null && !string.IsNullOrEmpty(resp.Content.Trim())) 
								{
									var serializer = new JsonSerializer<ErrorMessage>();
									var errorMessage = serializer.DeserializeFromString(resp.Content);
									Log.Error(string.Format("Unable to update Ticket [{0}], Description: {1}, Message: {2}",
															 updatedCard.ExternalCardID, resp.StatusDescription, errorMessage.Message));
								} 
								else 
								{
									Log.Error(string.Format("Unable to update Ticket [{0}], Description: {1}, Message: {2}",
															updatedCard.ExternalCardID, resp.StatusDescription, resp.StatusDescription));
								}
							} 
							else 
							{
								Log.Debug(String.Format("Updated Ticket [{0}]", updatedCard.ExternalCardID, ticketToUpdate.Status));
							}
						} 
						catch (Exception ex) 
						{
							Log.Error(string.Format("Unable to update Ticket [{0}], Exception: {1}", updatedCard.ExternalCardID, ex.Message));
						}
					}

					if (!string.IsNullOrEmpty(comment))
					{
						try {
							//https://mysubdomain.unfuddle.com/api/v1/api/v1/projects/{id}/tickets/{id}/comments
							var updateRequest = new RestRequest(string.Format("/api/v1/projects/{0}/tickets/{1}/comments", target, updatedCard.ExternalCardID), Method.POST);
							updateRequest.AddHeader("Accept", "application/json");
							updateRequest.AddHeader("Content-type", "application/xml");
							updateRequest.AddParameter(
									"application/xml",
									string.Format("<comment><body>{0}</body></comment>", comment),
									ParameterType.RequestBody
								);

							var resp = _restClient.Execute(updateRequest);

							if (resp.StatusCode != HttpStatusCode.OK && resp.StatusCode != HttpStatusCode.Created) 
							{
								if (resp.Content != null && !string.IsNullOrEmpty(resp.Content.Trim())) 
								{
									var serializer = new JsonSerializer<ErrorMessage>();
									var errorMessage = serializer.DeserializeFromString(resp.Content);
									Log.Error(string.Format("Unable to create comment on updated Ticket [{0}], Description: {1}, Message: {2}", updatedCard.ExternalCardID, resp.StatusDescription, errorMessage.Message));
								} 
								else 
								{
									Log.Error(string.Format("Unable to create comment on updated Ticket [{0}], Description: {1}, Message: {2}", updatedCard.ExternalCardID, resp.StatusDescription, resp.StatusDescription));
								}
							} 
							else 
							{
								Log.Debug(String.Format("Created comment on updated Ticket [{0}]", updatedCard.ExternalCardID, ticketToUpdate.Status));
							}
						} 
						catch (Exception ex) 
						{
							Log.Error(string.Format("Unable to create comment on updated Ticket [{0}], Exception: {1}", updatedCard.ExternalCardID, ex.Message));
						}						
					}
				}
			}		
        }

		private void TicketUpdated(Ticket ticket, Card card, BoardMapping boardMapping) 
		{
			Log.Info("Ticket [{0}] updated, comparing to corresponding card...", ticket.Id);

			long boardId = boardMapping.Identity.LeanKit;

			// sync and save those items that are different (of title, description, priority)
			bool saveCard = false;
			if (ticket.Summary != card.Title) 
			{
				card.Title = ticket.Summary;
				saveCard = true;
			}

			if (ticket.Description != card.Description) 
			{
				card.Description = ticket.Description;
				saveCard = true;
			}

			var priority = ticket.LeanKitPriority();
			if (priority != card.Priority) 
			{
				card.Priority = priority;
				saveCard = true;
			}

			if (ticket.Due_On != null) 
			{
				if (CurrentUser != null) 
				{
					var dateFormat = CurrentUser.DateFormat ?? "MM/dd/yyyy";
					var dueDateString = ticket.Due_On.Value.ToString(dateFormat);
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

			if (saveCard) 
			{
				Log.Info("Updating card [{0}]", card.Id);
				LeanKit.UpdateCard(boardId, card);
			}
		}

        protected override void Synchronize(BoardMapping project)
        {
            Log.Debug("Polling Unfuddle for Tickets");

			var queryAsOfDate = QueryDate.AddMilliseconds(Configuration.PollingFrequency * -1.5);

			var unfuddleQuery = !string.IsNullOrEmpty(project.Query) 
				? string.Format(project.Query, queryAsOfDate.ToString("yyyy/MM/dd hh:mm")) 
				: string.Format("status-eq-{0},created_at-gt-{1}", project.QueryStates[0], queryAsOfDate.ToString("yyyy/MM/dd hh:mm"));

			//http://mysubdomain.unfuddle.com/api/v1/projects/{id}/ticket_reports/dynamic?sort_by=created_at&sort_direction=ASC&conditions_string=status-eq-new,created_at-gt-yyyy/MM/dd hh:mm
			var request = new RestRequest(string.Format("/api/v1/projects/{0}/ticket_reports/dynamic", project.Identity.Target), Method.GET);
	        request.AddParameter("sort_by", "created_at");
			request.AddParameter("sort_direction", "ASC");
	        request.AddParameter("conditions_string", unfuddleQuery);
			//, "id,status,priority,summary,description,type");
	        request.AddParameter("limit", "500");

			var unfuddleResp = _restClient.Execute(request);

			if (unfuddleResp.StatusCode != HttpStatusCode.OK) 
			{
				var serializer = new JsonSerializer<ErrorMessage>();
				var errorMessage = serializer.DeserializeFromString(unfuddleResp.Content);
				Log.Error(string.Format("Unable to get tickets from Unfuddle, Error: {0}. Check your board/project mapping configuration.", errorMessage.Message));
				return;
			}

			var resp = new JsonSerializer<TicketsResponse>().DeserializeFromString(unfuddleResp.Content);

			Log.Info("\nQueried [{0}] at {1} for changes after {2}", project.Identity.Target, QueryDate, queryAsOfDate.ToString("o"));
			
			if (resp != null && resp.Groups != null && resp.Groups.Any())
			{

				foreach (var group in resp.Groups)
				{
					if (group != null && group.Tickets != null && group.Tickets.Any())
					{
						var tickets = group.Tickets;
						foreach (var ticket in tickets) 
						{
							Log.Info("Ticket [{0}]: {1}, {2}, {3}", ticket.Id, ticket.Summary, ticket.Status, ticket.Priority);

							// does this workitem have a corresponding card?
							var card = LeanKit.GetCardByExternalId(project.Identity.LeanKit, ticket.Id.ToString());

							if (card == null || card.ExternalSystemName != ServiceName) 
							{
								Log.Debug("Create new card for Ticket [{0}]", ticket.Id);
								CreateCardFromItem(project, ticket);
							} 
							else 
							{
								Log.Debug("Previously created a card for Ticket [{0}]", ticket.Id);
								if (project.UpdateCards)
									TicketUpdated(ticket, card, project);
								else
									Log.Info("Skipped card update because 'UpdateCards' is disabled.");
							}
						}
														
					}
				}
				Log.Info("{0} item(s) queried.\n", resp.Count);		
			}     
        }

        private void CreateCardFromItem(BoardMapping project, Ticket ticket)
        {
            if (ticket == null) return;

            var boardId = project.Identity.LeanKit;

	        var mappedCardType = ticket.LeanKitCardType(project);

            var laneId = project.LaneFromState(ticket.Status);
	        var card = new Card
                {
                    Active = true,
                    Title = ticket.Summary,
                    Description = ticket.Description,
                    Priority = ticket.LeanKitPriority(),
                    TypeId = mappedCardType.Id,
                    TypeName = mappedCardType.Name,
                    LaneId = laneId,
                    ExternalCardID = ticket.Id.ToString(),
                    ExternalSystemName = ServiceName,
                    ExternalSystemUrl = string.Format(_externalUrlTemplate, ticket.Id, project.Identity.Target)
                };

			var assignedUserId = CalculateAssignedUserId(boardId, ticket);
			if (assignedUserId != null)
				card.AssignedUserIds = new[] { assignedUserId.Value };

			if (ticket.Due_On != null) 
			{
				if (CurrentUser != null) 
				{
					var dateFormat = CurrentUser.DateFormat ?? "MM/dd/yyyy";
					card.DueDate = ticket.Due_On.Value.ToString(dateFormat);
				}
			}

			if ((card.Tags == null || !card.Tags.Contains(ServiceName)) && project.TagCardsWithTargetSystemName) 
			{
				if (string.IsNullOrEmpty(card.Tags))
					card.Tags = ServiceName;
				else
					card.Tags += "," + ServiceName;
			}

            Log.Info("Creating a card of type [{0}] for ticket [{1}] on Board [{2}] on Lane [{3}]", mappedCardType.Name, ticket.Id, boardId, laneId);

	        CardAddResult cardAddResult = null;

	        int tries = 0;
	        bool success = false;
	        while (tries < 10 && !success)
	        {
		        if (tries > 0)
		        {
			        Log.Error(string.Format("Attempting to create card for ticket [{0}] attempt number [{1}]", ticket.Id,			                                 tries));
			        // wait 5 seconds before trying again
			        Thread.Sleep(new TimeSpan(0, 0, 5));
		        }

		        try
		        {
			        cardAddResult = LeanKit.AddCard(boardId, card, "New Card From Unfuddle Ticket");
			        success = true;
		        }
		        catch (Exception ex)
		        {
			        Log.Error(string.Format("An error occurred: {0} - {1} - {2}", ex.GetType(), ex.Message, ex.StackTrace));
		        }
		        tries++;
	        }
	        if (cardAddResult != null) card.Id = cardAddResult.CardId;

	        Log.Info("Created a card [{0}] of type [{1}] for ticket [{2}] on Board [{3}] on Lane [{4}]", card.Id, mappedCardType.Name, ticket.Id, boardId, laneId);
        }

		public int GetPriority(int priority)
		{
			switch (priority) 
			{
				case 3:
					return 5;
				case 2:
					return 4;
				case 0:
					return 2;
				case 1:
				default:
					return 3;
			}			
		}

		public long? CalculateAssignedUserId(long boardId, Ticket ticket) 
		{
			if (ticket == null)
				return null;

			if (ticket.Assignee_Id > 0)
			{
				// http://mysubdomain.unfuddle.com/api/v1/people/{id}
				var request = new RestRequest(string.Format("/api/v1/people/{0}", ticket.Assignee_Id), Method.GET);
				var unfuddleResp = _restClient.Execute(request);

				if (unfuddleResp.StatusCode != HttpStatusCode.OK)
				{
					var serializer = new JsonSerializer<ErrorMessage>();
					var errorMessage = serializer.DeserializeFromString(unfuddleResp.Content);
					Log.Warn(string.Format("Unable to get user from Unfuddle, Error: {0}", errorMessage.Message));
				}
				else
				{
					var user = new JsonSerializer<Person>().DeserializeFromString(unfuddleResp.Content);

					if (user != null) 
					{
						var lkUser = LeanKit.GetBoard(boardId).BoardUsers.FirstOrDefault(x => x!= null && 
							((!string.IsNullOrEmpty(x.EmailAddress)) && (!string.IsNullOrEmpty(user.Email)) && x.EmailAddress.ToLowerInvariant() == user.Email.ToLowerInvariant()) ||
							((!string.IsNullOrEmpty(x.FullName)) && (!string.IsNullOrEmpty(user.Last_Name)) && x.FullName.ToLowerInvariant() == (user.First_Name + " " + user.Last_Name).ToLowerInvariant()) ||
							((!string.IsNullOrEmpty(x.UserName)) && (!string.IsNullOrEmpty(user.Username)) && x.UserName.ToLowerInvariant() == user.Username.ToLowerInvariant()));
						if (lkUser != null)
							return lkUser.Id;
					}
				}
			}

			return null;
		}

	    protected override void UpdateStateOfExternalItem(Card card, List<string> states, BoardMapping boardMapping)
	    {
			UpdateStateOfExternalItem(card, states, boardMapping, false);
	    }

        protected void UpdateStateOfExternalItem(Card card, List<string> states, BoardMapping mapping, bool runOnlyOnce)
		{
			if (card.ExternalSystemName != ServiceName)
				return;

			if (states == null || states.Count == 0)
				return;

			if (string.IsNullOrEmpty(card.ExternalCardID))
			{
				Log.Debug("Ignoring card [{0}] with missing external id value.", card.Id);
				return;
			}

			int tries = 0;
			bool success = false;
			while (tries < 10 && !success && (!runOnlyOnce || tries == 0))
			{
				if (tries > 0)
				{
					Log.Error(string.Format("Attempting to update external ticket [{0}] attempt number [{1}]", card.ExternalCardID,
					                         tries));
					// wait 5 seconds before trying again
					Thread.Sleep(new TimeSpan(0, 0, 5));
				}

				//https://mysubdomain.unfuddle.com/api/v1/projects/{id}/tickets/{id}
				var request = new RestRequest(string.Format("/api/v1/projects/{0}/tickets/{1}", mapping.Identity.Target, card.ExternalCardID), Method.GET);
				var unfuddleResp = _restClient.Execute(request);

				if (unfuddleResp.StatusCode != HttpStatusCode.OK)
				{
					var serializer = new JsonSerializer<ErrorMessage>();
					var errorMessage = serializer.DeserializeFromString(unfuddleResp.Content);
					Log.Error(string.Format("Unable to get tickets from Unfuddle, Error: {0}. Check your board/repo mapping configuration.", errorMessage.Message));
				}
				else
				{
					var ticketToUpdate = new JsonSerializer<Ticket>().DeserializeFromString(unfuddleResp.Content);

					if (ticketToUpdate != null && ticketToUpdate.Id.ToString() == card.ExternalCardID) 
					{
						// Check for a workflow mapping to the closed state
						if (states[0].Contains(">"))
						{
							var workflowStates = states[0].Split('>');

							// check to see if the workitem is already in one of the workflow states
							var alreadyInState = workflowStates.FirstOrDefault(x => x.Trim().ToLowerInvariant() == ticketToUpdate.Status.ToLowerInvariant());
							if (!string.IsNullOrEmpty(alreadyInState))
							{
								// change workflowStates to only use the states after the currently set state
								var currentIndex = Array.IndexOf(workflowStates, alreadyInState);
								if (currentIndex < workflowStates.Length - 1)
								{
									var updatedWorkflowStates = new List<string>();
									for (int i = currentIndex + 1; i < workflowStates.Length; i++)
									{
										updatedWorkflowStates.Add(workflowStates[i]);
									}
									workflowStates = updatedWorkflowStates.ToArray();
								}
							}

							if (workflowStates.Length > 0) 
							{
								foreach (string workflowState in workflowStates) 
								{
									//UpdateStateOfExternalItem(card, new LaneStateMap() { State = workflowState.Trim(), States = new List<string>() { workflowState.Trim() } }, mapping, runOnlyOnce);
								    UpdateStateOfExternalItem(card, new List<string> {workflowState.Trim()}, mapping, runOnlyOnce);
								}
								return;
							}
						}

						if (ticketToUpdate.Status.ToLowerInvariant() == states[0].ToLowerInvariant()) 
						{
							Log.Debug(string.Format("Ticket [{0}] is already in state [{1}]", ticketToUpdate.Id, states[0]));
							return;
						}

						ticketToUpdate.Status = states[0];

						try 
						{
							//https://mysubdomain.unfuddle.com/api/v1/api/v1/projects/{id}/tickets/{id}
							var updateRequest = new RestRequest(string.Format("/api/v1/projects/{0}/tickets/{1}", mapping.Identity.Target, card.ExternalCardID), Method.PUT);
							updateRequest.AddHeader("Accept", "application/json");
							updateRequest.AddHeader("Content-type", "application/xml");
							updateRequest.AddParameter("application/xml", "<ticket><status>" + ticketToUpdate.Status + "</status></ticket>", ParameterType.RequestBody);

							var resp = _restClient.Execute(updateRequest);

							if (resp.StatusCode != HttpStatusCode.OK) 
							{
								if (resp.Content != null && !string.IsNullOrEmpty(resp.Content.Trim()))
								{
									var serializer = new JsonSerializer<ErrorMessage>();
									var errorMessage = serializer.DeserializeFromString(resp.Content);
									Log.Error(string.Format("Unable to update Ticket [{0}] to [{1}], Description: {2}, Message: {3}",
									                         card.ExternalCardID, ticketToUpdate.Status, resp.StatusDescription, errorMessage.Message));
								}
								else
								{
									Log.Error(string.Format("Unable to update Ticket [{0}] to [{1}], Description: {2}, Message: {3}",
															card.ExternalCardID, ticketToUpdate.Status, resp.StatusDescription, resp.StatusDescription));
								}
							} 
							else 
							{
								success = true;
								Log.Debug(String.Format("Updated state for Ticket [{0}] to [{1}]", card.ExternalCardID, ticketToUpdate.Status));
							}
						} 
						catch (Exception ex) 
						{
							Log.Error(string.Format("Unable to update Ticket [{0}] to [{1}], Exception: {2}", card.ExternalCardID, ticketToUpdate.Status, ex.Message));
						}
					} 
					else 
					{
						Log.Debug(String.Format("Could not retrieve Ticket [{0}] for updating state to [{1}]", card.ExternalCardID, ticketToUpdate.Status));
					}
				}
				tries++;
			}
		}

		protected override void CreateNewItem(Card card, BoardMapping boardMapping) 
		{
			Log.Debug(String.Format("TODO: Create a Ticket from Card [{0}]", card.Id));
		}

		#region object model

		public class TicketsResponse
		{
			public List<Group> Groups { get; set; }
			public int Count { get; set; }

			public TicketsResponse()
			{
				Groups = new List<Group>();
			}
		}

		public class Group
		{
			public List<Ticket> Tickets { get; set; }
 
			public Group()
			{
				Tickets = new List<Ticket>();
			}
		}

		public class Ticket 
		{
			public long Id { get; set; }
			public long Assignee_Id { get; set; }
			public long Priority { get; set; }
			public string Status { get; set; }
			public string Description { get; set; }
			public string Summary { get; set; }
			public DateTime Created_At { get; set; }
			public DateTime? Due_On { get; set; }

			public Ticket()
			{
				Id = 0;
				Priority = 0;
				Status = "";
				Description = "";
				Summary = "";
				Created_At = DateTime.UtcNow;
				Assignee_Id = 0;
			}
		}

		public class Person
		{
			public long Id { get; set; }
			public string First_Name { get; set; }
			public string Last_Name { get; set; }
			public string Email { get; set; }
			public string Username { get; set; }
		}

		public class ErrorMessage 
		{
			public string Message { get; set; }
		}

		#endregion
	}
}
