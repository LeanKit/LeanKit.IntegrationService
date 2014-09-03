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
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace IntegrationService.Targets.TFS
{
    public class Tfs : TargetBase
    {

        private Uri _projectCollectionUri;
        private ICredentials _projectCollectionNetworkCredentials;
        private TfsTeamProjectCollection _projectCollection;
	    private TswaClientHyperlinkService _projectHyperlinkService;
        private WorkItemStore _projectCollectionWorkItemStore;
	    private BasicAuthCredential _basicAuthCredential;
	    private TfsClientCredentials _tfsClientCredentials;
        private List<Microsoft.TeamFoundation.Server.Identity> _tfsUsers;
	    private const string ServiceName = "TFS";

	    public Tfs(IBoardSubscriptionManager subscriptions) : base(subscriptions) { }

	    public Tfs(IBoardSubscriptionManager subscriptions,
	               IConfigurationProvider<Configuration> configurationProvider,
	               ILocalStorage<AppSettings> localStorage,
	               ILeanKitClientFactory leanKitClientFactory,
	               ICredentials projectCollectionNetworkCredentials,
	               BasicAuthCredential basicAuthCredential,
	               TfsClientCredentials tfsClientCredentials,
	               TfsTeamProjectCollection projectCollection,
				   TswaClientHyperlinkService projectHyperlinkService,
	               WorkItemStore projectCollectionWorkItemStore, 
                   List<Microsoft.TeamFoundation.Server.Identity> tfsUsers)
		    : base(subscriptions, configurationProvider, localStorage, leanKitClientFactory)
	    {
		    _projectCollectionNetworkCredentials = projectCollectionNetworkCredentials;
		    _projectCollection = projectCollection;
		    _projectHyperlinkService = projectHyperlinkService;
		    _projectCollectionWorkItemStore = projectCollectionWorkItemStore;
		    _basicAuthCredential = basicAuthCredential;
		    _tfsClientCredentials = tfsClientCredentials;
	        _tfsUsers = tfsUsers;
	    }

	    protected override void Synchronize(BoardMapping project)
        {
            Log.Debug("Polling TFS [{0}] for Work Items", project.Identity.TargetName);

			//query a project for new items   
	        var stateQuery = string.Format(" AND ({0})", String.Join(" or ", project.QueryStates.Select(x => "[System.State] = '" + x.Trim() + "'").ToList()));
	        var iterationQuery = "";
			if (!string.IsNullOrEmpty(project.IterationPath))
	        {
		        iterationQuery = string.Format(" AND [System.IterationPath] UNDER '{0}' ", project.IterationPath);
	        }

	        var queryAsOfDate = QueryDate.AddMilliseconds(Configuration.PollingFrequency*-1.5).ToString("o");

			string tfsQuery;
			if (!string.IsNullOrEmpty(project.Query)) 
			{
				tfsQuery = string.Format(project.Query, queryAsOfDate);
			} 
			else 
			{
				tfsQuery = String.Format(
					"[System.TeamProject] = '{0}' {1} {2} {3} and [System.ChangedDate] > '{4}'",
					 project.Identity.TargetName, iterationQuery, stateQuery, project.ExcludedTypeQuery, queryAsOfDate);
			}

		    var queryStr = string.Format("SELECT [System.Id], [System.WorkItemType]," +
		                                    " [System.State], [System.AssignedTo], [System.Title], [System.Description]" +
		                                    " FROM WorkItems " +
		                                    " WHERE {0}" +
		                                    " ORDER BY [System.TeamProject]", tfsQuery);
		    if (_projectCollectionWorkItemStore == null)
		    {
			    "Reconnecting to TFS...".Info();
				Init();
		    }
		    Query query;
		    try
		    {
			    query = new Query(_projectCollectionWorkItemStore, queryStr, null, false);
		    }
		    catch (Exception ex)
		    {
			    Log.Error("Error creating TFS query. {0} ", ex.Message);
				if (_projectCollectionWorkItemStore == null) Log.Error("Project Collection Work Item Store is null");
			    throw;
		    }
		    var cancelableAsyncResult = query.BeginQuery();

            var changedItems = query.EndQuery(cancelableAsyncResult);

			Log.Info("\nQuery [{0}] for changes after {1}", project.Identity.Target, queryAsOfDate);
			Log.Debug(queryStr);

		    foreach (WorkItem item in changedItems)
            {
                Log.Info("Work Item [{0}]: {1}, {2}, {3}",
                                  item.Id, item.Title, item.Fields["System.AssignedTo"].Value, item.State);

                // does this workitem have a corresponding card?
                var card = LeanKit.GetCardByExternalId(project.Identity.LeanKit, item.Id.ToString(CultureInfo.InvariantCulture));

                if (card == null || !card.ExternalSystemName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Debug("Creating new card for work item [{0}]", item.Id);
                    CreateCardFromWorkItem(project, item);
                }
                // TODO: else if Lane = defined end lane then update it in TFS (i.e. we missed the event)
                // call UpdateStateOfExternalWorkItem()
                else
                {
                    Log.Info("Previously created a card for work item[{0}]", item.Id);
                    if (project.UpdateCards)
                        WorkItemUpdated(item, card, project);
                    else
                        Log.Info("Skipped card update because 'UpdateCards' is disabled.");
                }
            }
            Log.Info("{0} item(s) queried.\n", changedItems.Count);            
        }


        private void CreateCardFromWorkItem(BoardMapping project, WorkItem workItem)
        {
            if (workItem == null) return;

            var boardId = project.Identity.LeanKit;

            var mappedCardType = workItem.LeanKitCardType(project);

            var laneId = project.LanesFromState(workItem.State).First();
            var card = new Card
                {
                    Active = true,
                    Title = workItem.Title,
                    Description = workItem.LeanKitDescription(GetTfsVersion()),
                    Priority = workItem.LeanKitPriority(),
                    TypeId = mappedCardType.Id,
                    TypeName = mappedCardType.Name,
                    LaneId = laneId,
                    ExternalCardID = workItem.Id.ToString(CultureInfo.InvariantCulture),
                    ExternalSystemName = ServiceName
                };

			if (workItem.Fields.Contains("Tags") && workItem.Fields["Tags"] != null && workItem.Fields["Tags"].Value != null)
			{
				card.Tags = workItem.Fields["Tags"].Value.ToString().Replace(";", ",");
			}

            if (project.TagCardsWithTargetSystemName && (card.Tags == null || !card.Tags.Contains(ServiceName))) 
			{
				if (string.IsNullOrEmpty(card.Tags))
					card.Tags = ServiceName;
				else
					card.Tags += "," + ServiceName;
			}

			if (_projectHyperlinkService != null)
			{
				card.ExternalSystemUrl = _projectHyperlinkService.GetWorkItemEditorUrl(workItem.Id).ToString();
			}

	        if (workItem.Fields != null && workItem.Fields.Contains("Assigned To"))
	        {
		        if (workItem.Fields["Assigned To"] != null && workItem.Fields["Assigned To"].Value != null)
		        {
			        var assignedUserId = CalculateAssignedUserId(boardId, workItem.Fields["Assigned To"].Value.ToString());
			        if (assignedUserId != null)
				        card.AssignedUserIds = new[] {assignedUserId.Value};
		        }
	        }

			if (workItem.Fields != null && workItem.Fields.Contains("Due Date"))
			{
				if (workItem.Fields["Due Date"] != null && workItem.Fields["Due Date"].Value != null)
				{
					DateTime tfsDueDate;
					var isDate = DateTime.TryParse(workItem.Fields["Due Date"].Value.ToString(), out tfsDueDate);
					if (isDate)
					{
						if (CurrentUser != null)
						{
							var dateFormat = CurrentUser.DateFormat ?? "MM/dd/yyyy";
							card.DueDate = tfsDueDate.ToString(dateFormat);
						}
					}
				}
			}

			if (workItem.Fields != null && (workItem.Fields.Contains("Original Estimate") || workItem.Fields.Contains("Story Points")))
			{
				if (workItem.Fields.Contains("Original Estimate") && workItem.Fields["Original Estimate"] != null && workItem.Fields["Original Estimate"].Value != null)
				{
					double cardSize;
					var isNumber = Double.TryParse(workItem.Fields["Original Estimate"].Value.ToString(), out cardSize);
					if (isNumber)
						card.Size = (int)cardSize;
				}
				else if (workItem.Fields.Contains("Story Points") && workItem.Fields["Story Points"] != null && workItem.Fields["Story Points"].Value != null)
				{
					double cardSize;
					var isNumber = Double.TryParse(workItem.Fields["Story Points"].Value.ToString(), out cardSize);
					if (isNumber)
						card.Size = (int) cardSize;
				}
			}

	        Log.Info("Creating a card of type [{0}] for work item [{1}] on Board [{2}] on Lane [{3}]", mappedCardType.Name, workItem.Id, boardId, laneId);

	        CardAddResult cardAddResult = null;

	        var tries = 0;
	        var success = false;
	        while (tries < 10 && !success)
	        {
		        if (tries > 0)
		        {
			        Log.Warn(String.Format("Attempting to create card for work item [{0}] attempt number [{1}]", workItem.Id,
			                                 tries));
					// wait 5 seconds before trying again
					Thread.Sleep(new TimeSpan(0, 0, 5));
		        }

		        try
		        {
			        cardAddResult = LeanKit.AddCard(boardId, card, "New Card From TFS Work Item");
			        success = true;
		        }
		        catch (Exception ex)
		        {
					Log.Error(ex, string.Format("An error occurred creating a new card for work item [{0}]", workItem.Id));
		        }
		        tries++;
	        }
	        card.Id = cardAddResult.CardId;

            Log.Info("Created a card [{0}] of type [{1}] for work item [{2}] on Board [{3}] on Lane [{4}]", card.Id, mappedCardType.Name, workItem.Id, boardId, laneId);
        }


		public void SetWorkItemPriority(WorkItem workItem, int newPriority)
		{
			//LK Priority: 0 = Low, 1 = Normal, 2 = High, 3 = Critical
			//TFS Priority: 1-4

			if (!workItem.Fields.Contains("Priority")) return;

			var tfsValue = 4 - newPriority;
			if (tfsValue < 1) tfsValue = 1;
			workItem.Fields["Priority"].Value = tfsValue;
		}

	    public long? CalculateAssignedUserId(long boardId, string assignedTo)
		{
			if (!String.IsNullOrEmpty(assignedTo))
			{
				if (_tfsUsers != null && _tfsUsers.Any()) 
				{
					var user = _tfsUsers.FirstOrDefault(x => 
						x != null && 
						x.DisplayName != null && 
						!String.IsNullOrEmpty(x.DisplayName) && 
						x.DisplayName.ToLowerInvariant() == assignedTo.ToLowerInvariant());
					if (user != null) {
						var lkUser = LeanKit.GetBoard(boardId).BoardUsers.FirstOrDefault(x => x != null && 
							(((!String.IsNullOrEmpty(x.EmailAddress)) && (!String.IsNullOrEmpty(user.MailAddress)) && x.EmailAddress.ToLowerInvariant() == user.MailAddress.ToLowerInvariant()) ||										((!String.IsNullOrEmpty(x.FullName)) && (!String.IsNullOrEmpty(user.DisplayName)) && x.FullName.ToLowerInvariant() == user.DisplayName.ToLowerInvariant()) ||
                            ((!String.IsNullOrEmpty(x.FullName)) && (!string.IsNullOrEmpty(user.DisplayName)) && x.FullName.ToLowerInvariant() == user.DisplayName.ToLowerInvariant()) ||
							((!String.IsNullOrEmpty(x.UserName)) && (!String.IsNullOrEmpty(user.AccountName)) && x.UserName.ToLowerInvariant() == user.AccountName.ToLowerInvariant())));
						if (lkUser != null)
							return lkUser.Id;
					}					
				}
			}				
			return null;
		}

		private void SetAssignedUser(WorkItem workItem, long boardId, long userId)
		{
			try
			{
				var lkUser = LeanKit.GetBoard(boardId).BoardUsers.FirstOrDefault(x => x.Id == userId);
				if (lkUser != null)
				{
					var gss = (IGroupSecurityService)_projectCollection.GetService(typeof(IGroupSecurityService));
					var sids = gss.ReadIdentity(SearchFactor.AccountName, "Project Collection Valid Users", QueryMembership.Expanded);
					var users = gss.ReadIdentities(SearchFactor.Sid, sids.Members, QueryMembership.None);
					var tfsUser = users.FirstOrDefault(x =>
						((!string.IsNullOrEmpty(lkUser.EmailAddress)) && (!string.IsNullOrEmpty(x.MailAddress)) && lkUser.EmailAddress.ToLowerInvariant() == x.MailAddress.ToLowerInvariant()) ||
						((!string.IsNullOrEmpty(lkUser.FullName)) && (!string.IsNullOrEmpty(x.DisplayName)) && lkUser.FullName.ToLowerInvariant() == x.DisplayName.ToLowerInvariant()) ||
						((!string.IsNullOrEmpty(lkUser.UserName)) && (!string.IsNullOrEmpty(x.AccountName)) && lkUser.UserName.ToLowerInvariant() == x.AccountName.ToLowerInvariant()));
					if (tfsUser != null && tfsUser.DisplayName != null)
					{
						workItem.Fields["System.AssignedTo"].Value = tfsUser.DisplayName;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error(string.Format("An error occurred: {0} - {1} - {2}", ex.GetType(), ex.Message, ex.StackTrace));
			}
		}

	    protected override void UpdateStateOfExternalItem(Card card, List<string> states, BoardMapping boardMapping)
	    {
			UpdateStateOfExternalItem(card, states, boardMapping, false);
	    }

        protected void UpdateStateOfExternalItem(Card card, List<string> states, BoardMapping mapping, bool runOnlyOnce)
		{
			if (!card.ExternalSystemName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase)) return;
			if (string.IsNullOrEmpty(card.ExternalCardID)) return;

			int workItemId;

			// use external card id to get the TFS work item
	        try
	        {
		        workItemId = Convert.ToInt32(card.ExternalCardID);
	        }
	        catch (Exception)
	        {
		        Log.Debug("Ignoring card [{0}] with missing external id value.", card.Id);
		        return;
	        }

	        if (states == null || states.Count == 0) return;

			var tries = 0;
			var success = false;
			while (tries < 10 && !success && (!runOnlyOnce || tries == 0))
			{
				if (tries > 0)
				{
					Log.Warn(String.Format("Attempting to update external work item [{0}] attempt number [{1}]", workItemId, tries));
					// wait 5 seconds before trying again
					Thread.Sleep(new TimeSpan(0, 0, 5));
				}

				Log.Debug("Attempting to retrieve work item [{0}]", workItemId);
				var workItemToUpdate = _projectCollectionWorkItemStore.GetWorkItem(workItemId);
				if (workItemToUpdate != null)
				{
					var initialState = workItemToUpdate.State;

					// iterate through the configured states until we find the one that works. 
					// Alternately we could do something with the validation results and check AllowedStates
					// may be able to figure out the issue and handle it
					// see http://bartwullems.blogspot.com/2012/04/tf237124-work-item-is-not-ready-to-save.html
					// according to docs the result should be a collection of Microsoft.TeamFoundation.WorkItemTracking.Client, not sure why it is an ArrayList
				    var ctr = 0;
					var valid = false;
					foreach (var st in states)
					{
						if (ctr > 0)
							workItemToUpdate = _projectCollectionWorkItemStore.GetWorkItem(workItemId);

						var attemptState = st;
						// Check for a workflow mapping to the closed state
						if (attemptState.Contains(">"))
						{
							var workflowStates = attemptState.Split('>');

							// check to see if the workitem is already in one of the workflow states
							var alreadyInState = workflowStates.FirstOrDefault(x => x.Trim().ToLowerInvariant() == workItemToUpdate.State.ToLowerInvariant());
							if (!string.IsNullOrEmpty(alreadyInState))
							{
								// change workflowStates to only use the states after the currently set state
								var currentIndex = Array.IndexOf(workflowStates, alreadyInState);
								if (currentIndex < workflowStates.Length - 1)
								{
									var updatedWorkflowStates = new List<string>();
									for (var i = currentIndex + 1; i < workflowStates.Length; i++)
									{
										updatedWorkflowStates.Add(workflowStates[i]);
									}
									workflowStates = updatedWorkflowStates.ToArray();
								}
							}
                            //                                        UpdateStateOfExternalItem(card, new List<string>() { workflowState.Trim() }, mapping, runOnlyOnce);

							if (workflowStates.Length > 0) 
							{
								// if it is already in the final state then bail
								if (workItemToUpdate.State.ToLowerInvariant() == workflowStates.Last().ToLowerInvariant())
								{
									Log.Debug("WorkItem [{0}] is already in state [{1}]", workItemId, workItemToUpdate.State);
									return;
								}

								// check to make sure that the first state is valid
								// if it is then update the work item through the workflow
								// if not then just process the states from the config file as normal 
								workItemToUpdate.State = workflowStates[0];

								// check to see if the work item is already in any of the states
								// if so then skip those states
								if (workItemToUpdate.IsValid()) 
								{
									Log.Debug("Attempting to process WorkItem [{0}] through workflow of [{1}].", workItemId, attemptState);
									foreach (var workflowState in workflowStates)
									{
									    UpdateStateOfExternalItem(card, new List<string> {workflowState.Trim()}, mapping, runOnlyOnce);
									}
									// Get the work item again and check the current state. 
									// if it is not in the last state of the workflow then something went wrong
									// so reverse the updates we made and it will then try the next configuration workflow if there is one
									var updatedWorkItem = _projectCollectionWorkItemStore.GetWorkItem(workItemId);
									if (updatedWorkItem != null)
									{
										if (updatedWorkItem.State.ToLowerInvariant() == workflowStates.Last().ToLowerInvariant())
										{
											return;
										}

										// try to reverse the changes we've made
										Log.Debug("Attempted invalid workflow for WorkItem [{0}]. Attempting to reverse previous state changes.", workItemId);
										foreach (var workflowState in workflowStates.Reverse().Skip(1))
										{
										    UpdateStateOfExternalItem(card, new List<string> {workflowState.Trim()}, mapping, runOnlyOnce);
										}

										// now try to set it back whatever it was before
										Log.Debug("Attempted invalid workflow for WorkItem [{0}]. Setting state back to initial state of [{1}].", workItemId, initialState);
										UpdateStateOfExternalItem(card, new List<string> {initialState.Trim()}, mapping, runOnlyOnce );

										// set the current attempt to empty string so that it will not be valid and 
										// we'll try the next state (or workflow)
										attemptState = "";
									}
								}
							}
						}
						

						if (workItemToUpdate.State.Equals(attemptState, StringComparison.InvariantCultureIgnoreCase))
						{
							Log.Debug(string.Format("WorkItem [{0}] is already in state [{1}]", workItemId, workItemToUpdate.State));
							return;
						}

						if (!string.IsNullOrEmpty(attemptState))
						{
							workItemToUpdate.State = attemptState;
							valid = workItemToUpdate.IsValid();
						}

						ctr++;

						if (valid) break;
					}

					if (!valid)
					{
						Log.Warn("Unable to update WorkItem [{0}] to [{1}] because the state is invalid from the current state.", 
							workItemId, workItemToUpdate.State);
						return;
					}

					try
					{
						workItemToUpdate.Save();
						success = true;
						Log.Debug("Updated state for mapped WorkItem [{0}] to [{1}]", workItemId, workItemToUpdate.State);
					}
					catch (ValidationException ex)
					{
						Log.Warn("Unable to update WorkItem [{0}] to [{1}], ValidationException: {2}", workItemId, workItemToUpdate.State, ex.Message);
					}
					catch (Exception ex)
					{
						Log.Error(ex, string.Format("Unable to update WorkItem [{0}] to [{1}]", workItemId, workItemToUpdate.State));
					}
				}
				else
				{
					Log.Debug("Could not retrieve WorkItem [{0}] for updating state to [{1}]", workItemId, workItemToUpdate.State);
				}
				tries++;
			}
		}

        private void WorkItemUpdated(WorkItem workItem, Card card, BoardMapping project)
        {
            Log.Info("WorkItem [{0}] updated, comparing to corresponding card...", workItem.Id);

	        var boardId = project.Identity.LeanKit;

            // sync and save those items that are different (of title, description, priority)
            var saveCard = false;
            if (workItem.Title != card.Title)
            {
                card.Title = workItem.Title;
                saveCard = true;
            }

            var description = workItem.LeanKitDescription(GetTfsVersion());
            if (description != card.Description)
            {
                card.Description = description;
                saveCard = true;
            }

            var priority = workItem.LeanKitPriority();
            if(priority!= card.Priority)
            {
                card.Priority = priority;
                saveCard = true;
            }
            
            if(workItem.Fields!=null && 
				workItem.Fields.Contains("Tags") && 
				workItem.Fields["Tags"] != null && 
				workItem.Fields["Tags"].Value.ToString() != card.Tags)
            {
	            var tfsTags = workItem.Fields["Tags"].Value.ToString();
				// since we cannot set the tags in TFS we cannot blindly overwrite the LK tags 
				// with what is in TFS. Instead we can only add TFS tags to LK
				if (!string.IsNullOrEmpty(tfsTags))
				{
					var tfsTagsArr = tfsTags.Contains(',') ? tfsTags.Split(',') : tfsTags.Split(';');
					foreach (var tag in tfsTagsArr)
					{
						if (card.Tags != null && card.Tags.ToLowerInvariant().Contains(tag.ToLowerInvariant())) continue;
						if (string.IsNullOrEmpty(card.Tags))
							card.Tags = tag;
						else
							card.Tags += "," + tag;
						saveCard = true;
					}
				}
            }

			if (workItem.Fields != null && (workItem.Fields.Contains("Original Estimate") || workItem.Fields.Contains("Story Points"))) 
			{
				if (workItem.Fields.Contains("Original Estimate") && workItem.Fields["Original Estimate"] != null && workItem.Fields["Original Estimate"].Value != null) 
				{
					double cardSize;
					var isNumber = Double.TryParse(workItem.Fields["Original Estimate"].Value.ToString(), out cardSize);
					if (isNumber)
					{
						var size = (int) cardSize;
						if (card.Size != size)
						{
							card.Size = size;
							saveCard = true;
						}
					}
				} 
				else if (workItem.Fields.Contains("Story Points") && workItem.Fields["Story Points"] != null && workItem.Fields["Story Points"].Value != null) 
				{
					double cardSize;
					var isNumber = Double.TryParse(workItem.Fields["Story Points"].Value.ToString(), out cardSize);
					if (isNumber)
					{
						var size = (int)cardSize;
						if (card.Size != size) 
						{
							card.Size = size;
							saveCard = true;
						}
					}
				}
			}

			if ((card.Tags == null || !card.Tags.Contains(ServiceName)) && project.TagCardsWithTargetSystemName) 
			{
				if (string.IsNullOrEmpty(card.Tags))
					card.Tags = ServiceName;
				else
					card.Tags += "," + ServiceName;
				saveCard = true;
			}

            if(saveCard)
            {
                Log.Info("Updating card [{0}]", card.Id);
                LeanKit.UpdateCard(boardId, card);
            }

			// check the state of the work item
			// if we have the state mapped to a lane then check to see if the card is in that lane
			// if it is not in that lane then move it to that lane
	        if (!project.UpdateCardLanes || string.IsNullOrEmpty(workItem.State)) return;

	        // if card is already in archive lane then we do not want to move it to the end lane
	        // because it is effectively the same thing with respect to integrating with TFS
	        if (card.LaneId == project.ArchiveLaneId)
	        {
		        return;
	        }

			var laneIds = project.LanesFromState(workItem.State);

	        if (laneIds.Any())
	        {
		        if (!laneIds.Contains(card.LaneId))
		        {
					// first let's see if any of the lanes are sibling lanes, if so then 
					// we should be using one of them. So we'll limit the results to just siblings
					if (project.ValidLanes != null) {
						var siblingLaneIds = (from siblingLaneId in laneIds
											  let parentLane =
												  project.ValidLanes.FirstOrDefault(x =>
													  x.HasChildLanes &&
													  x.ChildLaneIds.Contains(siblingLaneId) &&
													  x.ChildLaneIds.Contains(card.LaneId))
											  where parentLane != null
											  select siblingLaneId).ToList();
						if (siblingLaneIds.Any())
							laneIds = siblingLaneIds;
					}

			        LeanKit.MoveCard(project.Identity.LeanKit, card.Id, laneIds.First(), 0, "Moved Lane From TFS Work Item");
		        }
	        }
        }

	    protected override void CardUpdated(Card card, List<string> updatedItems, BoardMapping boardMapping)
	    {
		    if (!card.ExternalSystemName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase))
			    return;

		    if (string.IsNullOrEmpty(card.ExternalCardID))
			    return;

		    Log.Info("Card [{0}] updated.", card.Id);

		    int workItemId;
		    try
		    {
			    workItemId = Convert.ToInt32(card.ExternalCardID);
		    }
		    catch (Exception)
		    {
			    Log.Debug("Ignoring card [{0}] with missing external id value.", card.Id);
			    return;
		    }

		    Log.Debug("Attempting to load Work Item [{0}]", workItemId);
		    WorkItem workItem;
		    try
		    {
			    workItem = _projectCollectionWorkItemStore.GetWorkItem(workItemId);
		    }
		    catch (Exception ex)
		    {
			    Log.Error(ex, string.Format("Could not load Work Item [{0}]", workItemId));
			    return;
		    }

		    if (workItem == null)
		    {
			    Log.Debug("Failed to find work item matching [{0}].", workItemId);
			    return;
		    }

		    if (updatedItems.Contains("Title") && workItem.Title != card.Title)
			    workItem.Title = card.Title;


		    if (updatedItems.Contains("Description"))
		    {
			    var description = workItem.LeanKitDescription(GetTfsVersion());
			    if (description != card.Description)
			    {
				    if (workItem.UseReproSteps())
					    workItem.Fields["Repro Steps"].Value = card.Description;
				    else
					    workItem.Description = card.Description;
			    }
		    }

		    if (updatedItems.Contains("Priority"))
		    {
			    var currentWorkItemPriority = workItem.LeanKitPriority();
			    if (currentWorkItemPriority != card.Priority)
				    SetWorkItemPriority(workItem, card.Priority);
		    }

		    if (updatedItems.Contains("DueDate"))
		    {
			    SetDueDate(workItem, card.DueDate);
		    }

		    if (workItem.IsDirty)
		    {
			    Log.Info("Updating corresponding work item [{0}]", workItem.Id);
			    workItem.Save();
		    }

		    // unsupported properties; append changes to history

		    if (updatedItems.Contains("Size"))
		    {
			    workItem.History += "Card size changed to " + card.Size + "\r";
			    workItem.Save();
		    }

		    if (updatedItems.Contains("Blocked"))
		    {
			    if (card.IsBlocked)
				    workItem.History += "Card is blocked: " + card.BlockReason + "\r";
			    else
				    workItem.History += "Card is no longer blocked: " + card.BlockReason + "\r";
			    workItem.Save();
		    }

		    if (updatedItems.Contains("Tags"))
		    {
			    workItem.History += "Tags in LeanKit changed to " + card.Tags + "\r";
			    workItem.Save();
		    }

	    }

	    private void SetDueDate(WorkItem workItem, string date)
        {
            if (workItem.Fields.Contains("Due Date"))
                workItem.Fields["Due Date"].Value = date;
        }

		private Microsoft.TeamFoundation.WorkItemTracking.Client.WorkItemType GetTfsWorkItemType(BoardMapping boardMapping, WorkItemTypeCollection workItemTypes, long cardTypeId)
		{			
			if (boardMapping != null && 
				boardMapping.Types != null && 
				boardMapping.ValidCardTypes != null && 
				boardMapping.Types.Any() && 
				boardMapping.ValidCardTypes.Any() )
			{				
				var lkType = boardMapping.ValidCardTypes.FirstOrDefault(x => x.Id == cardTypeId);
				if (lkType != null)
				{
					// first check for mapped type
					var mappedType = boardMapping.Types.FirstOrDefault(x => x.LeanKit.ToLowerInvariant() == lkType.Name.ToLowerInvariant());
					if (mappedType != null) 
					{
						if (workItemTypes.Contains(mappedType.Target))
							return workItemTypes[mappedType.Target];
					}
					// now check for implicit type
					if (workItemTypes.Contains(lkType.Name))
						return workItemTypes[lkType.Name];
				}

			}
			// else just return the first type from list of types from TFS
			return workItemTypes[0];
		}

		protected override void CreateNewItem(Card card, BoardMapping boardMapping) 
		{
			var project = _projectCollectionWorkItemStore.Projects[boardMapping.Identity.TargetName];
			var tfsWorkItemType = GetTfsWorkItemType(boardMapping, project.WorkItemTypes, card.TypeId);
			var workItemType = project.WorkItemTypes[tfsWorkItemType.Name];

			// Note: using the default state
			var workItem = new WorkItem(workItemType)
				{
					Title = card.Title,
					Description = card.Description,					
				};

			SetWorkItemPriority(workItem, card.Priority);
			
			if (!string.IsNullOrEmpty(card.DueDate))
			{
				if (workItem.Fields.Contains("Due Date"))
					workItem.Fields["Due Date"].Value = card.DueDate;
			}

			if (card.AssignedUserIds != null && card.AssignedUserIds.Any())
			{
				SetAssignedUser(workItem, boardMapping.Identity.LeanKit, card.AssignedUserIds[0]);
			}

			if (!string.IsNullOrEmpty(boardMapping.IterationPath)) {
				workItem.Fields["System.IterationPath"].Value = boardMapping.IterationPath;
			}

			try 
			{
				Log.Debug("Attempting to create Work Item from Card [{0}]", card.Id);

				workItem.Save();

				Log.Debug("Created Work Item [{0}] from Card [{1}]", workItem.Id, card.Id);

				card.ExternalCardID = workItem.Id.ToString(CultureInfo.InvariantCulture);
				card.ExternalSystemName = ServiceName;

				if (_projectHyperlinkService != null) 
				{
					card.ExternalSystemUrl = _projectHyperlinkService.GetWorkItemEditorUrl(workItem.Id).ToString();
				}	

				// now that we've created the work item let's try to set it to any matching state defined by lane
				var states = boardMapping.LaneToStatesMap[card.LaneId];
				if (states != null) 
				{
					UpdateStateOfExternalItem(card, states, boardMapping, true);
				}			

				LeanKit.UpdateCard(boardMapping.Identity.LeanKit, card);
			} 
			catch (ValidationException ex) 
			{
				Log.Error("Unable to create WorkItem from Card [{0}]. ValidationException: {1}", card.Id, ex.Message);
			} 
			catch (Exception ex) 
			{
				Log.Error("Unable to create WorkItem from Card [{0}], Exception: {1}", card.Id, ex.Message);
			}
		}

        public override void Init()
        {
            if (Configuration == null) return;

	        try
	        {
		        _projectCollectionUri = new Uri(Configuration.Target.Url);
	        }
	        catch (UriFormatException ex)
	        {
		        Log.Error("Error connection to TFS. Ensure the Target Host is a valid URL: {0} - {1}", ex.GetType(), ex.Message);
		        return;
	        }

            Log.Info("Connecting to TFS...");
            //This is used to query TFS for new WorkItems
            try
            {
	            if (_projectCollectionNetworkCredentials == null)
	            {			
					// if this is hosted TFS then we need to authenticate a little different
					// see this for setup to do on visualstudio.com site:
					// http://blogs.msdn.com/b/buckh/archive/2013/01/07/how-to-connect-to-tf-service-without-a-prompt-for-liveid-credentials.aspx
		            if (_projectCollectionUri.Host.ToLowerInvariant().Contains(".visualstudio.com"))
		            {
			            _projectCollectionNetworkCredentials = new NetworkCredential(Configuration.Target.User, Configuration.Target.Password);

			            if (_basicAuthCredential == null)
				            _basicAuthCredential = new BasicAuthCredential(_projectCollectionNetworkCredentials);

			            if (_tfsClientCredentials == null)
			            {
				            _tfsClientCredentials = new TfsClientCredentials(_basicAuthCredential);
				            _tfsClientCredentials.AllowInteractive = false;
			            }

			            if (_projectCollection == null)
			            {
				            _projectCollection = new TfsTeamProjectCollection(_projectCollectionUri, _tfsClientCredentials);
							_projectHyperlinkService = _projectCollection.GetService<TswaClientHyperlinkService>();
				            _projectCollectionWorkItemStore = new WorkItemStore(_projectCollection);
			            }
		            }
		            else
		            {
						_projectCollectionNetworkCredentials = new NetworkCredential(Configuration.Target.User, Configuration.Target.Password);
			            if (_projectCollection == null)
			            {
				            _projectCollection = new TfsTeamProjectCollection(_projectCollectionUri, _projectCollectionNetworkCredentials);
				            _projectHyperlinkService = _projectCollection.GetService<TswaClientHyperlinkService>();
				            _projectCollectionWorkItemStore = new WorkItemStore(_projectCollection);
			            }
		            }
	            }

                if (_projectCollectionWorkItemStore == null)
					_projectCollectionWorkItemStore = new WorkItemStore(_projectCollection);

                if (_projectCollection != null && _tfsUsers == null)
                    LoadTfsUsers();

            }
            catch (Exception e)
            {
                Log.Error("Error connecting to TFS: {0} - {1}", e.GetType(), e.Message);
            }

	        // per project, if exclusions are defined, build type filter to exclude them
            foreach (var mapping in Configuration.Mappings)
            {
                mapping.ExcludedTypeQuery = "";
                if (mapping.Excludes == null) continue;
                var excludedTypes = mapping.Excludes.Split(',');
                Log.Debug("Excluded WorkItemTypes for [{0}]:", mapping.Identity.Target);
                foreach (var type in excludedTypes)
                {
                    var trimmedType = type.Trim();
                    Log.Debug(">> [{0}]", trimmedType);
                    mapping.ExcludedTypeQuery += String.Format(" AND [System.WorkItemType] <> '{0}'", trimmedType);
                }
            }
        }

		private int GetTfsVersion()
		{
			const int def = 2012;

			if (_projectCollection == null)
				return def;
			
			try
			{
				var buildServer = _projectCollection.GetService<IBuildServer>();
				switch (buildServer.BuildServerVersion)
				{
					case(BuildServerVersion.V3):
						return 2010;
					case(BuildServerVersion.V4):
						return 2012;
					default:
						return def;
				}
			} catch (Exception ex) {
				Log.Error("An error occurred: {0} - {1} - {2}", ex.GetType(), ex.Message, ex.StackTrace);
				return def;
			}
		}

        public void LoadTfsUsers()
        {
	        if (_projectCollection == null || Configuration == null) return;

			try
            {
				Log.Info("Loading TFS Users...");
                var users = new List<Microsoft.TeamFoundation.Server.Identity>();
                var iss = _projectCollection.GetService<ICommonStructureService>();
				var gss = (IGroupSecurityService) _projectCollection.GetService(typeof (IGroupSecurityService));
	            if (iss == null || gss == null)
	            {
		            _tfsUsers = users;
		            return;
	            }
				foreach (var boardMapping in Configuration.Mappings)
	            {
					var project = _projectCollectionWorkItemStore.Projects[boardMapping.Identity.TargetName];
					var groupsInProject = gss.ListApplicationGroups(project.Uri.ToString());
					if (groupsInProject == null || !groupsInProject.Any()) continue;
					foreach (var groupInProject in groupsInProject)
					{
						var pg = gss.ReadIdentity(SearchFactor.Sid, groupInProject.Sid, QueryMembership.Expanded);
						if (pg == null || pg.Members == null || !pg.Members.Any()) continue;
						foreach (var memberId in pg.Members)
						{
							var member = gss.ReadIdentity(SearchFactor.Sid, memberId, QueryMembership.Expanded);
							if (member == null || member.Type == IdentityType.ApplicationGroup) continue;
							if (!users.Contains(member))
							{
								users.Add(member);
							}
						}
					}
				}
				_tfsUsers = users;
            }
            catch (Exception ex)
            {
                Log.Error("An error occurred: {0} - {1} - {2}", ex.GetType(), ex.Message, ex.StackTrace);
            }            
        }

		public override void Shutdown() 
		{
			base.Shutdown();
			_projectCollectionNetworkCredentials = null;
			_projectCollection = null;
			_projectCollectionWorkItemStore = null;
		    _tfsUsers = null;
		}
    }
}
