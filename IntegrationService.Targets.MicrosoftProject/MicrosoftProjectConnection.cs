//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IntegrationService.Util;
using net.sf.mpxj;
using net.sf.mpxj.ExtensionMethods;
using net.sf.mpxj.reader;

namespace IntegrationService.Targets.MicrosoftProject
{
    public class MicrosoftProjectConnection : IConnection, IConfigurableFieldsConnection
    {
	    protected string FolderPath;
		protected string File;

        public ConnectionResult Connect(string protocol, string host, string user, string password)
        {
			string.Format("Connecting to Microsoft Project '{0}'", host).Debug();

	        if (protocol.ToLowerInvariant().StartsWith("file"))
	        {
		        FolderPath = host;
	        }

	        if (!string.IsNullOrEmpty(FolderPath))
	        {
		        try
		        {
			        if (!Directory.Exists(host))
			        {
				        string.Format("Folder does not exist '{0}'", host).Error();
				        return ConnectionResult.FailedToConnect;
			        }
		        }
		        catch (Exception)
		        {
			        return ConnectionResult.FailedToConnect;
		        }
	        }
	        else
	        {
		        // connect to project server
				return ConnectionResult.InvalidUrl;
	        }

	        return ConnectionResult.Success;
        }

        public List<Project> GetProjects()
        {
			var projects = new List<Project>();

	        if (!string.IsNullOrEmpty(FolderPath))
	        {
		        foreach (var projectFile in Directory.GetFiles(FolderPath))
		        {
			        if (projectFile.ToLower().EndsWith("mpx")
			            || projectFile.ToLower().EndsWith("mpp")
			            || projectFile.ToLower().EndsWith("mpt"))
			        {
						string projectFileName = projectFile.Substring(FolderPath.Length + 1);

						ProjectReader reader = ProjectReaderUtility.getProjectReader(projectFile);
						ProjectFile mpx = reader.read(projectFile);

						// Get the top level task
						var topTask = (from Task task in mpx.AllTasks.ToIEnumerable()
									   where task.ID.intValue() == 0
									   select task).FirstOrDefault();

						if (topTask == null) {
							string.Format("No project found in file: '{0}'.", projectFile).Debug();
						} else {
							// add types and states
							projects.Add(new Project(
											 projectFileName,
											 projectFileName,
											 GetTaskTypes(mpx),
											 GetStates(mpx)
											 ));
						}				        
			        }
		        }
	        }
	        else
	        {
		        // connect to project server
	        }

	        return projects;
        }		

