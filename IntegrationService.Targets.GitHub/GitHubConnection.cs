//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using IntegrationService.Util;
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

        public ConnectionResult Connect(string protocol, string host, string user, string password)
        {
			if (protocol.ToLowerInvariant().StartsWith("file")) 
			{
				string.Format("GitHub integration cannot use a file datasource '{0}'.", host).Error();
				return ConnectionResult.InvalidUrl;
			}

	        Host = host;
	        RestClient.BaseUrl = "https://api.github.com";
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

			public List<Repository> Items { get; set; }
		}

		protected class Repository 
		{
			public string Id { get; set; }
			public string Name { get; set; }
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

			//https://api.github.com/search/repositories?q=@hostname
			var reposRequest = new RestRequest("/search/repositories?q=@" + Host, Method.GET);
			// required for GitHub Search API during the developer preview
			reposRequest.AddHeader("Accept", "application/vnd.github.preview");
			var reposResponse = RestClient.Execute(reposRequest);
			if (reposResponse.StatusCode == HttpStatusCode.OK) 
			{
				var repos = new JsonSerializer<RepositoryResponse>().DeserializeFromString(reposResponse.Content);
				if (repos != null && repos.Items != null && repos.Items.Any()) 
				{
					projects.AddRange(repos.Items.Select(repo => new Project(repo.Name, repo.Name, GetIssueTypes(), GetStates())));
				}
			}

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

			//https://api.github.com/search/repositories?q=@hostname
			var reposRequest = new RestRequest("/search/repositories?q=@" + Host, Method.GET);
			// required for GitHub Search API during the developer preview
			reposRequest.AddHeader("Accept", "application/vnd.github.preview");
			var reposResponse = RestClient.Execute(reposRequest);
			if (reposResponse.StatusCode == HttpStatusCode.OK) 
			{
				var repos = new JsonSerializer<RepositoryResponse>().DeserializeFromString(reposResponse.Content);
				if (repos != null && repos.Items != null && repos.Items.Any()) 
				{
					projects.AddRange(repos.Items.Select(repo => new Project(repo.Name, repo.Name, GetIssueTypes(), GetStates())));
				}
			}

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