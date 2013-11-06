//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoMapper;
using IntegrationService.API.Models;
using IntegrationService.Util;
using LeanKit.API.Client.Library;
using LeanKit.API.Client.Library.TransferObjects;
using ServiceStack.ServiceHost;
using Board = LeanKit.API.Client.Library.TransferObjects.Board;

namespace IntegrationService.API
{
	public class Request
	{
		public string Url { get { return Protocol + Host; } }

		public string Protocol { get; set; }
		public string Type { get; set; }
		public string Host { get; set; }
		public string User { get; set; }
		public string Password { get; set; }

		public override string ToString()
		{
			return string.Format("Url: {0}, User: {1}", Url, User);
		}
	}

	[Route("/boards")]
	public class BoardsRequest : Request
	{
	}

	[Route("/board")]
	public class BoardRequest : Request
	{
		public long BoardId { get; set; }
	}

	[Route("/lanenames")]
	public class BoardLaneRequest : Request
	{
		public List<long> BoardIds { get; set; }
	}

	public class LeanKitService : ServiceBase
	{
		private ILeanKitClientFactory LeanKitClientFactory { get; set; }

		public object Get(BoardsRequest request)
		{

			var api = Connect(request, true);

			IEnumerable<BoardListing> boards;
			try
			{
				"Getting all boards...".Debug();
				boards = api.GetBoards();
			}
			catch (Exception ex)
			{
				ex.Message.Error(ex);
				return ServerError(ex.Message);
			}

			return OK(boards.OrderBy(x => x.Title));
		}

		public object Get(BoardRequest request)
		{
			var api = Connect(request);

			Board board;
			try
			{
				string.Format("Getting board {0}", request.BoardId).Debug();
				board = api.GetBoard(request.BoardId);
			}
			catch (Exception ex)
			{
				ex.Message.Error(ex);
				return ServerError(ex.Message);
			}

			var boardmodel = Mapper.Map<Models.Board>(board);

			var allLanes = new List<LeanKit.API.Client.Library.TransferObjects.Lane>();
			allLanes.AddRange(board.Backlog);
			allLanes.AddRange(board.Lanes);
			allLanes.AddRange(board.Archive);
			boardmodel.Lanes.Clear();
			foreach (var lane in allLanes.Where(x => x.ParentLaneId == 0))
			{
				var laneModel = Mapper.Map<LaneModel>(lane);
				MapChildLanes(allLanes, lane, laneModel, 0);
				boardmodel.Lanes.Add(laneModel);
			}

			var html = HtmlHelpers.RenderBoardRepresentationStatic(boardmodel.Lanes, true);
			boardmodel.LaneHtml = html;

			return OK(boardmodel);
		}

		private void MapChildLanes(IList<LeanKit.API.Client.Library.TransferObjects.Lane> lanes, LeanKit.API.Client.Library.TransferObjects.Lane parentLane,
		                           LaneModel parentLaneModel, int level)
		{
			parentLaneModel.ChildLanes = new List<LaneModel>();
			parentLaneModel.IsParent = false;
			parentLaneModel.Level = level;

			if (parentLane.ChildLaneIds.Count == 0) return;

			parentLaneModel.IsParent = true;
			level++;

			foreach (var childLaneId in parentLane.ChildLaneIds)
			{
				var childLane = lanes.FirstOrDefault(x => x.Id == childLaneId);
				var childLaneModel = Mapper.Map<LaneModel>(childLane);
				MapChildLanes(lanes, childLane, childLaneModel, level);
				childLaneModel.Level = level;
				parentLaneModel.ChildLanes.Add(childLaneModel);
			}
		}

		public object Get(BoardLaneRequest request)
		{
			var boards = new Dictionary<long, List<LaneName>>();
			var api = Connect(request);

			foreach (var boardId in request.BoardIds)
			{
				Board board;
				try
				{
					string.Format("Getting all lanes for board {0}", boardId).Debug();
					board = api.GetBoard(boardId);
				}
				catch (Exception ex)
				{
					ex.Message.Error(ex);
					return ServerError(ex.Message);
				}

				var laneNames = new List<LaneName>();
				var allLanes = new List<LeanKit.API.Client.Library.TransferObjects.Lane>();
				allLanes.AddRange(board.Backlog);
				allLanes.AddRange(board.Lanes);
				allLanes.AddRange(board.Archive);

				foreach (var lane in allLanes.Where(x => x.ParentLaneId == 0))
				{
					laneNames.Add(new LaneName {Id = lane.Id.GetValueOrDefault(), Name = lane.Title});
					if (lane.ChildLaneIds.Count > 0)
					{
						GetChildLaneNames(laneNames, lane.Id.GetValueOrDefault(), lane.Title, allLanes);
					}
				}

				boards.Add(boardId, laneNames);
			}

			return OK(boards);
		}

		private void GetChildLaneNames(List<LaneName> laneNames, long parentId, string title, IList<LeanKit.API.Client.Library.TransferObjects.Lane> lanes)
		{
			var children = lanes.Where(x => x.ParentLaneId == parentId);
			foreach (var lane in children)
			{
				laneNames.Add(new LaneName {Id = lane.Id.GetValueOrDefault(), Name = title + " / " + lane.Title});
				if (lane.ChildLaneIds.Count > 0)
				{
					GetChildLaneNames(laneNames, lane.Id.GetValueOrDefault(), title + " / " + lane.Title, lanes);
				}
			}
		}

		private ILeanKitApi Connect(Request request, bool saveLogin = false)
		{

			LeanKitClientFactory = new LeanKitClientFactory();
			var account = new LeanKitAccountAuth
				{
					Hostname = request.Host,
					Username = request.User,
					Password = request.Password,
					UrlTemplateOverride = request.Host
				};

			if (saveLogin) SaveLogin(account);

			// expand host if necessary
			if (account.Hostname == "kanban-cibuild")
				account.UrlTemplateOverride = "http://kanban-cibuild.localkanban.com/";
			else if (!account.Hostname.Contains("http://"))
				account.UrlTemplateOverride = "https://" + account.Hostname + ".leankit.com/";
			
			string.Format("Attempting connection to {0}", request).Debug();

			return LeanKitClientFactory.Create(account);

		}


		private void SaveLogin(LeanKitAccountAuth account)
		{
			"Saving LeanKit login information...".Debug();

			var dir = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory;
			if (dir == null) throw new Exception("Could not access current directory.");
			var curFolder = dir.FullName;
			var storagefile = Path.Combine(curFolder, "config-edit.json");
			var localStorage = new LocalStorage<Configuration>(storagefile);
			var config = File.Exists(storagefile) ? localStorage.Load() : new Configuration();

			config.LeanKit = new ServerConfiguration
				{
					Host = account.Hostname,
					User = account.Username,
					Password = account.Password
				};
			localStorage.Save(config);
		}
	}
}
