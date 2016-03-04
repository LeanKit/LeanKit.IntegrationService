﻿//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using IntegrationService.Util;
using LeanKit.API.Client.Library;
using LeanKit.API.Client.Library.EventArguments;

namespace IntegrationService
{
    public interface IBoardSubscriptionManager
    {
        ILeanKitApi Subscribe(ILeanKitAccountAuth auth, long boardId, int pollingFrequency, Action<long, BoardChangedEventArgs, ILeanKitApi> notification);
        void Unsubscribe(long boardId);
        void Shutdown();
    }
    public class BoardSubscriptionManager : IBoardSubscriptionManager
    {
        private static readonly Dictionary<long, BoardSubscription> BoardSubscriptions = new Dictionary<long, BoardSubscription>();
	    private static Logger _log;

		public BoardSubscriptionManager()
		{
			_log = Logger.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		}

        internal class BoardSubscription
        {
	        private readonly long _boardId;
            internal readonly ILeanKitApi LkClientApi;
            internal ILeanKitIntegration Integration;
            internal readonly List<Action<long, BoardChangedEventArgs, ILeanKitApi>> Notifications = new List<Action<long, BoardChangedEventArgs, ILeanKitApi>>();
            
            internal BoardSubscription(ILeanKitAccountAuth auth, long boardId, int pollingFrequency)
            {
                _boardId = boardId;
				LkClientApi = new LeanKitClientFactory().Create(auth);
	            var settings = new IntegrationSettings {CheckForUpdatesIntervalSeconds = pollingFrequency};
	            Integration = new LeanKitIntegrationFactory().Create(_boardId, auth, settings);

                new Thread(WatchThread).Start();
            }

            internal void StopWatching()
            {
                Integration.ShouldContinue = false;
            }

            private void WatchThread()
            {
                var boardIdLocal = _boardId;
                var lkClientApi = LkClientApi;

                Integration.BoardChanged += (sender, args) => Notifications.ForEach(action => action(boardIdLocal, args, lkClientApi));
	            Integration.ClientError += IntegrationOnClientError;

	            try
	            {
					_log.Debug(string.Format("Start watching board [{0}]", boardIdLocal));
		            Integration.StartWatching();					
	            }
	            catch (LeanKit.API.Client.Library.Exceptions.UnauthorizedAccessException e)
	            {
		            _log.Error(string.Format("Error authenticating with LeanKit: {0}", e.Message));		            
	            }
	            catch (Exception e)
	            {
		            _log.Error(string.Format("Unknown error: {0} - {1} - {2}", e.GetType(), e.Message, e.StackTrace));		            
	            }
            }

	        private void IntegrationOnClientError(object sender, ClientErrorEventArgs args)
	        {
		        _log.Error(args.Exception, args.Message);
	        }
        }

        public ILeanKitApi Subscribe(ILeanKitAccountAuth auth, long boardId, int pollingFrequency, Action<long, BoardChangedEventArgs, ILeanKitApi> notification)
        {
            if (notification == null)
            {
                throw new Exception("Must provide subscription notification function");
            }
            lock (BoardSubscriptions)
            {
	            if (BoardSubscriptions.ContainsKey(boardId)) return BoardSubscriptions[boardId].LkClientApi;

	            BoardSubscriptions[boardId] = new BoardSubscription(auth, boardId, pollingFrequency);
	            BoardSubscriptions[boardId].Notifications.Add(notification);

	            return BoardSubscriptions[boardId].LkClientApi;
            }
        }

        public void Unsubscribe(long boardId)
        {
            BoardSubscription sub;
            lock (BoardSubscriptions)
            {
                if (!BoardSubscriptions.ContainsKey(boardId))
                {
                    throw new Exception(string.Format("Board id [{0}] not found", boardId));
                }
                sub = BoardSubscriptions[boardId];
            }
			_log.Debug(string.Format("Stop watching board [{0}]", boardId));
            sub.StopWatching();
	        BoardSubscriptions.Remove(boardId);
        }

        public void Shutdown()
        {
            lock (BoardSubscriptions)
            {
                var boardIds = BoardSubscriptions.Keys.ToList();
	            try
	            {
		            boardIds.ForEach(Unsubscribe);
	            }
	            catch (Exception ex)
	            {
					_log.Error(string.Format("An error occured: {0} - {1}", ex.GetType(), ex.Message));
	            }
            }
        }
    }
}
