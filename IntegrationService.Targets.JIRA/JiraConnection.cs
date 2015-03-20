//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.Linq;
using System.Net;
using IntegrationService.Util;
using RestSharp;
using ServiceStack.Text;

namespace IntegrationService.Targets.JIRA
{
    public class JiraConnection : IConnection
    {
	    private readonly IRestClient _restClient;
	    private Dictionary<string, string> _sessionCookies;

		public JiraConnection()
		{
			_restClient = new RestClient();
			_sessionCookies = new Dictionary<string, string>();
		}

		public JiraConnection(IRestClient restClient)
		{
			_restClient = restClient;
			_sessionCookies = new Dictionary<string, string>();
		}

        public ConnectionResult Connect(string host, string user, string password)
        {
			_restClient.BaseUrl = new Uri(host);
			RefreshSessionCookie(host, user, password);
			return _sessionCookies.Keys.Count == 0 ? ConnectionResult.FailedToConnect : ConnectionResult.Success;

			//_restClient.BaseUrl = new Uri(host);
			//// _restClient.Authenticator = new HttpBasicAuthenticator(user, password);

			//try
			//{
			//	//https://yoursite.atlassian.net/rest/api/2/serverInfo
			//	var request = CreateRequest("rest/api/2/serverInfo", Method.GET);
			//	var jiraResp = ExecuteRequest(request);

			//	string.Format("Connection Status: {0}", jiraResp.StatusCode).Debug();
			//	string.Format("Response: {0}", jiraResp.Content).Debug();
			//	if (jiraResp.StatusCode != HttpStatusCode.OK)
			//	{
			//		//var serializer = new JsonSerializer<ErrorMessage>();
			//		//var errorMessage = serializer.DeserializeFromString(jiraResp.Content);
			//		return ConnectionResult.FailedToConnect;
			//	}
			//}
			//catch (Exception ex)
			//{
			//	"Error connecting to JIRA.".Error(ex);
			//	return ConnectionResult.FailedToConnect;
			//}

			//return ConnectionResult.Success;
        }

	    public void AddSessionCookieToRequest(RestRequest request)
	    {
		    foreach (var k in _sessionCookies.Keys)
			    request.AddCookie(k, _sessionCookies[k]);
	    }

	    private void RefreshSessionCookie(string host, string user, string password)
	    {
			_sessionCookies = GetSessionCookie(host, user, password);
	    }

	    private RestRequest CreateRequest(string resource, Method method)
	    {
			var request = new RestRequest(resource, method);
			AddSessionCookieToRequest(request);
			request.RequestFormat = DataFormat.Json;
			
		    return request;
	    }

	    private IRestResponse ExecuteRequest(RestRequest request)
	    {
		    request.Debug(_restClient);
		    var response = _restClient.Execute(request);
		    if (response.StatusCode == HttpStatusCode.Unauthorized) _sessionCookies.Clear();
		    return response;
	    }

	    public static Dictionary<string, string> GetSessionCookie(string host, string user, string password)
	    {
		    var sessionCookies = new Dictionary<string, string>();
		    try
		    {
			    var restClient = new RestClient(host);
				var request = new RestRequest("rest/auth/1/session", Method.POST);
				request.AddJsonBody(new { username = user, password = password });
				var response = restClient.Execute(request);
			    if (response.StatusCode != HttpStatusCode.OK)
			    {
					string.Format("Error connecting to {0}{1}", restClient.BaseUrl, request.Resource).Error();
					if (response.Content != null) response.Content.Error();
				    return null;
			    };
				foreach(var c in response.Cookies)
					sessionCookies.Add(c.Name, c.Value);
		    }
		    catch (Exception ex)
		    {
				"Error getting session using rest/auth/1/session.".Error(ex);
		    }
			return sessionCookies;
	    }

        public List<Project> GetProjects()
        {
			var issueTypes = new List<Type>();
            var projects = new List<Project>();

            try
            {
                "Getting a list of issue types from JIRA".Debug();
                //https://yoursite.atlassian.net/rest/api/2/issuetype
				var issueTypeRequest = CreateRequest("rest/api/2/issuetype", Method.GET);
				var issueTypeResponse = ExecuteRequest(issueTypeRequest);
                if (issueTypeResponse.StatusCode == HttpStatusCode.OK)
                {
                    "JIRA issue types retrieved. Deserializing results.".Debug();
                    var jiraIssueTypes =
                        new JsonSerializer<List<IssueType>>().DeserializeFromString(issueTypeResponse.Content);
                    if (jiraIssueTypes != null && jiraIssueTypes.Any())
                    {
                        issueTypes.AddRange(jiraIssueTypes.Select(jiraIssueType => new Type(jiraIssueType.Name)));
                    }
                }

                "Getting projects from JIRA".Debug();

                //https://yoursite.atlassian.net/rest/api/2/project
                var request = CreateRequest("rest/api/2/project", Method.GET);
                var jiraResp = ExecuteRequest(request);

                if (jiraResp.StatusCode != HttpStatusCode.OK)
                {
                    string.Format("Failed to get projects from JIRA. {0}: {1}", jiraResp.StatusCode, jiraResp.ErrorMessage ?? string.Empty).Warn();
                    //var serializer = new JsonSerializer<ErrorMessage>();
                    //var errorMessage = serializer.DeserializeFromString(jiraResp.Content);
                    return projects;
                }

                "JIRA projects retrieved. Deserializing results.".Debug();
                var resp = new JsonSerializer<List<JiraProject>>().DeserializeFromString(jiraResp.Content);

	            if (resp != null && resp.Any())
	            {
		            projects.AddRange(
			            resp.Select(
				            jiraProject =>
					            new Project(jiraProject.Key, jiraProject.Name, issueTypes,
						            GetProjectStates(jiraProject.Key))));
	            }
	            else
	            {
		            "No JIRA projects were retrieved. Please check account access to projects.".Error();
	            }
            }
            catch (Exception ex)
            {
                "Error getting JIRA projects.".Error(ex);
            }
            return projects;
        }

		private List<State> GetProjectStates(string projectKey)
		{
            "Getting JIRA project states.".Debug();

            var states = new SortedList<string, State>();

            try
		    {
		        //https://yoursite.atlassian.net/rest/api/2/project/{key}/statuses
		        var request = CreateRequest(string.Format("rest/api/2/project/{0}/statuses", projectKey), Method.GET);
				var response = ExecuteRequest(request);
		        if (response.StatusCode == HttpStatusCode.OK)
		        {
                    "Retrieved project states, deserializing.".Debug();

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
                    "First attempt to retrieve states failed. Retrying with JIRA 5.x API.".Debug();
                    
                    // JIRA 5.x has one list of statuses for all projects
		            // http://example.com:8080/jira/rest/api/2/status
		            request = CreateRequest("rest/api/2/status", Method.GET);
		            response = ExecuteRequest(request);
		            if (response.StatusCode == HttpStatusCode.OK)
		            {
                        "Retrieved project states, deserializing.".Debug();

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
		    }
		    catch (Exception ex)
		    {
		        "Error getting JIRA project states.".Error(ex);
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