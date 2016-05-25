//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using IntegrationService.Util;
using LeanKit.API.Client.Library;
using LeanKit.API.Client.Library.TransferObjects;
using RestSharp;
using ServiceStack.Text;

namespace IntegrationService.Targets.JIRA
{
	public class Jira : TargetBase
	{
		private readonly IRestClient _restClient;
		private readonly IJiraConnection _jiraConnection;
		private string _externalUrlTemplate;
		private const string ServiceName = "JIRA";
		private const string QueryDateFormat = "yyyy-MM-dd HH:mm";
		private Dictionary<string, string> _sessionCookies;
		private object _customFieldsLock = new object();
		private object _priorityLock = new object();

		private List<Field> _customFields;
		private List<Priority> _priorities;

		protected List<Field> CustomFields
		{
			get
			{
				if (_customFields == null)
				{
					lock (_customFieldsLock)
					{
						if (_customFields == null)
						{
							_customFields = new List<Field>();
							var request = CreateRequest("rest/api/latest/field", Method.GET);
							var jiraResp = ExecuteRequest(request);

							if (jiraResp.StatusCode != HttpStatusCode.OK)
							{
								ProcessJiraError(jiraResp, "Unable to get custom fields from JIRA.");
							}
							else
							{
								var resp = new JsonSerializer<List<Field>>().DeserializeFromString(jiraResp.Content);
								if (resp == null || !resp.Any()) return _customFields;
								foreach (var field in resp.Where(field => field.Custom))
								{
									_customFields.Add(field);
								}
							}
						}
					}
				}
				return _customFields;
			}

			private set { _customFields = value; }
		}

		protected List<Priority> Priorities
		{
			get
			{
				if (_priorities != null) return _priorities;
				lock (_priorityLock)
				{
					if (_priorities == null)
					{
						_priorities = new List<Priority>();
						var request = CreateRequest("rest/api/latest/priority", Method.GET);
						var jiraResp = ExecuteRequest(request);

						if (jiraResp.StatusCode != HttpStatusCode.OK)
						{
							ProcessJiraError(jiraResp, "Unable to get priorities from JIRA.");
						}
						else
						{
							var resp = new JsonSerializer<List<Priority>>().DeserializeFromString(jiraResp.Content);
							if (resp == null || !resp.Any()) return _priorities;
							foreach (var p in resp)
							{
								_priorities.Add(p);
							}
						}
					}
					return _priorities;
				}
			}

			private set { _priorities = value; }
		}

		public Jira(IBoardSubscriptionManager subscriptions) : base(subscriptions)
		{
			_restClient = new RestClient
			{
				BaseUrl = new Uri(Configuration.Target.Url),
				//Authenticator = new HttpBasicAuthenticator(Configuration.Target.User, Configuration.Target.Password)
			};
			_jiraConnection = new JiraConnection(_restClient);
		}

		public Jira(IBoardSubscriptionManager subscriptions,
			IConfigurationProvider<Configuration> configurationProvider,
			ILocalStorage<AppSettings> localStorage,
			ILeanKitClientFactory leanKitClientFactory,
			IRestClient restClient,
			IJiraConnection jiraConnection)
			: base(subscriptions, configurationProvider, localStorage, leanKitClientFactory)
		{
			_restClient = restClient;
			_jiraConnection = jiraConnection;
		}


		private void ClearSessionCookies()
		{
			_sessionCookies?.Clear();
		}

		public void AddSessionCookieToRequest(RestRequest request)
		{
			if (_sessionCookies == null || _sessionCookies.Count == 0)
				RefreshSessionCookie(Configuration.Target.Url, Configuration.Target.User, Configuration.Target.Password);
			foreach (var k in _sessionCookies.Keys)
				request.AddCookie(k, _sessionCookies[k]);
		}

		private void RefreshSessionCookie(string host, string user, string password)
		{
			_sessionCookies = _jiraConnection.GetSessionCookie(host, user, password);
		}

		private RestRequest CreateRequest(string resource, Method method)
		{
			var request = new RestRequest(resource, method);
			AddSessionCookieToRequest(request);
			return request;
		}

		private IRestResponse ExecuteRequest(RestRequest request)
		{
			request.Debug(_restClient);
			return _restClient.Execute(request);
		}

		public override void Init()
		{
			if (Configuration == null) return;

			_externalUrlTemplate = Configuration.Target.Url + "/browse/{0}";

			// per project, if exclusions are defined, build type filter to exclude them
			foreach (var mapping in Configuration.Mappings)
			{
				mapping.ExcludedTypeQuery = "";
				if (string.IsNullOrEmpty(mapping.Excludes)) continue;
				Log.Debug("Excluded issue types for [{0}]: [{1}]", mapping.Identity.Target, mapping.Excludes);
				var excludedTypes = mapping.Excludes.Split(',');

				mapping.ExcludedTypeQuery = string.Format(" and issueType not in ({0})",
					string.Join(",", excludedTypes.Select(x => "'" + x.Trim() + "'").ToList()));
			}
		}

