//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.IO;
using System.Reflection;
using IntegrationService.Targets.GitHub;
using IntegrationService.Targets.JIRA;
using IntegrationService.Targets.TFS;
using IntegrationService.Targets.Unfuddle;
using IntegrationService.Targets.MicrosoftProject;
using IntegrationService.Util;
using ServiceStack.ServiceHost;

namespace IntegrationService.API
{
    [Route("/tryconnection")]
    public class ConnectionRequest : Request { }

    [Route("/projects")]
    public class ProjectsRequest : Request { }

	public class TargetService : ServiceBase
	{
		public object Get(ConnectionRequest request)
		{
			var result = ConnectionResult.UnknownTarget;

			if (request.Type == null) return result;

			switch (request.Type.ToLowerInvariant())
			{
				case "tfs":
					string.Format("Connecting to TFS using {0}", request).Debug();
					var tfs = new TfsConnection();
					result = tfs.Connect(request.Protocol, request.Url, request.User, request.Password);
					break;
				case "jira":
					string.Format("Connecting to JIRA using {0}", request).Debug();
					var jira = new JiraConnection();
					result = jira.Connect(request.Protocol, request.Url, request.User, request.Password);
					break;
				case "githubissues":
					string.Format("Connecting to GitHub (Issues) using {0}", request).Debug();
					var githubissues = new GitHubIssuesConnection();
					result = githubissues.Connect(request.Protocol, request.Host, request.User, request.Password);
					break;
				case "githubpulls":
					string.Format("Connecting to GitHub (Pulls) using {0}", request).Debug();
					var githubpulls = new GitHubPullsConnection();
					result = githubpulls.Connect(request.Protocol, request.Host, request.User, request.Password);
					break;
				case "unfuddle":
					string.Format("Connecting to Unfuddle using {0}", request).Debug();
					var unfuddle = new UnfuddleConnection();
					result = unfuddle.Connect(request.Protocol, request.Host, request.User, request.Password);
					break;
				case "microsoftproject":
					string.Format("Connecting to Microsoft Project using {0}", request).Debug();
					var microsoftproject = new MicrosoftProjectConnection();
					result = microsoftproject.Connect(request.Protocol, request.Host, request.User, request.Password);
					break;
			}

			if (result == ConnectionResult.Success)
			{
				"Connection successful.".Debug();
				SaveLogin(request);
				return OK();
			}

			if (result == ConnectionResult.FailedToConnect)
				return NotAuthorized("Credentials Rejected");

			if (result == ConnectionResult.InvalidUrl)
				return BadRequest("Invalid Url");

			return BadRequest("Unknown Target");
		}

		private static void SaveLogin(Request request)
		{
			"Saving Target login information.".Debug();

			var dir = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory;
			if (dir == null) throw new Exception("Could not access application folder.");

			var storagefile = Path.Combine(dir.FullName, "config-edit.json");
			var localStorage = new LocalStorage<Configuration>(storagefile);
			var config = File.Exists(storagefile) ? localStorage.Load() : new Configuration();

			config.Target = new ServerConfiguration
			{
				Type = request.Type,
				Protocol = request.Protocol,
				Host = request.Host,
				User = request.User,
				Password = request.Password
			};

			localStorage.Save(config);
		}

		private static IConnection Connect(Request request, out ConnectionResult result)
		{
			result = ConnectionResult.UnknownTarget;
			if (request.Type == null) return null;

			IConnection target;

			switch (request.Type.ToLowerInvariant())
			{
				case "tfs":
					target = new TfsConnection();
					result = target.Connect(request.Protocol, request.Url, request.User, request.Password);
					break;
				case "jira":
					target = new JiraConnection();
					result = target.Connect(request.Protocol, request.Url, request.User, request.Password);
					break;
				case "githubissues":
					target = new GitHubIssuesConnection();
					result = target.Connect(request.Protocol, request.Host, request.User, request.Password);
					break;
				case "githubpulls":
					target = new GitHubPullsConnection();
					result = target.Connect(request.Protocol, request.Host, request.User, request.Password);
					break;
				case "unfuddle":
					target = new UnfuddleConnection();
					result = target.Connect(request.Protocol, request.Host, request.User, request.Password);
					break;
				case "microsoftproject":
					target = new MicrosoftProjectConnection();
					result = target.Connect(request.Protocol, request.Host, request.User, request.Password);
					break;
				default:
					target = null;
					break;
			}
			return target;
		}

		public object Get(ProjectsRequest request)
		{
			if (request.Type == null) return null;

			ConnectionResult result;
			var target = Connect(request, out result);

			if (result != ConnectionResult.Success)
				return ServerError(result.ToString());

			"Getting list of projects...".Debug();

			var projects = target.GetProjects();

			return OK(projects);
		}
	}
}