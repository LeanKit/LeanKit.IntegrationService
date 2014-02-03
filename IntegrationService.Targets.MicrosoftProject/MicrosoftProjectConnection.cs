//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace IntegrationService.Targets.MicrosoftProject
{
    public class MicrosoftProjectConnection : IConnection
    {
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