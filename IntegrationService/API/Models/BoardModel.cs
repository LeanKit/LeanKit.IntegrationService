//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System.Collections.Generic;
using AutoMapper;
using IntegrationService.Util;
using LeanKit.API.Client.Library.TransferObjects;
using KanbanBoard = LeanKit.API.Client.Library.TransferObjects.Board;
using KanbanLane = LeanKit.API.Client.Library.TransferObjects.Lane;

namespace IntegrationService.API.Models
{
    // Namespace:App.codegen
    // ModelBase:NiceTools.Model

    public class Board
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public List<LaneModel> Lanes { get; set; }
        public List<CardTypeModel> CardTypes { get; set; }
        public string LaneHtml { get; set; }
    }

    public class BoardListItem
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public string TargetProjectId { get; set; }
        public string TargetProjectName { get; set; }
    }

    public class LaneHtml
    {
        public long BoardId { get; set; }
        public string Html { get; set; }
    }

    public class LaneModel
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public int Index { get; set; }
        public string Relation { get; set; }
        public bool IsParent { get; set; }
        public LaneClassType ClassType { get; set; }
        public LaneType Type { get; set; }
        public IList<LaneModel> ChildLanes { get; set; }
        public Orientation Orientation { get; set; }
        public int Level { get; set; }
        public Activity Activity { get; set; }
    }

    public class CardTypeModel
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string ColorHex { get; set; }
        public string IsDefault { get; set; }
    }

    
    // End

	public class LaneName
	{
		public long Id { get; set; }
		public string Name { get; set; }
	}

    // Mapping definitions
    public class BoardMaps : IModelMapping
    {
        public void Init()
        {
            Mapper.CreateMap<KanbanBoard, Board>()
                .ForMember(m => m.LaneHtml, opt => opt.Ignore());

            Mapper.CreateMap<KanbanBoard, BoardListItem>()
                .ForMember(m => m.TargetProjectId, opt => opt.Ignore())
                .ForMember(m => m.TargetProjectName, opt => opt.Ignore());

            Mapper.CreateMap<KanbanLane, LaneModel>()
                .ForMember(m => m.Relation, opt => opt.MapFrom(s => s.LaneState))
                .ForMember(m => m.ChildLanes, opt => opt.Ignore())
                .ForMember(m => m.IsParent, opt => opt.Ignore())
                .ForMember(m => m.Level, opt => opt.Ignore())
                .ForMember(m => m.Activity, opt => opt.Ignore());


           Mapper.CreateMap<CardType, CardTypeModel>();
       }
   }
}
