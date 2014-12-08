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

namespace IntegrationService.Targets.GitHub
{
    public abstract class GitHubConnection : IConnection
    {
	    protected IRestClient RestClient;
	    protected string Host;

		protected GitHubConnection()
		{
			RestClient = new RestClient();
		}

		protected GitHubConnection(IRestClient restClient)
		{
			RestClient = restClient;
		}

        public ConnectionResult Connect(string host, string user, string password)
        {
	        Host = host;
	        RestClient.BaseUrl = new Uri("https://api.github.com");
			RestClient.Authenticator = new HttpBasicAuthenticator(user, password);
            
			try
            {
				//https://api.github.com/users/[username]/keys
				var request = new RestRequest("/users/" + user +"/keys", Method.GET);
				var githubResp = RestClient.Execute(request);

	            if (githubResp.StatusCode != HttpStatusCode.OK)
	            {
					//var serializer = new JsonSerializer<ErrorMessage>();
					//var errorMessage = serializer.DeserializeFromString(githubResp.Content);
					return ConnectionResult.FailedToConnect;
	            }
            }
            catch (Exception)
            {
                return ConnectionResult.FailedToConnect;
            }

            return ConnectionResult.Success;
        }

	    public abstract List<Project> GetProjects();

		protected class ErrorMessage 
		{
			public string Message { get; set; }
		}

		protected class RepositoryResponse 
		{
			RepositoryResponse()
			{
				Items = new List<Repository>();
			}

			public int Total_Count { get; set; }
			public List<Repository> Items { get; set; }
		}

		protected class Repository 
		{
			public string Id { get; set; }
			public string Name { get; set; }
		}

		protected IRestResponse ReposResponse(int pageNumber, int pageSize) 
		{
			//https://api.github.com/search/repositories?q=@hostname
			var reposRequest = new RestRequest(string.Format("/search/repositories?q=@{0}&page={1}&per_page={2}", Host, pageNumber, pageSize), Method.GET);
			// required for GitHub Search API during the developer preview
			reposRequest.AddHeader("Accept", "application/vnd.github.preview");
			var reposResponse = RestClient.Execute(reposRequest);
			return reposResponse;
		}		
    }

	public class GitHubIssuesConnection : GitHubConnection
	{
		public GitHubIssuesConnection()
		{
			RestClient = new RestClient();
		}

		public GitHubIssuesConnection(IRestClient restClient)
		{
			RestClient = restClient;
		}

		public override List<Project> GetProjects()
		{
			var projects = new List<Project>();

			int pageNumber = 0;
			int pageSize = 100;
			int totalCount = 0;

			do
			{
				pageNumber++;
				var reposResponse = ReposResponse(pageNumber, pageSize);
				if (reposResponse.StatusCode == HttpStatusCode.OK) 
				{
					var repos = new JsonSerializer<RepositoryResponse>().DeserializeFromString(reposResponse.Content);
					if (repos != null)
					{
						if (repos.Items != null && repos.Items.Any())
						{
							projects.AddRange(repos.Items.Select(repo => new Project(repo.Name, repo.Name, GetIssueTypes(), GetStates())));
						}
						totalCount = repos.Total_Count;
					}
					else
					{
						break;
					}
				} else {
					throw new ApplicationException("Error reading projects: " + reposResponse.StatusCode + " - " + reposResponse.StatusDescription + ". " + reposResponse.Content);
				}				
			} while (totalCount > pageNumber * pageSize);

			return projects;
		}

		private List<Type> GetIssueTypes() 
		{
			var types = new List<Type>
				{
					new Type("bug"), 
					new Type("duplicate"), 
					new Type("enhancement"), 
					new Type("invalid"), 
					new Type("question"), 
					new Type("wont fix")
				};
			return types;
		}

		private List<State> GetStates() 
		{
			var states = new List<State>
				{
					new State("open"), 
					new State("closed")
				};
			return states;
		}
	}

	public class GitHubPullsConnection : GitHubConnection 
	{
		public GitHubPullsConnection() { }

		public GitHubPullsConnection(IRestClient restClient) : base (restClient) { }

		public override List<Project> GetProjects() 
		{
			var projects = new List<Project>();

			int pageNumber = 0;
			int pageSize = 100;
			int totalCount = 0;

			do {
				pageNumber++;
				var reposResponse = ReposResponse(pageNumber, pageSize);
				if (reposResponse.StatusCode == HttpStatusCode.OK) {
					var repos = new JsonSerializer<RepositoryResponse>().DeserializeFromString(reposResponse.Content);
					if (repos != null) {
						if (repos.Items != null && repos.Items.Any()) {
							projects.AddRange(repos.Items.Select(repo => new Project(repo.Name, repo.Name, GetIssueTypes(), GetStates())));
						}
						totalCount = repos.Total_Count;
					} else {
						break;
					}
				} else {
					throw new ApplicationException("Error reading projects: " + reposResponse.StatusCode + " - " + reposResponse.StatusDescription + ". " + reposResponse.Content);
				}
			} while (totalCount > pageNumber * pageSize);

			return projects;
		}

		private List<Type> GetIssueTypes() 
		{
			var types = new List<Type>
				{
					new Type("Pull Request")
				};
			return types;
		}

		private List<State> GetStates() 
		{
			var states = new List<State>
				{
					new State("open"), 
					new State("closed")
				};
			return states;
		}
	}
}