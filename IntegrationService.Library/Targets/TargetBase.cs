//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using IntegrationService.Util;
using LeanKit.API.Client.Library;
using LeanKit.API.Client.Library.EventArguments;
using LeanKit.API.Client.Library.Exceptions;
using LeanKit.API.Client.Library.TransferObjects;

namespace IntegrationService.Targets
{
    public abstract class TargetBase
    {
        protected readonly IBoardSubscriptionManager Subscriptions;
        protected readonly IConfigurationProvider<Configuration> ConfigurationProvider;
        protected readonly ILocalStorage<AppSettings> LocalStorage;
        protected readonly ILeanKitClientFactory LeanKitClientFactory;

        public abstract void Init();
		protected abstract void UpdateStateOfExternalItem(Card card, List<string> states, BoardMapping boardMapping);
        protected abstract void CardUpdated(Card card, List<string> updatedItems, BoardMapping boardMapping);
	    protected abstract void CreateNewItem(Card card, BoardMapping boardMapping);
	    protected abstract void Synchronize(BoardMapping boardMapping);

        protected static readonly Logger Log = Logger.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected AutoResetEvent StopEvent = new AutoResetEvent(false);
        protected Configuration Configuration { get; set; }
        protected LeanKitAccountAuth LeanKitAccount { get; set; }
        protected ILeanKitApi LeanKit { get; set; }
        public DateTime QueryDate { get; set; }
	    private FileSystemWatcher _configWatcher;

	    private AppSettings _appSettings;

        protected AppSettings AppSettings
	    {
	        get
	        {
		        if (_appSettings != null) return _appSettings;
		        _appSettings = LocalStorage.Load() ?? new AppSettings
			        {
				        BoardVersions = new Dictionary<long, long>(), 
						RecentQueryDate = Configuration.EarliestSyncDate
			        };
		        return _appSettings;
	        }
	    }

	    private User _currentUser;
	    protected User CurrentUser
	    {
		    get
		    {
			    if (_currentUser == null)
			    {
					// This is a hack, we shouldn't need to call GetCurrentUser(boardId)
					// we should be able to just get the current user, we know the user name from auth in wrapper
					// we should be able to just call GetCurrentUser()
				    if (Configuration != null &&
				        Configuration.Mappings != null &&
				        Configuration.Mappings[0] != null &&
				        Configuration.Mappings[0].Identity != null)
				    {
					    var curUser = LeanKit.GetCurrentUser(Configuration.Mappings[0].Identity.LeanKit);
					    if (curUser != null)
					    {
						    _currentUser = curUser;
					    }
				    }
			    }
			    return _currentUser;
		    }
			set { _currentUser = value; }
	    }

	    protected TargetBase(IBoardSubscriptionManager subscriptions)
        {
            Subscriptions = subscriptions;
            ConfigurationProvider = new ConfigurationProvider();
            LocalStorage = new LocalStorage<AppSettings>();
            LeanKitClientFactory = new LeanKitClientFactory();
            LoadConfiguration();
		    if (Configuration == null) return;
		    try
		    {
			    Init();
		    } 
		    catch (Exception e) 
		    {
			    Log.Error("Exception for Init: " + e.Message);
		    }
        }

	    protected TargetBase(IBoardSubscriptionManager subscriptions, IConfigurationProvider<Configuration> configurationProvider, ILocalStorage<AppSettings> localStorage, ILeanKitClientFactory leanKitClientFactory)
        {
            Subscriptions = subscriptions;
            ConfigurationProvider = configurationProvider;
            LocalStorage = localStorage;
            LeanKitClientFactory = leanKitClientFactory;

	        LoadConfiguration();
		    if (Configuration == null) return;
		    try
		    {
			    Init();
		    } 
		    catch (Exception e) 
		    {
			    Log.Error("Exception for Init: " + e.Message);
		    }
        }

