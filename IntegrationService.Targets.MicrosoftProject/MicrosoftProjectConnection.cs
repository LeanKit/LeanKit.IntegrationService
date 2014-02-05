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
		protected string FilePath;

        public ConnectionResult Connect(string host, string user, string password)
        {
			string.Format("Connecting to Microsoft Project '{0}'", host).Debug();

			FilePath = host;

			try 
			{
				if (!File.Exists(host))
				{
					string.Format("File does not exist '{0}'", host).Error();
					return ConnectionResult.FailedToConnect;
				}
			} 
			catch (Exception) 
			{
				return ConnectionResult.FailedToConnect;
			}

			return ConnectionResult.Success;
        }

        public List<Project> GetProjects()
        {
			var projects = new List<Project>();

			ProjectReader reader = ProjectReaderUtility.getProjectReader(FilePath);
			ProjectFile mpx = reader.read(FilePath);

			// Get the top level task
			var topTask = (from Task task in mpx.AllTasks.ToIEnumerable()
						 where task.ID.intValue() == 0
						 select task).FirstOrDefault();

			if (topTask == null)
			{
				("No project found in file.").Error();
			}
			else
			{
				// add types and states
				projects.Add(new Project(
								topTask.UniqueID.intValue().ToString(), 
								topTask.Name, 
								GetTaskTypes(mpx), 
								GetStates(mpx)
							));
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