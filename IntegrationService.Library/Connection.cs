//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System.Collections.Generic;

namespace IntegrationService 
{
	public interface IConnection
	{
		ConnectionResult Connect(string protocol, string host, string user, string password);
		List<Project> GetProjects();
	}

	public interface IConfigurableFieldsConnection
	{
		List<ConfigurableField> GetConfigurableFields();
	}

	public enum ConnectionResult 
	{
		InvalidUrl = 0,
		FailedToConnect = 1,
		Success = 2,
		UnknownTarget = 3
	}


	public class ConfigurableField
	{
		public string Description { get; set; }
		public List<SyncDirection> SyncDirections { get; set; }
		public SyncDirection DefaultSyncDirection { get; set; }
		public LeanKitField LeanKitField { get; set; }
		public List<TargetField> TargetFields { get; set; } 

		public ConfigurableField()
		{
			LeanKitField = LeanKitField.None;
			Description = "";
			SyncDirections = new List<SyncDirection>(){ SyncDirection.None };
			DefaultSyncDirection = SyncDirection.None;
			TargetFields = new List<TargetField>();
		}

		public ConfigurableField(LeanKitField leanKitField, List<TargetField> targetFields, List<SyncDirection> syncDirections, SyncDirection defaultSyncDirection, string description)
		{
			Description = description;
			SyncDirections = syncDirections;
			DefaultSyncDirection = defaultSyncDirection;
			LeanKitField = leanKitField;
			TargetFields = targetFields;
		}
	}

	public enum SyncDirection
	{
		None, 
		ToLeanKit,
		ToTarget,
		Both
	}

	public class TargetField
	{
		public string Name { get; set; }
		public bool IsDefault { get; set; }
	}

	public enum LeanKitField 
	{
		None,
		CardType,
		ClassOfService,
		IsBlocked,
		BlockedReason,
		ExcludeFromLeankit,
		Tags, 
		StartDate, 
		DueDate, 
		Title, 
		Description, 
		Priority, 
		Size, 
		ExternalId, 
		Id, 
		Permalink
	}

	public class Project 
	{
		public Project(string id, string name) 
		{
			Name = name;
			var pos = id.LastIndexOf('/');
			Id = id.Substring(pos + 1);
		}

		public Project(string id, string name, List<Type> types)
			: this(id, name) 
		{
			Types = types;
		}

		public Project(string id, string name, List<Type> types, List<State> states)
			: this(id, name) 
		{
			Types = types;
			States = states;
		}

		public Project(string id, string name, List<Type> types, List<State> states, List<string> pathFilters )
			: this(id, name) 
		{
			Types = types;
			States = states;
		    PathFilters = pathFilters;
		}

		public string Id { get; set; }
		public string Name { get; set; }
		public List<Type> Types { get; set; }
		public List<State> States { get; set; }
        public List<string> PathFilters { get; set; } 
	}

	public class Type 
	{
		public Type(string name) 
		{
			Name = name;
		}

		public Type(string name, List<State> states)
			: this(name) 
		{
			States = states;
		}

		public string Name { get; set; }
		public List<State> States { get; set; }
	}

	public class State 
	{
		public State(string name) 
		{
			Name = name;
		}

		public string Name { get; set; }
	}
}