		public virtual void Process()
		{
			if (Configuration != null && Configuration.Mappings != null)
			{
				// pickup any changes since the last time the service ran
				// start subscription to each board in board mappings			
				foreach (var mapping in Configuration.Mappings)
				{					
					CheckForMissedCardMoves(mapping);
					try
					{
						Subscriptions.Subscribe(LeanKitAccount, mapping.Identity.LeanKit, BoardUpdate);
					}
					catch (Exception ex)
					{
						Log.Error(string.Format("An error occured: {0} - {1} - {2}", ex.GetType(), ex.Message, ex.StackTrace));
					}
				}
			}

			if (Configuration != null && Configuration.Mappings != null)
			{
				QueryDate = Configuration.EarliestSyncDate.ToUniversalTime();

				int i = 0;
				while (true)
				{
					i++;
					if (i%10 == 0)
						SaveRecentQueryDate(QueryDate);

					foreach (var project in Configuration.Mappings)
					{
						try
						{
							Synchronize(project);
						}
						catch (Exception ex)
						{
							Log.Error("An error occurred: {0} - {1} - {2}", ex.GetType(), ex.Message, ex.StackTrace);
						}
					}

					if (StopEvent.WaitOne(Configuration.PollingFrequency))
						break;

					QueryDate = DateTime.Now;
				}
			}
		}

	    private void CheckForMissedCardMoves(BoardMapping mapping)
	    {
			// if we have local storage, we have saved board versions and we have one for this board			
		    long boardId = mapping.Identity.LeanKit;
			if (AppSettings != null &&
                AppSettings.BoardVersions != null &&
                AppSettings.BoardVersions.Any() &&
                AppSettings.BoardVersions.ContainsKey(boardId))
		    {
                var version = AppSettings.BoardVersions[boardId];
				Log.Debug(string.Format("Checking for any cards moved to mapped lanes on board [{0}] since service last ran, version [{1}].", boardId, version));
			    try
			    {
				    var events = LeanKit.GetBoardHistorySince(boardId, (int) version);
				    var board = LeanKit.GetBoard(boardId);
				    if (board != null && events != null)
				    {
					    foreach (var ev in events)
					    {
							// check for created cards
							if (ev.EventType == "CardCreation")
							{
								var card = LeanKit.GetCard(board.Id, ev.CardId);
								if (card != null && string.IsNullOrEmpty(card.ExternalCardID))
								{
									try
									{
										CreateNewItem(card.ToCard(), mapping);
									}
									catch (Exception e)
									{
							            Log.Error("Exception for CreateNewItem: " + e.Message);
						            }
								}
							}
							// only look for moved cards
						    else if (ev.ToLaneId != 0)
						    {
							    var lane = board.GetLaneById(ev.ToLaneId);
							    if (lane != null)
							    {
								    if (lane.Id.HasValue && mapping.LaneToStatesMap.Any() && mapping.LaneToStatesMap.ContainsKey(lane.Id.Value))
								    {
									    if (mapping.LaneToStatesMap[lane.Id.Value] != null && mapping.LaneToStatesMap[lane.Id.Value].Count > 0)
									    {
										    // board.GetCard() only seems to get cards in active lanes
										    // using LeanKitApi.GetCard() instead because it will get 
										    // cards in archive lanes
										    var card = LeanKit.GetCard(board.Id, ev.CardId);
										    if (card != null && !string.IsNullOrEmpty(card.ExternalCardID))
										    {
												try {
												    UpdateStateOfExternalItem(card.ToCard(), mapping.LaneToStatesMap[lane.Id.Value], mapping);
												} catch (Exception e) {
													Log.Error("Exception for UpdateStateOfExternalItem: " + e.Message);
												}
										    }
									    }
								    }
							    }
						    }					    
					    }
					    UpdateBoardVersion(board.Id, board.Version);
				    }
			    }
			    catch (Exception ex)
			    {
				    Log.Error(string.Format("An error occured: {0} - {1} - {2}", ex.GetType(), ex.Message, ex.StackTrace));
			    }
		    }
	    }

        private void LoadConfiguration()
        {
            Log.Debug("Starting Loading/Reloading Configuration");
            try
            {
                Configuration = ConfigurationProvider.GetConfiguration();
            }
            catch (ConfigurationErrorsException ex)
            {
                Configuration = null;
				Log.Error(ex.Message);
                return;
            }
            
            CheckLastQueryDate();
            ConnectToLeanKit();
            ConfigureDefaults();
            Log.Debug("Finished Loading Configuration");
        }

        private void ReloadConfiguration()
		{
			// We need to stop all other processes and then restart them
			Shutdown();
			Subscriptions.Shutdown();
			lock (Configuration)
			{
				LoadConfiguration();
				// call Init() so the subsystem can re-implement communication with target system
				Init();
			}
			// This will restart the polling in addition to restarting subscriptions
			Process();			
		}

