//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using IntegrationService.Util;

namespace IntegrationService.API.Models
{
    // Namespace:App.codegen
    // ModelBase:NiceTools.Model
    public class ProjectListItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string PathFilter { get; set; }
    }

    public class ConfigurationModel
    {
        public ServerConfigurationModel Target { get; set; }
        public ServerConfigurationModel LeanKit { get; set; }
        public List<BoardMappingModel> Mappings { get; set; }
        public ConfigurationSettingsModel Settings { get; set; }
    }

    public class ConfigurationSettingsModel
    {
        public int PollingFrequency { get; set; }
        public DateTime EarliestSyncDate { get; set; }
        public string LocalStoragePath { get; set; }
    }

    public class ServerConfigurationModel
    {
        public string Protocol { get; set; }
        public string Url { get; set; }
        public string Host { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string Type { get; set; }
    }

    public class TypeMapModel
    {
        public string LeanKitType { get; set; }
        public string TargetType { get; set; }
    }
    public class BoardMappingModel
    {
        public int Id { get; set; }
        public long BoardId { get; set; }
        public string Title { get; set; }
        public string TargetProjectId { get; set; }
        public string TargetProjectName { get; set; }
        public bool UpdateCards { get; set; }
		public bool UpdateCardLanes { get; set; }
        public bool UpdateTargetItems { get; set; }
		public bool CreateCards { get; set; }
		public bool CreateTargetItems { get; set; }
        public Dictionary<long, List<string>> LaneToStatesMap { get; set; }
        public List<TypeMapModel> TypeMap { get; set; } 
        public string Query { get; set; }
        public string IterationPath { get; set; }
        public List<string> QueryStates { get; set; }
        public string Excludes { get; set; }
        public bool TagCardsWithTargetSystemName { get; set; }
    }


    // End

    public class ConfigurationModelMap:IModelMapping
    {
        public void Init()
        {
            Mapper.CreateMap<Configuration, ConfigurationModel>()
				.ForMember(x => x.LeanKit, opt => opt.MapFrom(s => s.LeanKit))
				.ForMember(x => x.Mappings, opt => opt.MapFrom(s => s.Mappings))
                .ForMember(x => x.Settings, opt => opt.ResolveUsing(ConvertToSettings));

            Mapper.CreateMap<ServerConfiguration, ServerConfigurationModel>()
                .ForMember(m => m.Url, opt => opt.Ignore());

            Mapper.CreateMap<WorkItemType, TypeMapModel>()
                .ForMember(m => m.LeanKitType, opt => opt.MapFrom(s => s.LeanKit))
                .ForMember(m => m.TargetType, opt => opt.MapFrom(s => s.Target));

            Mapper.CreateMap<BoardMapping, BoardMappingModel>()
                .ForMember(m => m.Id, opt => opt.MapFrom(s => s.Identity.LeanKit))
                .ForMember(m => m.BoardId, opt => opt.MapFrom(s => s.Identity.LeanKit))
                .ForMember(m => m.TargetProjectId, opt => opt.MapFrom(s => s.Identity.Target))
                .ForMember(m => m.TypeMap, opt=>opt.MapFrom(s=>s.Types))
                .ForMember(m => m.TargetProjectName, opt=>opt.MapFrom(s=>s.Identity.TargetName))
	              .ForMember(m => m.Title, opt => opt.MapFrom(s => s.Identity.LeanKitTitle));

            Mapper.CreateMap<BoardMappingModel, BoardMapping>()
                .ForMember(m => m.Identity, opt => opt.ResolveUsing(board => new Identity { LeanKit = board.BoardId, LeanKitTitle = board.Title, Target = board.TargetProjectId, TargetName = board.TargetProjectName }))
                .ForMember(m => m.Types, opt => opt.ResolveUsing(board => board.TypeMap==null?null:board.TypeMap
                                                                                .Where(item=>!string.IsNullOrEmpty(item.LeanKitType)&& !string.IsNullOrEmpty(item.TargetType))
                                                                                .Select(item => new WorkItemType { LeanKit = item.LeanKitType, Target = item.TargetType })))
                .ForMember(m => m.ExcludedTypeQuery, opt => opt.Ignore())
                .ForMember(m => m.ValidLanes, opt => opt.Ignore())
                .ForMember(m => m.ValidCardTypes, opt => opt.Ignore())
                .ForMember(m => m.ArchiveLaneId, opt => opt.Ignore());

        }

        private ConfigurationSettingsModel ConvertToSettings(Configuration configuration)
        {
            return new ConfigurationSettingsModel
                {
                    EarliestSyncDate = configuration.EarliestSyncDate,
                    LocalStoragePath = configuration.LocalStoragePath,
                    PollingFrequency = configuration.PollingFrequency
                };
        }
    }
}
