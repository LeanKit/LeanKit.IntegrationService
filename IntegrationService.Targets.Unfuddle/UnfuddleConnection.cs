//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using RestSharp;

namespace IntegrationService.Targets.Unfuddle
{
    public class UnfuddleConnection : IConnection
    {
	    private readonly IRestClient _restClient;

		public UnfuddleConnection()
		{
			_restClient = new RestClient();
		}

		public UnfuddleConnection(IRestClient restClient)
		{
			_restClient = restClient;
		}

        public ConnectionResult Connect(string host, string user, string password)
        {
			throw new NotImplementedException();
        }

        public List<Project> GetProjects()
        {
			throw new NotImplementedException();
        }		
    }
}