		//TODO: would be great if we could reload configuration for just a single board
       
        private void ConnectToLeanKit()
        {
            LeanKitAccount = new LeanKitAccountAuth
            {
                Hostname = Configuration.LeanKit.Url,
                UrlTemplateOverride = GetTemplateOverride(Configuration.LeanKit.Url),
                Username = Configuration.LeanKit.User,
                Password = Configuration.LeanKit.Password
            };

            try
            {
				Log.Debug("Connecting to LeanKit account [{0}] with account [{1}]", LeanKitAccount.Hostname, LeanKitAccount.Username);
                LeanKit = LeanKitClientFactory.Create(LeanKitAccount);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to open LeanKit API: " + ex.Message);
            }
        }

        private string GetTemplateOverride(string host)
        {
            if (host == "kanban-cibuild") return "http://kanban-cibuild.localkanban.com";
            return "http://{0}.leankit.com";
        }

        protected void ConfigureDefaults()
        {
            // set polling frequency
            if (Configuration.PollingFrequency <= 0)
                Configuration.PollingFrequency = 60000;

            foreach (var mapping in Configuration.Mappings)
            {
                foreach (var item in mapping.LaneToStatesMap)
                    OrderWorkflowStates(item.Value);

                LoadBoardValues(mapping);
            }
        }

        private void OrderWorkflowStates(List<string> states)
        {
            // order lane states by workflow-first (states that contain a '>')
            int insertionPoint = 0;
	        for (int i = states.Count - 1; i >= insertionPoint; i--)
            {
	            string state = states[i];
	            if(state.Contains(">"))
                {
                    states.Remove(state);
                    states.Insert(insertionPoint, state);
                    insertionPoint++;
                }
            }
        }

        private void LoadBoardValues(BoardMapping boardMapping)
        {
            Board board = null;
            try
            {
                board = LeanKit.GetBoard(boardMapping.Identity.LeanKit);
            }
            catch (LeanKitAPIException ex)
            {
                Log.Error(ex, string.Format("Error getting Board: {0}",  boardMapping.Identity.LeanKit));
            }
	        
			if (board == null) return;
	        
			if (board.CardTypes != null && board.CardTypes.Any())
	        {
		        boardMapping.ValidCardTypes = board.CardTypes;
                    
		        // check to make sure we have a default card type
		        var defaultCard = boardMapping.ValidCardTypes.FirstOrDefault(x => x.IsDefault);
		        if (defaultCard == null)
		        {
			        // if we do not have a default card type then check 
			        // to see if there is a Task card type and make that the default
			        var taskCardType = boardMapping.ValidCardTypes.FirstOrDefault(x => x.Name.ToLowerInvariant() == "task");
			        if (taskCardType != null)
			        {
				        boardMapping.ValidCardTypes.FirstOrDefault(x => x.Name.ToLowerInvariant() == "task").IsDefault = true;
			        }
			        else
			        {
				        // otherwise just set the first card type to be the default
				        boardMapping.ValidCardTypes.FirstOrDefault().IsDefault = true;
			        }
		        }
	        }

	        if (board.ArchiveTopLevelLaneId.HasValue)
	        {
		        boardMapping.ArchiveLaneId = board.ArchiveTopLevelLaneId.Value;
	        }
	        else
	        {
		        var archive = board.Archive.FirstOrDefault(x => x.ParentLaneId == 0);
		        if (archive != null && archive.Id.HasValue)
		        {
			        boardMapping.ArchiveLaneId = archive.Id.Value;
		        }

		        if (boardMapping.ArchiveLaneId == 0)
		        {
			        var allLanes = board.AllLanes();
			        archive = allLanes.FirstOrDefault(x => x.ClassType == LaneClassType.Archive && x.ParentLaneId == 0);
			        if (archive != null && archive.Id.HasValue)
			        {
				        boardMapping.ArchiveLaneId = archive.Id.Value;
			        }
		        }
	        }

	        if (board.Lanes != null && board.Lanes.Any())
	        {
		        var validLanes = board.AllLanes().Where(x => x.ClassType != LaneClassType.Archive).OrderBy(x => x.Index).ToList();
		        var maxIndex = validLanes.Max(x => x.Index);

		        // only use active lanes for the purpose of selecting the default drop lane
		        var activeLanes = board.Lanes.Where(x => x.ClassType == LaneClassType.Active).OrderBy(x => x.Index).ToList();
		        var defaultDropLaneId = GetDefaultDropLane(activeLanes);

		        boardMapping.ValidLanes = validLanes
			        .Select(x => new Lane
			        {
				        Id = x.Id.Value, 
				        Name = x.Title, 
				        IsFirst = x.Id == defaultDropLaneId, 
				        ChildLaneIds = (x.ChildLaneIds != null && x.ChildLaneIds.Any()) ? x.ChildLaneIds : null,
				        IsLast = (boardMapping.ArchiveLaneId > 0) ? x.Id == boardMapping.ArchiveLaneId : x.Index == maxIndex
			        })
			        .ToList();

		        if (boardMapping.ArchiveLaneId > 0)
		        {
			        var archiveLane = board.GetLaneById(boardMapping.ArchiveLaneId);
			        if (archiveLane != null)
			        {
				        boardMapping.ValidLanes.Add(new Lane
				        {
					        Id = boardMapping.ArchiveLaneId,
					        Name = archiveLane.Title,
					        IsFirst = false,
					        IsLast = true
				        });
			        }
		        }
	        }

	        if (boardMapping.Types == null)
		        boardMapping.Types = new List<WorkItemType>();

	        // values in LaneToStatesMap are assumed to valid, as they were configured using valid values.
	        if(boardMapping.LaneToStatesMap==null)
	        {
		        Log.Fatal("An unexpected error occurred -- there is no valid lane-to-states mapping.");
	        }
        }

