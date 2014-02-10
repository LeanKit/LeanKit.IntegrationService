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
    public class MicrosoftProjectConnection : IConnection
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