		public List<ConfigurableField> GetConfigurableFields()
		{
			var fields = new List<ConfigurableField>();


			#region From Target to LeanKit
			fields.Add(new ConfigurableField(LeanKitField.Title,
										 new List<TargetField>() { new TargetField() { Name = "Name", IsDefault = true } },
										 SyncDirection.ToLeanKit,
										 ""));
			fields.Add(new ConfigurableField(LeanKitField.Description,
										 new List<TargetField>() { new TargetField() { Name = "Notes", IsDefault = true } },
										 SyncDirection.ToLeanKit,
										 ""));
			fields.Add(new ConfigurableField(LeanKitField.ExternalId, 
										 new List<TargetField>() { new TargetField() { Name = "UniqueId", IsDefault = true } },
										 SyncDirection.ToLeanKit, 
										 ""));

			fields.Add(new ConfigurableField(LeanKitField.ExcludeFromLeankit,
										 GetAllTextFields(6),
										 SyncDirection.ToLeanKit,
										 "Exclude a task from being imported into LeanKit. Value should be 'Yes' to exclude."));
			fields.Add(new ConfigurableField(LeanKitField.StartDate,
			                             new List<TargetField>()
											{
												new TargetField() {Name = "BaselineStart", IsDefault = false},
					                            new TargetField() {Name = "Start", IsDefault = true},
					                            new TargetField() {Name = "EarlyStart", IsDefault = false}
				                            },
										 SyncDirection.ToLeanKit,
										 "Select the Project field to use as the card's StartDate."));
			fields.Add(new ConfigurableField(LeanKitField.DueDate,
										 new List<TargetField>()
				                            {
					                            new TargetField() {Name = "BaselineFinish", IsDefault = false},
					                            new TargetField() {Name = "Finish", IsDefault = true},
					                            new TargetField() {Name = "EarlyFinish", IsDefault = false}
				                            },
										 SyncDirection.ToLeanKit,
										 "Select the Project field to use as the card's DueDate."));
			fields.Add(new ConfigurableField(LeanKitField.CardType, 
										 GetAllTextFields(1),
										 SyncDirection.ToLeanKit, 
										 "To create a card of a specific type. Must match a card type name i.e. Task, Defect, etc."));
			fields.Add(new ConfigurableField(LeanKitField.Priority,
										 new List<TargetField>() { new TargetField() { Name = "Priority", IsDefault = true } },
										 SyncDirection.ToLeanKit, 
										 ""));
			fields.Add(new ConfigurableField(LeanKitField.Size,
										 new List<TargetField>()
				                            {
												new TargetField() {Name = "None", IsDefault = true },
					                            new TargetField() {Name = "BaselineWork", IsDefault = false},
					                            new TargetField() {Name = "Work", IsDefault = false},
					                            new TargetField() {Name = "BaselineCost", IsDefault = false},
											    new TargetField() {Name = "Cost", IsDefault = false}
				                            },
										 SyncDirection.ToLeanKit, 
										 "Select the Project field to use as the card's Size."));
			fields.Add(new ConfigurableField(LeanKitField.ClassOfService, 
										 GetAllTextFields(2), 
										 SyncDirection.ToLeanKit, 
										 "To assign a class of service to the card."));			
			fields.Add(new ConfigurableField(LeanKitField.IsBlocked, 
										 GetAllTextFields(5), 
										 SyncDirection.ToLeanKit, 
										 "To mark the card as blocked. Value should be 'Yes' to be blocked"));
			fields.Add(new ConfigurableField(LeanKitField.BlockedReason, 
										 GetAllTextFields(3), 
										 SyncDirection.ToLeanKit, 
										 "To provide a reason the card is blocked."));
			fields.Add(new ConfigurableField(LeanKitField.Tags,
										 GetAllTextFields(4),
										 SyncDirection.ToLeanKit,
										 "A Project field(s) containing a comma separated list of tags to apply to the card."));
			#endregion
			#region From LeanKit to Target 

			fields.Add(new ConfigurableField(LeanKitField.Id,
										GetAllTextFields(0),
										SyncDirection.ToTarget,
										""));
			fields.Add(new ConfigurableField(LeanKitField.Title,
										new List<TargetField>()
											{
												new TargetField() { Name = "None", IsDefault = true },
												new TargetField() { Name = "Name", IsDefault = false }, 
											},
										SyncDirection.ToTarget,
										""));
			fields.Add(new ConfigurableField(LeanKitField.Description,
										new List<TargetField>()
											{
												new TargetField() { Name = "None", IsDefault = true },
												new TargetField() { Name = "Notes", IsDefault = false },
											},
										SyncDirection.ToTarget,
										""));
			fields.Add(new ConfigurableField(LeanKitField.StartDate,
										 new List<TargetField>()
											{
												new TargetField() {Name = "None", IsDefault = true},
												new TargetField() {Name = "BaselineStart", IsDefault = false},
					                            new TargetField() {Name = "Start", IsDefault = false},
					                            new TargetField() {Name = "EarlyStart", IsDefault = false}
				                            },
										 SyncDirection.ToTarget,
										 ""));
			fields.Add(new ConfigurableField(LeanKitField.DueDate,
										 new List<TargetField>()
				                            {
												new TargetField() {Name = "None", IsDefault = true},
					                            new TargetField() {Name = "BaselineFinish", IsDefault = false},
					                            new TargetField() {Name = "Finish", IsDefault = false},
					                            new TargetField() {Name = "EarlyFinish", IsDefault = false}
				                            },
										 SyncDirection.ToTarget,
										 ""));
			fields.Add(new ConfigurableField(LeanKitField.CardType,
										 GetAllTextFields(0),
										 SyncDirection.ToTarget,
										 ""));
			fields.Add(new ConfigurableField(LeanKitField.Priority,
										 new List<TargetField>()
											 {
												 new TargetField() {Name = "None", IsDefault = true},
												 new TargetField() { Name = "Priority", IsDefault = false }
											 },
										 SyncDirection.ToTarget,
										 ""));
			fields.Add(new ConfigurableField(LeanKitField.Size,
										 new List<TargetField>()
				                            {
												new TargetField() {Name = "None", IsDefault = true },
					                            new TargetField() {Name = "BaselineWork", IsDefault = false},
					                            new TargetField() {Name = "Work", IsDefault = false},
					                            new TargetField() {Name = "BaselineCost", IsDefault = false},
											    new TargetField() {Name = "Cost", IsDefault = false}
				                            },
										 SyncDirection.ToTarget,
										 "Select the Project field to use as the card's Size."));
			fields.Add(new ConfigurableField(LeanKitField.IsBlocked,
										 GetAllTextFields(0),
										 SyncDirection.ToTarget,
										 ""));
			fields.Add(new ConfigurableField(LeanKitField.BlockedReason,
										 GetAllTextFields(0),
										 SyncDirection.ToTarget,
										 ""));
			fields.Add(new ConfigurableField(LeanKitField.Tags,
										 GetAllTextFields(0),
										 SyncDirection.ToTarget,
										 ""));
			#endregion

			return fields;
		}

		private List<TargetField> GetAllTextFields(int defaultValue = 0)
		{
			var fields = new List<TargetField>();
			fields.Add(new TargetField() { Name = "None", IsDefault = (0 == defaultValue) });
			for (int i = 1; i <= 20; i++)
			{
				fields.Add(new TargetField() { Name = "Text" + i, IsDefault = (i == defaultValue)});				
			}
			return fields;
		}

		private List<Type> GetTaskTypes(ProjectFile mpx)
		{
			var taskTypes = new List<Type>();

			var projTaskTypes = (from Task task in mpx.AllTasks.ToIEnumerable<Task>()
			                 select task.GetText(1))
							 .Where(x => !string.IsNullOrEmpty(x))
							 .Distinct()
							 .ToList();

			if (projTaskTypes.Any())
			{
				taskTypes.AddRange(projTaskTypes.Select(projTaskType => new Type(projTaskType)));
			}

			return taskTypes;
		}

		private List<State> GetStates(ProjectFile mpx)
		{
			return new List<State>()
				{
					new State("All Tasks")
				};

			//return new List<State>()
			//	{
			//		new State("Ready"),
			//		new State("In Process"),
			//		new State("Completed")
			//	};
		}
    }
}