		// This method is based on method of same name from Kanban.ApplicationServices.BoardService
		private long GetDefaultDropLane(IList<LeanKit.API.Client.Library.TransferObjects.Lane> lanes)
		{
			//get first top level lane
			var firstParentLane = lanes.Where(x => x.ParentLaneId == 0)
									   .OrderBy(x => x.Index)
									   .FirstOrDefault();

			if (null != firstParentLane)
			{
				var defaultDropLane = firstParentLane.ChildLaneIds != null && firstParentLane.ChildLaneIds.Any()
					                      ? FindFirstChildLane(lanes.Where(x => x.ParentLaneId == firstParentLane.Id).OrderBy(x => x.Index).FirstOrDefault(), lanes)
					                      : firstParentLane;

				return (defaultDropLane != null && defaultDropLane.Id.HasValue) ? defaultDropLane.Id.Value : 0;
			}

			return 0;
		}

		// This method is based on method of same name from Kanban.ApplicationServices.BoardService
		private LeanKit.API.Client.Library.TransferObjects.Lane FindFirstChildLane(LeanKit.API.Client.Library.TransferObjects.Lane parentLane, IList<LeanKit.API.Client.Library.TransferObjects.Lane> allLanes)
		{
			if (parentLane == null) return null;
			return (parentLane.ChildLaneIds != null && parentLane.ChildLaneIds.Any())
					   ? FindFirstChildLane(allLanes.Where(x => x.ParentLaneId == parentLane.Id).OrderBy(x => x.Index).FirstOrDefault(), allLanes)
					   : parentLane;
		}

        public virtual void Shutdown()
        {
			// Stop watching
			if (_configWatcher != null)
			{
				_configWatcher.EnableRaisingEvents = false;
				_configWatcher = null;
			}
	        _currentUser = null;
            StopEvent.Set();
        }