		protected override void CardUpdated(Card updatedCard, List<string> updatedItems, BoardMapping boardMapping)
		{
			if (!updatedCard.ExternalSystemName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase))
				return;

			if (string.IsNullOrEmpty(updatedCard.ExternalCardID))
				return;

			if (string.IsNullOrEmpty(updatedCard.ExternalCardID))
			{
				Log.Debug("Ignoring card [{0}] with missing external id value.", updatedCard.ExternalCardID);
				return;
			}

			var version = GetCachedCardVersion(updatedCard.Id, false);
			if (version >= updatedCard.Version)
			{
				Log.Debug("CardUpdated, Card [{0}] with version [{1}] has already been processed. Skipping comparison.", updatedCard.Id, updatedCard.Version);
				return;
			}

			//https://yoursite.atlassian.net/rest/api/latest/issue/{issueIdOrKey}
			var request = CreateRequest(string.Format("rest/api/latest/issue/{0}", updatedCard.ExternalCardID),
				Method.GET);
			var jiraResp = ExecuteRequest(request);

			if (jiraResp.StatusCode != HttpStatusCode.OK)
			{
				ProcessJiraError(jiraResp, string.Format("Unable to get issue [{0}] from JIRA.", updatedCard.ExternalCardID));
			}
			else
			{
				var issueToUpdate = new JsonSerializer<Issue>().DeserializeFromString(jiraResp.Content);

				if (issueToUpdate != null && issueToUpdate.Key == updatedCard.ExternalCardID)
				{
					bool isDirty = false;
					bool updateEpicName = false;

					var updateJson = "{ \"fields\": { ";

					if (updatedItems.Contains("Title") && issueToUpdate.Fields.Summary != updatedCard.Title)
					{
						issueToUpdate.Fields.Summary = updatedCard.Title;
						isDirty = true;
						updateEpicName = true;
					}

					updateJson += "\"summary\": \"" + issueToUpdate.Fields.Summary.Replace("\"", "\\\"") + "\"";

					if (updateEpicName)
					{
						if (issueToUpdate.Fields.IssueType.Name.ToLowerInvariant() == "epic")
						{
							if (CustomFields.Any())
							{
								var epicNameField = CustomFields.FirstOrDefault(x => x.Name == "Epic Name");
								if (epicNameField != null)
								{
									updateJson += ", \"" + epicNameField.Id + "\": \"" +
									              updatedCard.Title.Replace("\"", "\\\"") + "\"";
								}
							}
						}
					}

					if (updatedItems.Contains("Description") &&
					    issueToUpdate.Fields.Description.SanitizeCardDescription().JiraPlainTextToLeanKitHtml() !=
					    updatedCard.Description)
					{
						var updatedDescription = updatedCard.Description.LeanKitHtmlToJiraPlainText();
						updateJson += ", \"description\": \"" + updatedDescription + "\"";
						isDirty = true;
					}

					if (updatedItems.Contains("Priority"))
					{
						updateJson += ", \"priority\": { \"name\": \"" + GetPriority(updatedCard.Priority) + "\"}";
						isDirty = true;
					}

					if (updatedItems.Contains("DueDate") && CurrentUser != null)
					{
						try
						{
							var dateFormat = CurrentUser.DateFormat ?? "MM/dd/yyyy";
							var parsed = DateTime.ParseExact(updatedCard.DueDate, dateFormat,
								CultureInfo.InvariantCulture);

							updateJson += ", \"duedate\": \"" + parsed.ToString("o") + "\"";
						}
						catch (Exception ex)
						{
							Log.Warn(ex, "Could not parse due date: {0}", updatedCard.DueDate);
						}
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

							updateLabels += "\"" + newLabel.Trim() + "\"";

							ctr++;
						}
						updateJson += ", \"labels\": [" + updateLabels + "]";
						isDirty = true;
					}

					string comment = "";
					if (updatedItems.Contains("Size"))
					{
						comment += "LeanKit card Size changed to " + updatedCard.Size + ". ";
					}
					if (updatedItems.Contains("Blocked"))
					{
						if (updatedCard.IsBlocked)
							comment += "LeanKit card is blocked: " + updatedCard.BlockReason + ". ";
						else
							comment += "LeanKit card is no longer blocked: " + updatedCard.BlockReason + ". ";
					}

					updateJson += "}}";

					if (isDirty)
					{
						try
						{
							//https://yoursite.atlassian.net/rest/api/latest/issue/{issueIdOrKey}
							var updateRequest =
								CreateRequest(string.Format("rest/api/latest/issue/{0}", updatedCard.ExternalCardID),
									Method.PUT);
							updateRequest.AddParameter("application/json", updateJson, ParameterType.RequestBody);

							var resp = ExecuteRequest(updateRequest);

							if (resp.StatusCode != HttpStatusCode.OK && resp.StatusCode != HttpStatusCode.NoContent)
							{
								ProcessJiraError(resp, string.Format("Unable to update JIRA Issue [{0}].", updatedCard.ExternalCardID));
							}
							else
							{
								Log.Debug(string.Format("Updated Issue [{0}]", updatedCard.ExternalCardID));
								CacheCardVersion(updatedCard.Id, false, updatedCard.Version);
							}
						}
						catch (Exception ex)
						{
							Log.Error(string.Format("Unable to update Issue [{0}], Exception: {1}",
								updatedCard.ExternalCardID, ex.Message));
						}
					}

					if (!string.IsNullOrEmpty(comment))
					{
						try
						{
							//https://yoursite.atlassian.net/rest/api/latest/issue/{issueIdOrKey}
							var updateRequest =
								CreateRequest(
									string.Format("rest/api/latest/issue/{0}/comment", updatedCard.ExternalCardID),
									Method.POST);
							updateRequest.AddParameter(
								"application/json",
								"{ \"body\": \"" + comment + "\"}",
								ParameterType.RequestBody);

							var resp = ExecuteRequest(updateRequest);

							if (resp.StatusCode != HttpStatusCode.OK &&
							    resp.StatusCode != HttpStatusCode.NoContent &&
							    resp.StatusCode != HttpStatusCode.Created)
							{
								ProcessJiraError(resp, string.Format("Unable to create comment for updated Issue [{0}].", updatedCard.ExternalCardID));
							}
							else
							{
								Log.Debug(string.Format("Created comment for updated Issue [{0}]", updatedCard.ExternalCardID));
								CacheCardVersion(updatedCard.Id, false, updatedCard.Version);
							}
						}
						catch (Exception ex)
						{
							Log.Error(string.Format("Unable to create comment for updated Issue [{0}], Exception: {1}",
								updatedCard.ExternalCardID, ex.Message));
						}
					}
				}
			}
		}

		private void IssueUpdated(Issue issue, Card card, BoardMapping boardMapping)
		{
			Log.Info("Issue [{0}] updated, comparing to corresponding card...", issue.Key);

			long boardId = boardMapping.Identity.LeanKit;

			// sync and save those items that are different (of title, description, priority)
			bool saveCard = false;

			if (issue.Fields != null)
			{
				if (issue.Fields.Summary != null && issue.Fields.Summary != card.Title)
				{
					card.Title = issue.Fields.Summary;
					saveCard = true;
				}

				if (issue.Fields.Description != null &&
					issue.Fields.Description.SanitizeCardDescription().JiraPlainTextToLeanKitHtml() != card.Description)
				{
					card.Description = issue.Fields.Description.SanitizeCardDescription().JiraPlainTextToLeanKitHtml();
					saveCard = true;
				}

				var priority = issue.LeanKitPriority();
				if (priority != card.Priority)
				{
					card.Priority = priority;
					saveCard = true;
				}

				if (issue.Fields.Labels != null && issue.Fields.Labels.Count > 0)
				{
					var tags = string.Join(",", issue.Fields.Labels.Select(x => x));
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

				if (issue.Fields.DueDate != null && CurrentUser != null)
				{
					var dateFormat = CurrentUser.DateFormat ?? "MM/dd/yyyy";
					var dueDateString = issue.Fields.DueDate.Value.ToString(dateFormat, CultureInfo.InvariantCulture);
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
			}

			if (saveCard)
			{
				Log.Info("Updating card [{0}]", card.Id);
				var result = LeanKit.UpdateCard(boardId, card);
				CacheCardVersion(result.CardDTO.Id, false, result.CardDTO.Version);
				TargetSetCacheVersion(issue.Key, issue.Fields.Updated);
			}

			// check the state of the work item
			// if we have the state mapped to a lane then check to see if the card is in that lane
			// if it is not in that lane then move it to that lane
			if (!boardMapping.UpdateCardLanes || issue.Fields == null || issue.Fields.Status == null ||
			    string.IsNullOrEmpty(issue.Fields.Status.Name)) return;

			var laneIds = boardMapping.LanesFromState(issue.Fields.Status.Name);
			if (!laneIds.Any()) return;
			if (laneIds.Contains(card.LaneId))
			{
				Log.Debug("Card [{0}] is already in mapped Lane [{1}]", card.Id, card.LaneId);
				TargetSetCacheVersion(issue.Key, issue.Fields.Updated);
				return;
			}
			// first let's see if any of the lanes are sibling lanes, if so then
			// we should be using one of them. So we'll limit the results to just siblings
			if (boardMapping.ValidLanes != null)
			{
				var siblingLaneIds = (from siblingLaneId in laneIds
					let parentLane =
						boardMapping.ValidLanes.FirstOrDefault(x =>
							x.HasChildLanes &&
							x.ChildLaneIds.Contains(siblingLaneId) &&
							x.ChildLaneIds.Contains(card.LaneId))
					where parentLane != null
					select siblingLaneId).ToList();
				if (siblingLaneIds.Any())
					laneIds = siblingLaneIds;
			}
			var laneId = laneIds.First();
			Log.Info("Moving card [{0}] to Lane [{1}]", card.Id, laneId);
			LeanKit.MoveCard(boardMapping.Identity.LeanKit, card.Id, laneId, 0, "Moved Lane From Jira Issue");
			var updatedCard = LeanKit.GetCard(boardMapping.Identity.LeanKit, card.Id);
			CacheCardVersion(updatedCard.Id, true, updatedCard.Version);
			TargetSetCacheVersion(issue.Key, issue.Fields.Updated);
		}

		protected override void Synchronize(BoardMapping project)
		{
			Log.Debug("Polling Jira for Issues");

			var queryAsOfDate = QueryDate.AddMilliseconds(Configuration.PollingFrequency*-1.5);
			//var queryAsOfDate = QueryDate.AddMinutes(-5);

			string jqlQuery;
			var formattedQueryDate = queryAsOfDate.ToString(QueryDateFormat, CultureInfo.InvariantCulture);
			if (!string.IsNullOrEmpty(project.Query))
			{
				jqlQuery = string.Format(project.Query, formattedQueryDate);
			}
			else
			{
				var queryFilter = string.Format(" and ({0})",
					string.Join(" or ", project.QueryStates.Select(x => "status = '" + x.Trim() + "'").ToList()));
				if (!string.IsNullOrEmpty(project.ExcludedTypeQuery))
				{
					queryFilter += project.ExcludedTypeQuery;
				}
				jqlQuery = string.Format("project=\"{0}\" {1} and updated > \"{2}\" order by updated asc",
					project.Identity.Target, queryFilter, formattedQueryDate);
			}

			//https://yoursite.atlassian.net/rest/api/latest/search?jql=project=%22More+Tests%22+and+status=%22open%22+and+created+%3E+%222008/12/31+12:00%22+order+by+created+asc&fields=id,status,priority,summary,description
			var request = CreateRequest("rest/api/latest/search", Method.GET);

			request.AddParameter("jql", jqlQuery);
			request.AddParameter("fields",
				"id,status,priority,summary,description,issuetype,type,assignee,duedate,labels,updated");
			request.AddParameter("maxResults", "9999");

			var jiraResp = ExecuteRequest(request);

			if (jiraResp.StatusCode != HttpStatusCode.OK)
			{
				ProcessJiraError(jiraResp, "Unable to get issues from Jira.");
				return;
			}

			var resp = new JsonSerializer<IssuesResponse>().DeserializeFromString(jiraResp.Content);

			Log.Info("Queried [{0}] at {1} for changes after {2}", project.Identity.Target, QueryDate,
				queryAsOfDate.ToString("o"));

			if (resp != null && resp.Issues != null && resp.Issues.Any())
			{
				var issues = resp.Issues;
				Log.Info("{0} item(s) queried.", issues.Count);
				foreach (var issue in issues)
				{
					if (TargetCacheCheckForVersion(issue.Key, issue.Fields.Updated))
					{
						Log.Info("Issue [{0}] already processed, skipping.", issue.Key);
						continue;
					}
					Log.Info("Issue [{0}]: {1}, {2}, {3}", issue.Key, issue.Fields.Summary, issue.Fields.Status.Name,
						issue.Fields.Priority.Name);

					// does this workitem have a corresponding card?
					var card = LeanKit.GetCardByExternalId(project.Identity.LeanKit, issue.Key);

					if (card == null || !card.ExternalSystemName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase))
					{
						Log.Debug("Create new card for Issue [{0}]", issue.Key);
						CreateCardFromItem(project, issue);
					}
					else
					{
						Log.Debug("Previously created a card for Issue [{0}]", issue.Key);
						if (project.UpdateCards)
							IssueUpdated(issue, card, project);
						else
							Log.Debug("Skipped card update because 'UpdateCards' is disabled.");
					}
				}
			}
		}

		private void ProcessJiraError(IRestResponse response, string errMessage)
		{
			try
			{
				var serializer = new JsonSerializer<ErrorMessage>();
				var errorMessage = serializer.DeserializeFromString(response.Content);
				var err = string.Format(" Status: {0}, Message: {1}", response.StatusDescription, errorMessage.Message);
				Log.Error( errMessage + err );
			}
			catch (Exception)
			{
				var err = string.Format(" Status: {0}, Message: {1}", response.StatusDescription, response.Content);
				Log.Error(errMessage + err);
				ClearSessionCookies();
			}
		}

		private void CreateCardFromItem(BoardMapping project, Issue issue)
		{
			if (issue == null) return;
			if (!project.CreateCards)
			{
				Log.Debug("CreateCards is disabled, skipping card creation.");
				return;
			}

			var boardId = project.Identity.LeanKit;

			var mappedCardType = issue.LeanKitCardType(project);

			var validLanes = project.LanesFromState(issue.Fields.Status.Name);
			var laneId = validLanes.Any() ? validLanes.First() : project.DefaultCardCreationLaneId;

			var card = new Card
			{
				Active = true,
				Title = issue.Fields.Summary,
				Description = issue.Fields.Description.SanitizeCardDescription().JiraPlainTextToLeanKitHtml(),
				Priority = issue.LeanKitPriority(),
				TypeId = mappedCardType.Id,
				TypeName = mappedCardType.Name,
				LaneId = laneId,
				ExternalCardID = issue.Key,
				ExternalSystemName = ServiceName,
				ExternalSystemUrl = string.Format(_externalUrlTemplate, issue.Key)
			};

			var assignedUserId = issue.LeanKitAssignedUserId(boardId, LeanKit);
			if (assignedUserId != null)
				card.AssignedUserIds = new[] {assignedUserId.Value};

			if (issue.Fields != null && issue.Fields.DueDate != null && CurrentUser != null)
			{
				var dateFormat = CurrentUser.DateFormat ?? "MM/dd/yyyy";
				card.DueDate = issue.Fields.DueDate.Value.ToString(dateFormat, CultureInfo.InvariantCulture);
			}

			if (issue.Fields != null && issue.Fields.Labels != null && issue.Fields.Labels.Any())
			{
				card.Tags = string.Join(",", issue.Fields.Labels);
			}

			if ((card.Tags == null || !card.Tags.Contains(ServiceName)) && project.TagCardsWithTargetSystemName)
			{
				if (string.IsNullOrEmpty(card.Tags))
					card.Tags = ServiceName;
				else
					card.Tags += "," + ServiceName;
			}

			// TODO: Add size from the custom story points field.

			Log.Debug("Creating a card of type [{0}] for issue [{1}] on Board [{2}] on Lane [{3}]", mappedCardType.Name,
				issue.Key, boardId, laneId);

			CardAddResult cardAddResult = null;

			var tries = 0;
			var success = false;
			while (tries < 3 && !success)
			{
				if (tries > 0)
				{
					Log.Error(string.Format("Attempting to create card for work item [{0}] attempt number [{1}]",
						issue.Key, tries));
					// wait 5 seconds before trying again
					Thread.Sleep(new TimeSpan(0, 0, 5));
				}

				try
				{
					cardAddResult = LeanKit.AddCard(boardId, card, "New Card From Jira Issue");
					success = true;
					CacheCardVersion(cardAddResult.CardId, false, 1);
					CacheCardVersion(cardAddResult.CardId, true, 1);
					TargetSetCacheVersion(issue.Key, issue.Fields.Updated);
				}
				catch (Exception ex)
				{
					Log.Error(string.Format("An error occurred: {0} - {1} - {2}", ex.GetType(), ex.Message,
						ex.StackTrace));
				}
				tries++;
			}
			card.Id = cardAddResult.CardId;

			Log.Info("Created a card [{0}] of type [{1}] for work item [{2}] on Board [{3}] on Lane [{4}]", card.Id,
				mappedCardType.Name, issue.Key, boardId, laneId);
		}

		public string GetPriority(int priority)
		{
			var priorities = Priorities;
			if (priorities != null && priorities.Count > 0)
			{
				// Highest, High, Medium, Low, Lowest
				switch (priority)
				{
					case 3:
						var p3 =
							priorities.Find(
								x =>
									x.Name.Equals("critical", StringComparison.OrdinalIgnoreCase) ||
									x.Name.Equals("highest", StringComparison.OrdinalIgnoreCase));
						return (p3 != null) ? p3.Name : priorities[0].Name;
					case 2:
						var p2 =
							priorities.Find(
								x =>
									x.Name.Equals("major", StringComparison.OrdinalIgnoreCase) ||
									x.Name.Equals("high", StringComparison.OrdinalIgnoreCase));
						return (p2 != null) ? p2.Name : (priorities.Count > 1) ? priorities[1].Name : priorities[0].Name;
					case 0:
						var p0 =
							priorities.Find(
								x =>
									x.Name.Equals("trivial", StringComparison.OrdinalIgnoreCase) ||
									x.Name.Equals("low", StringComparison.OrdinalIgnoreCase));
						return (p0 != null)
							? p0.Name
							: (priorities.Count > 3) ? priorities[3].Name : priorities[priorities.Count - 1].Name;
					default:
						var p1 =
							priorities.Find(
								x =>
									x.Name.Equals("medium", StringComparison.OrdinalIgnoreCase) ||
									x.Name.Equals("minor", StringComparison.OrdinalIgnoreCase));
						return (p1 != null)
							? p1.Name
							: (priorities.Count > 2) ? priorities[2].Name : priorities[priorities.Count - 1].Name;
				}
			}
			switch (priority)
			{
				case 3:
					return "Critical";
				case 2:
					return "Major";
				case 0:
					return "Trivial";
				case 1:
				default:
					return "Minor";
			}
		}

		protected override void UpdateStateOfExternalItem(Card card, List<string> states, BoardMapping boardMapping)
		{
			UpdateStateOfExternalItem(card, states, boardMapping, false);
		}

		protected void UpdateStateOfExternalItem(Card card, List<string> states, BoardMapping mapping, bool runOnlyOnce)
		{
			if (!mapping.UpdateTargetItems) return;

			if (string.IsNullOrEmpty(card.ExternalSystemName) ||
			    !card.ExternalSystemName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase))
				return;

			if (string.IsNullOrEmpty(card.ExternalCardID))
			{
				Log.Debug("Ignoring card [{0}] with missing external id value.", card.Id);
				return;
			}

			var version = GetCachedCardVersion(card.Id, true);
			if (version >= card.Version)
			{
				Log.Debug("UpdateStateOfExternalItem, Card [{0}] with version [{1}] has already been processed. Skipping comparison.", card.Id, card.Version);
				return;
			}


			if (states == null || states.Count == 0)
				return;

			var tries = 0;
			var success = false;
			while (tries < 3 && !success && (!runOnlyOnce || tries == 0))
			{
				if (tries > 0)
				{
					Log.Warn(string.Format("Attempting to update external work item [{0}] attempt number [{1}]",
						card.ExternalCardID,
						tries));
					// wait 5 seconds before trying again
					Thread.Sleep(new TimeSpan(0, 0, 5));
				}

				//https://yoursite.atlassian.net/rest/api/latest/issue/{issueIdOrKey}
				var request = CreateRequest(string.Format("rest/api/latest/issue/{0}", card.ExternalCardID), Method.GET);
				var jiraResp = ExecuteRequest(request);

				if (jiraResp.StatusCode != HttpStatusCode.OK)
				{
					ProcessJiraError(jiraResp, string.Format("Unable to get issue [{0}] from Jira.", card.ExternalCardID));
				}
				else
				{
					var issueToUpdate = new JsonSerializer<Issue>().DeserializeFromString(jiraResp.Content);

					// Check for a workflow mapping to the closed state
					if (states != null && states.Count > 0 && states[0].Contains(">"))
					{
						var workflowStates = states[0].Split('>');

						// check to see if the workitem is already in one of the workflow states
						var alreadyInState =
							workflowStates.FirstOrDefault(
								x => x.Trim().ToLowerInvariant() == issueToUpdate.Fields.Status.Name.ToLowerInvariant());
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
								UpdateStateOfExternalItem(card, new List<string> {workflowState.Trim()}, mapping,
									runOnlyOnce);
							}
							return;
						}
					}

					foreach (var state in states)
					{
						if (issueToUpdate.Fields.Status.Name.ToLowerInvariant() == state.ToLowerInvariant())
						{
							Log.Debug(string.Format("Issue [{0}] is already in state [{1}]", issueToUpdate.Key, state));
							return;
						}
					}

					try
					{
						// first get a list of available transitions
						var transitionsRequest =
							CreateRequest(
								string.Format("rest/api/2/issue/{0}/transitions?expand=transitions.fields",
									card.ExternalCardID), Method.GET);
						var transitionsResponse = ExecuteRequest(transitionsRequest);

						if (transitionsResponse.StatusCode != HttpStatusCode.OK)
						{
							ProcessJiraError(jiraResp, "Unable to get available transitions from Jira.");
						}
						else
						{
							var availableTransitions =
								new JsonSerializer<TransitionsResponse>().DeserializeFromString(
									transitionsResponse.Content);

							if (availableTransitions != null &&
							    availableTransitions.Transitions != null &&
							    availableTransitions.Transitions.Any())
							{
								// now find match from available transitions to states
								var valid = false;
								Transition validTransition = null;
								foreach (var st in states)
								{
									validTransition = availableTransitions.Transitions.FirstOrDefault(
										x =>
											x.Name.ToLowerInvariant() == st.ToLowerInvariant() ||
											x.To.Name.ToLowerInvariant() == st.ToLowerInvariant());
									if (validTransition != null)
									{
										// if you find one then set it
										valid = true;
										break;
									}
								}

								if (!valid)
								{
									// if not then write an error message
									Log.Error(
										string.Format(
											"Unable to update Issue [{0}] to [{1}] because the status transition is invalid. Try adding additional states to the config.",
											card.ExternalCardID, states.Join(",")));
								}
								else
								{
									// go ahead and try to update the state of the issue in JIRA
									//https://yoursite.atlassian.net/rest/api/latest/issue/{issueIdOrKey}/transitions?expand=transitions.fields
									var updateRequest =
										CreateRequest(
											string.Format(
												"rest/api/latest/issue/{0}/transitions?expand=transitions.fields",
												card.ExternalCardID), Method.POST);
									updateRequest.AddParameter("application/json",
										"{ \"transition\": { \"id\": \"" + validTransition.Id + "\"}}",
										ParameterType.RequestBody);
									var resp = ExecuteRequest(updateRequest);

									if (resp.StatusCode != HttpStatusCode.OK &&
									    resp.StatusCode != HttpStatusCode.NoContent)
									{
										ProcessJiraError(jiraResp, string.Format("Unable to update Issue [{0}] to [{1}].", card.ExternalCardID, validTransition.To.Name));
									}
									else
									{
										success = true;
										Log.Debug(string.Format("Updated state for Issue [{0}] to [{1}]",
											card.ExternalCardID, validTransition.To.Name));
										CacheCardVersion(card.Id, true, card.Version);
									}
								}
							}
							else
							{
								Log.Error(
									string.Format(
										"Unable to update Issue [{0}] to [{1}] because no transitions were available from its current status [{2}]. The user account you are using to connect may not have proper privileges.",
										card.ExternalCardID, states.Join(","), issueToUpdate.Fields.Status.Name));
							}
						}
					}
					catch (Exception ex)
					{
						Log.Error(string.Format("Unable to update Issue [{0}] to [{1}], Exception: {2}",
							card.ExternalCardID, states.Join(","), ex.Message));
					}
				}
				tries++;
			}
		}

		protected override void CreateNewItem(Card card, BoardMapping boardMapping)
		{
			var jiraIssueType = GetJiraIssueType(boardMapping, card.TypeId);

			string json = "{ \"fields\": { ";
			json += "\"project\":  { \"key\": \"" + boardMapping.Identity.Target + "\" }";
			json += ", \"summary\": \"" + card.Title.Replace("\"", "\\\"") + "\" ";
			json += ", \"description\": \"" + card.Description.LeanKitHtmlToJiraPlainText() + "\" ";
			json += ", \"issuetype\": { \"name\": \"" + jiraIssueType + "\" }";
			json += ", \"priority\": { \"name\": \"" + GetPriority(card.Priority) + "\" }";

			if (jiraIssueType.ToLowerInvariant() == "epic")
			{
				if (CustomFields.Any())
				{
					var epicNameField = CustomFields.FirstOrDefault(x => x.Name == "Epic Name");
					if (epicNameField != null)
					{
						json += ", \"" + epicNameField.Id + "\": \"" + card.Title.Replace("\"", "\\\"") + "\"";
					}
				}
			}

			if (!string.IsNullOrEmpty(card.DueDate) && CurrentUser != null)
			{
				try
				{
					var dateFormat = CurrentUser.DateFormat ?? "MM/dd/yyyy";
					var parsed = DateTime.ParseExact(card.DueDate, dateFormat, CultureInfo.InvariantCulture);

					json += ", \"duedate\": \"" + parsed.ToString("o") + "\"";
				}
				catch (Exception ex)
				{
					Log.Warn(ex, "Could not parse due date: {0}", card.DueDate);
				}
			}

			if (!string.IsNullOrEmpty(card.Tags))
			{
				var newLabels = card.Tags.Split(',');
				string updateLabels = "";
				int ctr = 0;
				foreach (string newLabel in newLabels)
				{
					if (ctr > 0)
						updateLabels += ", ";

					updateLabels += "\"" + newLabel.Trim() + "\"";

					ctr++;
				}
				json += ", \"labels\": [" + updateLabels + "]";
			}

			json += "}}";

			Issue newIssue = null;
			try
			{
				//https://yoursite.atlassian.net/rest/api/latest/issue
				var createRequest = CreateRequest("rest/api/latest/issue", Method.POST);
				createRequest.AddParameter("application/json", json, ParameterType.RequestBody);
				var resp = ExecuteRequest(createRequest);

				if (resp.StatusCode != HttpStatusCode.OK && resp.StatusCode != HttpStatusCode.Created)
				{
					ProcessJiraError(resp, string.Format("Unable to create Issue from card [{0}].", card.ExternalCardID));
				}
				else
				{
					newIssue = new JsonSerializer<Issue>().DeserializeFromString(resp.Content);
					Log.Debug(string.Format("Created Issue [{0}]", newIssue.Key));
				}
			}
			catch (Exception ex)
			{
				Log.Error(string.Format("Unable to create Issue from Card [{0}], Exception: {1}", card.ExternalCardID,
					ex.Message));
			}

			if (newIssue != null)
			{
				try
				{
					card.ExternalCardID = newIssue.Key;
					card.ExternalSystemName = ServiceName;
					card.ExternalSystemUrl = string.Format(_externalUrlTemplate, newIssue.Key);

					// now that we've created the work item let's try to set it to any matching state defined by lane
					var states = boardMapping.LaneToStatesMap[card.LaneId];
					if (states != null)
					{
						UpdateStateOfExternalItem(card, states, boardMapping, true);
					}

					var result = LeanKit.UpdateCard(boardMapping.Identity.LeanKit, card);
					CacheCardVersion(card.Id, false, result.CardDTO.Version);
					CacheCardVersion(card.Id, true, result.CardDTO.Version);
				}
				catch (Exception ex)
				{
					Log.Error(string.Format("Error updating Card [{0}] after creating new Issue, Exception: {1}",
						card.ExternalCardID,
						ex.Message));
				}
			}
		}

		private string GetJiraIssueType(BoardMapping boardMapping, long cardTypeId)
		{
			const string defaultIssueType = "Bug";
			if (cardTypeId <= 0
			    || boardMapping == null
			    || boardMapping.Types == null
			    || boardMapping.ValidCardTypes == null
			    || !boardMapping.ValidCardTypes.Any()
			    || !boardMapping.Types.Any())
				return defaultIssueType;

			var lkType = boardMapping.ValidCardTypes.FirstOrDefault(x => x.Id == cardTypeId);
			if (lkType == null) return defaultIssueType;
			var mappedType =
				boardMapping.Types.FirstOrDefault(
					x =>
						x != null && !string.IsNullOrEmpty(x.LeanKit) &&
						String.Equals(x.LeanKit, lkType.Name, StringComparison.OrdinalIgnoreCase));
			return mappedType != null ? mappedType.Target : "Bug";
		}

		#region object model

		public class IssuesResponse
		{
			public List<Issue> Issues { get; set; }

			public IssuesResponse()
			{
				Issues = new List<Issue>();
			}
		}

		public class TransitionsResponse
		{
			public List<Transition> Transitions { get; set; }

			public TransitionsResponse()
			{
				Transitions = new List<Transition>();
			}
		}

		public class Issue
		{
			public long Id { get; set; }
			public string Key { get; set; }
			public Fields Fields { get; set; }

			public Issue()
			{
				Id = 0;
				Key = "";
				Fields = new Fields();
			}
		}

		public class Fields
		{
			public string Summary { get; set; }
			public IssueType IssueType { get; set; }
			public string Created { get; set; }
			public string Updated { get; set; }
			public string Description { get; set; }
			public Priority Priority { get; set; }
			public DateTime? DueDate { get; set; }
			public Status Status { get; set; }
			public Author Assignee { get; set; }
			public List<string> Labels { get; set; }

			public Fields()
			{
				Summary = "";
				IssueType = new IssueType();
				Created = "";
				Updated = "";
				Description = "";
				Priority = new Priority();
				Status = new Status();
				Labels = new List<string>();
				Assignee = new Author();
			}
		}

		public class Author
		{
			public string Name { get; set; }
			public string EmailAddress { get; set; }
			public string DisplayName { get; set; }

			public Author()
			{
				Name = "";
				EmailAddress = "";
				DisplayName = "";
			}
		}

		public class IssueType
		{
			public string Id { get; set; }
			public string Description { get; set; }
			public string Name { get; set; }

			public IssueType()
			{
				Name = "";
				Id = "";
				Description = "";
			}
		}

		public class Priority
		{
			public string Description { get; set; }
			public string Name { get; set; }
			public string Id { get; set; }

			public Priority()
			{
				Name = "";
				Id = "";
				Description = "";
			}
		}

		public class Status
		{
			public string Description { get; set; }
			public string Name { get; set; }
			public string Id { get; set; }

			public Status()
			{
				Description = "";
				Name = "";
				Id = "";
			}
		}

		public class Transition
		{
			public string Id { get; set; }
			public string Name { get; set; }
			public Status To { get; set; }

			public Transition()
			{
				Id = "";
				Name = "";
				To = new Status();
			}
		}

		public class Field
		{
			public string Id { get; set; }
			public string Name { get; set; }
			public bool Custom { get; set; }
		}

		public class ErrorMessage
		{
			public string Message { get; set; }
		}

		#endregion
	}
}