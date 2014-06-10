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
using Microsoft.ProjectServer.Client;
using Microsoft.SharePoint.Client;
using Wictor.Office365;
using net.sf.mpxj;
using net.sf.mpxj.ExtensionMethods;
using net.sf.mpxj.reader;
using Task = net.sf.mpxj.Task;

namespace IntegrationService.Targets.MicrosoftProject
{
    public class MicrosoftProjectConnection : IConnection, IConfigurableConnection
    {
	    protected string FolderPath;
	    protected string FilePath;
		protected string File;
        protected string ProjectServerUrl;
        protected MsOnlineClaimsHelper ClaimsHelper;

        public ConnectionResult Connect(string protocol, string host, string user, string password)
        {
			string.Format("Connecting to Microsoft Project '{0}'", host).Debug();

	        if (protocol.ToLowerInvariant().StartsWith("folder path")) {
		        FolderPath = host;
	        } else if (protocol.ToLowerInvariant().StartsWith("file")) {
		        FilePath = host;
	        }

	        if (!string.IsNullOrEmpty(FolderPath))
	        {
		        try
		        {
			        if (!Directory.Exists(FolderPath))
			        {
				        string.Format("Folder does not exist '{0}'", FolderPath).Error();
				        return ConnectionResult.FailedToConnect;
			        }
		        }
		        catch (Exception)
		        {
			        return ConnectionResult.FailedToConnect;
		        }
	        }
			else if (!string.IsNullOrEmpty(FilePath))
			{
				try
				{
					if (!System.IO.File.Exists(FilePath))
					{
						string.Format("File does not exist '{0}'", FilePath).Error();
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
                if (string.IsNullOrEmpty(host))
                    return ConnectionResult.InvalidUrl;
			    try
			    {
			        ProjectServerUrl = protocol + host;
			        ClaimsHelper = new MsOnlineClaimsHelper(ProjectServerUrl, user, password);
			        using (ClientContext context = new ClientContext(ProjectServerUrl))
			        {
			            context.ExecutingWebRequest += ClaimsHelper.clientContext_ExecutingWebRequest;
			            context.Load(context.Web);
			            context.ExecuteQuery();
			            //Console.WriteLine("Name of the web is: " + context.Web.Title);
			        }
			    }
			    catch (Exception ex)
			    {
                    ex.Message.Debug();
			        return ConnectionResult.FailedToConnect;
			    }
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
			else if (!string.IsNullOrEmpty(FilePath)) 
			{
		        ProjectReader reader = ProjectReaderUtility.getProjectReader(FilePath);
		        ProjectFile mpx = reader.read(FilePath);

		        // Get the top level task
		        var topTask = (from Task task in mpx.AllTasks.ToIEnumerable()
		                       where task.ID.intValue() == 0
		                       select task).FirstOrDefault();

		        if (topTask == null)
		        {
			        string.Format("No project found in file: '{0}'.", FilePath).Debug();
		        }
		        else
		        {
			        // add types and states
			        projects.Add(new Project(
									 topTask.Name,
								     topTask.Name,
				                     GetTaskTypes(mpx),
				                     GetStates(mpx)
				                     ));
		        }
	        }
	        else if (!string.IsNullOrEmpty(ProjectServerUrl) && ClaimsHelper != null)
	        {
                using (ProjectContext projContext = new ProjectContext(ProjectServerUrl))
                {
                    projContext.ExecutingWebRequest += ClaimsHelper.clientContext_ExecutingWebRequest;

                    // Get the list of published projects in Project Web App.
                    projContext.Load(projContext.Projects);
                    projContext.ExecuteQuery();

                    foreach (PublishedProject pubProj in projContext.Projects)
                    {
                        projects.Add(new Project(pubProj.Id.ToString(), pubProj.Name));
                        //Console.WriteLine("\n\t{0}\n\t{1} : {2}", pubProj.Id.ToString(), pubProj.Name, pubProj.CreatedDate.ToString());
                    }                    
                }
	        }

	        return projects;
        }		

		public List<ConfigurableField> GetConfigurableFields()
		{
			var fields = new List<ConfigurableField>();

//			fields.Add(new ConfigurableField(LeanKitField.Id,
//										 GetAllTextFields(),
//										 GetSyncDirections(true, false, true, false),
//										 SyncDirection.None, 
//										 "", 
//										 false));
			fields.Add(new ConfigurableField(LeanKitField.Title,
										 new List<TargetField>() { new TargetField() { Name = "Name", IsDefault = true } },
										 GetSyncDirections(false, true, false, true),
										 SyncDirection.ToLeanKit, 
										 "", 
										 true));
			fields.Add(new ConfigurableField(LeanKitField.Description,
										 new List<TargetField>() { new TargetField() { Name = "Notes", IsDefault = true } },
										 GetSyncDirections(true, true, true, true),
										 SyncDirection.ToLeanKit, 
										 "", 
										 true));
			fields.Add(new ConfigurableField(LeanKitField.ExternalId, 
										 new List<TargetField>() { new TargetField() { Name = "UniqueId", IsDefault = true } },
										 GetSyncDirections(false, true, false, false),
										 SyncDirection.ToLeanKit, 
										 "", 
										 true));			
			fields.Add(new ConfigurableField(LeanKitField.StartDate,
			                             new List<TargetField>()
											{
												new TargetField() {Name = "BaselineStart", IsDefault = false},
					                            new TargetField() {Name = "Start", IsDefault = true},
					                            new TargetField() {Name = "EarlyStart", IsDefault = false}
				                            },
										 GetSyncDirections(false, true, false, true),
										 SyncDirection.ToLeanKit, 
										 "Select the Project field to use as the card's StartDate.", 
										 true));
			fields.Add(new ConfigurableField(LeanKitField.DueDate,
										 new List<TargetField>()
				                            {
					                            new TargetField() {Name = "BaselineFinish", IsDefault = false},
					                            new TargetField() {Name = "Finish", IsDefault = true},
					                            new TargetField() {Name = "EarlyFinish", IsDefault = false}
				                            },
										 GetSyncDirections(true, true, true, true),
										 SyncDirection.ToLeanKit, 
										 "Select the Project field to use as the card's DueDate.", 
										 true));
			fields.Add(new ConfigurableField(LeanKitField.CardType, 
										 GetAllTextFields(),
										 GetSyncDirections(true, true, true, true),
										 SyncDirection.None, 
										 "To create a card of a specific type. Must match a card type name i.e. Task, Defect, etc.", 
										 false));
			fields.Add(new ConfigurableField(LeanKitField.Priority,
										 new List<TargetField>() { new TargetField() { Name = "Priority", IsDefault = true } },
										 GetSyncDirections(true, true, true, true),
										 SyncDirection.ToLeanKit, 
										 "", 
										 true));
			fields.Add(new ConfigurableField(LeanKitField.Size,
										 new List<TargetField>()
				                            {
												new TargetField() {Name = "None", IsDefault = true },
					                            new TargetField() {Name = "BaselineWork", IsDefault = false},
					                            new TargetField() {Name = "Work", IsDefault = false},
					                            new TargetField() {Name = "BaselineCost", IsDefault = false},
											    new TargetField() {Name = "Cost", IsDefault = false}
				                            },
										 GetSyncDirections(true, true, true, true),
										 SyncDirection.None, 
										 "Select the Project field to use as the card's Size.", 
										 false));
			fields.Add(new ConfigurableField(LeanKitField.ClassOfService, 
										 GetAllTextFields(),
										 GetSyncDirections(true, true, true, true),
										 SyncDirection.None, 
										 "To assign a class of service to the card.", 
										 false));			
			fields.Add(new ConfigurableField(LeanKitField.IsBlocked, 
										 GetAllTextFields(),
										 GetSyncDirections(true, true, true, true),
										 SyncDirection.None, 
										 "To mark the card as blocked. Value should be 'Yes' to be blocked", 
										 false));
			fields.Add(new ConfigurableField(LeanKitField.BlockedReason, 
										 GetAllTextFields(),
										 GetSyncDirections(true, true, true, true),
										 SyncDirection.None, 
										 "To provide a reason the card is blocked.", 
										 false));
			fields.Add(new ConfigurableField(LeanKitField.Tags,
										 GetAllTextFields(),
										 GetSyncDirections(true, true, true, true),
										 SyncDirection.None, 
										 "A Project field(s) containing a comma separated list of tags to apply to the card.", 
										 false));
//			var dateArchivedTargetFields = GetAllTextFields();
//			dateArchivedTargetFields.Add(new TargetField() {IsDefault = false, Name = "ActualFinish"});
//			fields.Add(new ConfigurableField(LeanKitField.DateArchived,
//										 dateArchivedTargetFields,
//										 GetSyncDirections(true, false, true, false),
//										 SyncDirection.None,
//										 "The date the card is moved to the Archive, or considered finished.", 
//										 false));
			return fields;
		}

		public List<string> GetFilterFields()
		{
			return GetAllTextFields(false).Select(x => x.Name).ToList();
		}

		public List<Type> GetTaskTypes(string project, string field) 
		{
			var taskTypes = new List<Type>();

			if (!string.IsNullOrEmpty(FolderPath))
			{
				var projectFile = Path.Combine(FolderPath, project);
				if (System.IO.File.Exists(projectFile))
				{
					ProjectReader reader = ProjectReaderUtility.getProjectReader(projectFile);
					ProjectFile mpx = reader.read(projectFile);

					// add types and states
					var projTaskTypes = (from Task task in mpx.AllTasks.ToIEnumerable<Task>()
										 select task.GetText(field))
										 .Where(x => !string.IsNullOrEmpty(x))
										 .Distinct()
										 .ToList();

					if (projTaskTypes.Any()) 
					{
						taskTypes.AddRange(projTaskTypes.Select(projTaskType => new Type(projTaskType)));
					}
				}								
			}
			else if (!string.IsNullOrEmpty(FilePath)) 
			{
				if (System.IO.File.Exists(FilePath)) 
				{
					ProjectReader reader = ProjectReaderUtility.getProjectReader(FilePath);
					ProjectFile mpx = reader.read(FilePath);

					// add types and states
					var projTaskTypes = (from Task task in mpx.AllTasks.ToIEnumerable<Task>()
										 select task.GetText(field))
										 .Where(x => !string.IsNullOrEmpty(x))
										 .Distinct()
										 .ToList();

					if (projTaskTypes.Any()) {
						taskTypes.AddRange(projTaskTypes.Select(projTaskType => new Type(projTaskType)));
					}
				}
			}
            else if (!string.IsNullOrEmpty(ProjectServerUrl))
            {
                // get task types from Project Server
            }
			return taskTypes;
		}


		private List<TargetField> GetAllTextFields(bool includeNone = true, int defaultValue = 0)
		{
			var fields = new List<TargetField>();
			if (includeNone)
			{
				fields.Add(new TargetField() {Name = "None", IsDefault = (0 == defaultValue)});
			}
			for (int i = 1; i <= 20; i++)
			{
				fields.Add(new TargetField() { Name = "Text" + i, IsDefault = (i == defaultValue)});				
			}
			return fields;
		}

		private List<Type> GetTaskTypes(ProjectFile mpx)
		{
			return new List<Type>();
		}

		private List<SyncDirection> GetSyncDirections(bool includeNone, 
													  bool includeToLeankit, 
													  bool includeToTarget,
		                                              bool includeBoth)
		{
			var syncDirections = new List<SyncDirection>();

			if (includeNone)
				syncDirections.Add(SyncDirection.None);
			if (includeToLeankit)
				syncDirections.Add(SyncDirection.ToLeanKit);
//			if (includeToTarget)
//				syncDirections.Add(SyncDirection.ToTarget);
//			if (includeBoth)
//				syncDirections.Add(SyncDirection.Both);

			return syncDirections;
		}

		private List<State> GetStates(ProjectFile mpx)
		{
			return new List<State>();
		}
    }
}