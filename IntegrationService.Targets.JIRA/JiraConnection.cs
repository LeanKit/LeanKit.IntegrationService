//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using RestSharp;
using ServiceStack.Text;

namespace IntegrationService.Targets.JIRA
{
    public class JiraConnection : IConnection
    {
	    private readonly IRestClient _restClient;

		public JiraConnection()
		{
			_restClient = new RestClient();
		}

		public JiraConnection(IRestClient restClient)
		{
			_restClient = restClient;
		}

        public ConnectionResult Connect(string host, string user, string password)
        {
			_restClient.BaseUrl = host;
			_restClient.Authenticator = new HttpBasicAuthenticator(user, password);

            try
            {
				//https://yoursite.atlassian.net/rest/api/2/serverInfo
				var request = new RestRequest("/rest/api/2/serverInfo", Method.GET);
				var jiraResp = _restClient.Execute(request);

	            if (jiraResp.StatusCode != HttpStatusCode.OK)
	            {
					//var serializer = new JsonSerializer<ErrorMessage>();
					//var errorMessage = serializer.DeserializeFromString(jiraResp.Content);
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
			var issueTypes = new List<Type>();

			//https://yoursite.atlassian.net/rest/api/2/issuetype
			var issueTypeRequest = new RestRequest("/rest/api/2/issuetype", Method.GET);
	        var issueTypeResponse = _restClient.Execute(issueTypeRequest);
			if (issueTypeResponse.StatusCode == HttpStatusCode.OK)
			{
				var jiraIssueTypes = new JsonSerializer<List<IssueType>>().DeserializeFromString(issueTypeResponse.Content);
				if (jiraIssueTypes != null && jiraIssueTypes.Any())
				{
					issueTypes.AddRange(jiraIssueTypes.Select(jiraIssueType => new Type(jiraIssueType.Name)));
				}
			}

			var projects = new List<Project>();

			//https://yoursite.atlassian.net/rest/api/2/project
			var request = new RestRequest("/rest/api/2/project", Method.GET);

			var jiraResp = _restClient.Execute(request);

			if (jiraResp.StatusCode != HttpStatusCode.OK) 
			{
				//var serializer = new JsonSerializer<ErrorMessage>();
				//var errorMessage = serializer.DeserializeFromString(jiraResp.Content);
				return null;
			}

			var resp = new JsonSerializer<List<JiraProject>>().DeserializeFromString(jiraResp.Content);

			if (resp != null && resp.Any()) 
			{
				projects.AddRange(resp.Select(jiraProject => new Project(jiraProject.Key, jiraProject.Name, issueTypes, GetProjectStates(jiraProject.Key))));
			}

	        return projects;
        }

		private List<State> GetProjectStates(string projectKey)
		{
			var states = new SortedList<string, State>();

			//https://yoursite.atlassian.net/rest/api/2/project/{key}/statuses
			var request = new RestRequest(string.Format("/rest/api/2/project/{0}/statuses", projectKey), Method.GET);
			var response = _restClient.Execute(request);
			if (response.StatusCode == HttpStatusCode.OK) 
			{
				var jiraIssueTypes = new JsonSerializer<List<IssueType>>().DeserializeFromString(response.Content);
				if (jiraIssueTypes != null && jiraIssueTypes.Any()) 
				{
					foreach (var jiraIssueType in jiraIssueTypes) 
					{
						if (jiraIssueType.Statuses != null && jiraIssueType.Statuses.Any())
						{
							foreach (var jiraState in jiraIssueType.Statuses)
							{
								if (!states.ContainsKey(jiraState.Name))
									states.Add(jiraState.Name, new State(jiraState.Name));								
							}
						}
					}
				}
			}
			else
			{
				// JIRA 5.x has one list of statuses for all projects
				// http://example.com:8080/jira/rest/api/2/status
				request = new RestRequest("/rest/api/2/status", Method.GET);
				response = _restClient.Execute(request);
				if (response.StatusCode == HttpStatusCode.OK)
				{
					var jiraStates = new JsonSerializer<List<Status>>().DeserializeFromString(response.Content);
					if (jiraStates != null && jiraStates.Any())
					{
						foreach (var jiraStatus in jiraStates)
						{
							if (!states.ContainsKey(jiraStatus.Name))
								states.Add(jiraStatus.Name, new State(jiraStatus.Name));
						}
					}
				}
			}

			return states.Values.ToList();
		}

		public class ProjectsResponse 
		{
			public List<JiraProject> Projects { get; set; }

			public ProjectsResponse() 
			{
				Projects = new List<JiraProject>();
			}
		}

		public class JiraProject
		{
			public string Id { get; set; }
			public string Key { get; set; }
			public string Name { get; set; }

			public JiraProject()
			{
				Id = "";
				Key = "";
				Name = "";
			}
		}

		public class IssueType 
		{
			public string Id { get; set; }
			public string Description { get; set; }
			public string Name { get; set; }
			public List<Status> Statuses { get; set; } 

			public IssueType() 
			{
				Name = "";
				Id = "";
				Description = "";
				Statuses = new List<Status>();
			}
		}

		public class Status 
		{
			public string Description { get; set; }
			public string Name { get; set; }
			public string Id { get; set; }

			public Status() {
				Description = "";
				Name = "";
				Id = "";
			}
		}

		public class ErrorMessage 
		{
			public string Message { get; set; }
		}
    }
}