		protected virtual void BoardUpdate(long boardId, BoardChangedEventArgs eventArgs, ILeanKitApi integration)
		{
		    if (eventArgs.BoardStructureChanged)
		    {
		        Log.Debug(String.Format("Received BoardStructureChanged event for [{0}], reloading Configuration", boardId));
		        // TODO: Ideally this would be ReloadConfiguration(boardId);
		        ReloadConfiguration();
		    }

		    var boardConfig = Configuration.Mappings.FirstOrDefault(x => x.Identity.LeanKit == boardId);
		    if (boardConfig == null)
		    {
		        Log.Debug(String.Format("Expected a configuration for board [{0}].", boardId));
		        return;
		    }

		    Log.Debug(String.Format("Received board changed event for board [{0}]", boardId));

            // check for content change events
		    if (!boardConfig.UpdateTargetItems)
		    {
		        Log.Info("Skipped target item update because 'UpdateTargetItems' is disabled.");
		    }
		    else
		    {
		        Log.Info("Checking for updated cards.");
			    if (eventArgs.UpdatedCards.Any())
			    {
				    var itemsUpdated = new List<string>();
				    foreach (var updatedCardEvent in eventArgs.UpdatedCards)
				    {
					    try
					    {
						    if (updatedCardEvent.UpdatedCard == null) throw new Exception("Updated card is null");
						    if (updatedCardEvent.OriginalCard == null) throw new Exception("Original card is null");

						    var card = updatedCardEvent.UpdatedCard;

						    if (string.IsNullOrEmpty(card.ExternalCardID) && !string.IsNullOrEmpty(card.ExternalSystemUrl))
						    {
							    // try to grab id from url
							    var pos = card.ExternalSystemUrl.LastIndexOf('=');
							    if (pos > 0)
								    card.ExternalCardID = card.ExternalSystemUrl.Substring(pos + 1);
						    }

						    if (string.IsNullOrEmpty(card.ExternalCardID)) continue; // still invalid; skip this card

						    if (card.Title != updatedCardEvent.OriginalCard.Title)
							    itemsUpdated.Add("Title");
						    if (card.Description != updatedCardEvent.OriginalCard.Description)
							    itemsUpdated.Add("Description");
						    if (card.Tags != updatedCardEvent.OriginalCard.Tags)
							    itemsUpdated.Add("Tags");
						    if (card.Priority != updatedCardEvent.OriginalCard.Priority)
							    itemsUpdated.Add("Priority");
						    if (card.DueDate != updatedCardEvent.OriginalCard.DueDate)
							    itemsUpdated.Add("DueDate");
						    if (card.Size != updatedCardEvent.OriginalCard.Size)
							    itemsUpdated.Add("Size");
						    if (card.IsBlocked != updatedCardEvent.OriginalCard.IsBlocked)
							    itemsUpdated.Add("Blocked");

						    if (itemsUpdated.Count <= 0) continue;

						    CardUpdated(card, itemsUpdated, boardConfig);
					    }
					    catch (Exception e)
					    {
							var card = updatedCardEvent.UpdatedCard ?? updatedCardEvent.OriginalCard ?? new Card();
							string.Format("Error processing blocked card, [{0}]: {1}", card.Id, e.Message).Error(e);
					    }
				    }
			    }
			    if (eventArgs.BlockedCards.Any())
				{
		            var itemsUpdated = new List<string>();
					foreach (var cardBlockedEvent in eventArgs.BlockedCards)
					{
						try
						{
							var card = cardBlockedEvent.BlockedCard;
							if (string.IsNullOrEmpty(card.ExternalCardID) && !string.IsNullOrEmpty(card.ExternalSystemUrl))
							{
								// try to grab id from url
								var pos = card.ExternalSystemUrl.LastIndexOf('=');
								if (pos > 0)
									card.ExternalCardID = card.ExternalSystemUrl.Substring(pos + 1);
							}

							if (string.IsNullOrEmpty(card.ExternalCardID)) continue; // still invalid; skip this card

							if (card.IsBlocked != cardBlockedEvent.BlockedCard.IsBlocked)
								itemsUpdated.Add("Blocked");

							if (itemsUpdated.Count <= 0) continue;
							CardUpdated(card, itemsUpdated, boardConfig);
						}
						catch (Exception e)
						{
							var card = cardBlockedEvent.BlockedCard ?? new Card();
							string.Format("Error processing blocked card, [{0}]: {1}", card.Id, e.Message).Error(e);
						}
					}
				}
				if (eventArgs.UnBlockedCards.Any())
				{
					var itemsUpdated = new List<string>();
					foreach (var cardUnblockedEvent in eventArgs.UnBlockedCards) 
					{
						try
						{
							var card = cardUnblockedEvent.UnBlockedCard;
							if (string.IsNullOrEmpty(card.ExternalCardID) && !string.IsNullOrEmpty(card.ExternalSystemUrl))
							{
								// try to grab id from url
								var pos = card.ExternalSystemUrl.LastIndexOf('=');
								if (pos > 0)
									card.ExternalCardID = card.ExternalSystemUrl.Substring(pos + 1);
							}

							if (string.IsNullOrEmpty(card.ExternalCardID)) continue; // still invalid; skip this card

							if (card.IsBlocked != cardUnblockedEvent.UnBlockedCard.IsBlocked)
								itemsUpdated.Add("Blocked");

							if (itemsUpdated.Count <= 0) continue;
							CardUpdated(card, itemsUpdated, boardConfig);
						}
						catch (Exception e)
						{
							var card = cardUnblockedEvent.UnBlockedCard ?? new Card();
							string.Format("Error processing unblocked card, [{0}]: {1}", card.Id, e.Message).Error(e);
						}
					}					
				}
		    }


            // check for content change events
			if (!boardConfig.CreateTargetItems)
			{
				Log.Info("Skipped adding target items because 'AddTargetItems' is disabled.");
			}
			else
			{
				Log.Info("Checking for added cards.");
				if (eventArgs.AddedCards.Any())
				{
					foreach (var newCard in eventArgs.AddedCards.Select(cardAddEvent => cardAddEvent.AddedCard)
						.Where(newCard => newCard != null && string.IsNullOrEmpty(newCard.ExternalCardID)))
					{
						try
						{
							CreateNewItem(newCard, boardConfig);
						}
						catch (Exception e)
						{
							string.Format("Error processing newly created card, [{0}]: {1}", newCard.Id, e.Message).Error(e);
						}
					}
				}
			}

			//Ignore all other events except for MovedCardEvents
			if (!eventArgs.MovedCards.Any()) 
			{
				Log.Debug(String.Format("No Card Move Events detected event for board [{0}], exiting method", boardId));
				return;
			}

			UpdateBoardVersion(boardId);

			Log.Debug("Checking for cards moved to mapped lanes.");
			foreach (var movedCardEvent in eventArgs.MovedCards.Where(x => x != null && x.ToLane != null && x.MovedCard != null))
			{
				try
				{
					if (!movedCardEvent.ToLane.Id.HasValue) continue;
					
					if (boardConfig.LaneToStatesMap.Any() &&
					    boardConfig.LaneToStatesMap.ContainsKey(movedCardEvent.ToLane.Id.Value))
					{
						var states = boardConfig.LaneToStatesMap[movedCardEvent.ToLane.Id.Value];
						if (states != null && states.Count > 0)
						{
							try
							{
								if (!string.IsNullOrEmpty(movedCardEvent.MovedCard.ExternalCardID))
									UpdateStateOfExternalItem(movedCardEvent.MovedCard, states, boardConfig);
								else if (boardConfig.CreateTargetItems) 
									// This may be a task card being moved to the parent board, or card being moved from another board
									CreateNewItem(movedCardEvent.MovedCard, boardConfig);
							}
							catch (Exception e)
							{
								Log.Error("Exception for UpdateStateOfExternalItem: " + e.Message);
							}
						}
						else
							Log.Debug(String.Format("No states are mapped to the Lane [{0}]", movedCardEvent.ToLane.Id.Value));
					}
					else
					{
						Log.Debug(String.Format("No states are mapped to the Lane [{0}]", movedCardEvent.ToLane.Id.Value));
					}
				}
				catch (Exception e)
				{
					string.Format("Error processing moved card, [{0}]: {1}", movedCardEvent.MovedCard.Id, e.Message).Error(e);
				}
			}
		}

	    protected void SaveRecentQueryDate(DateTime queryDate)
	    {
		    try
		    {
			    Log.Debug("Saving Recent Query Date: {0} at {1}", queryDate.ToString("o"), queryDate.ToShortDateString());
			    AppSettings.RecentQueryDate = queryDate;
			    LocalStorage.Save(AppSettings);
		    }
		    catch (Exception ex)
		    {
			    Log.Error("An error occurred: {0} - {1} - {2}", ex.GetType(), ex.Message, ex.StackTrace);
		    }
	    }

	    private void CheckLastQueryDate() 
		{
            Configuration.EarliestSyncDate = AppSettings.RecentQueryDate;
		}

		private void UpdateBoardVersion(long boardId, long? version = null) 
		{
			if (!version.HasValue) {
				var board = LeanKit.GetBoard(boardId);
				if (board == null)
					return;
				version = board.Version;
			}

			if (AppSettings.BoardVersions.ContainsKey(boardId)) {
				AppSettings.BoardVersions[boardId] = version.Value;
			} else {
				AppSettings.BoardVersions.Add(boardId, version.Value);
			}
			LocalStorage.Save(AppSettings);
		}

    }
}
