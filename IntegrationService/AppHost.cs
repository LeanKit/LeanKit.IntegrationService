//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using Funq;
using ServiceStack.Common;
using ServiceStack.Razor;
using ServiceStack.ServiceHost;
using ServiceStack.WebHost.Endpoints;

namespace IntegrationService
{
    public class AppHost : AppHostHttpListenerBase
    {        public AppHost() : base("ConfigHost", typeof(AppHost).Assembly) { }

        public override void Configure(Container container)
        {
            Plugins.Add(new RazorFormat());
            
            SetConfig(new EndpointHostConfig
            {
                EnableFeatures = Feature.All.Remove(Feature.Metadata),
                DefaultRedirectPath = "/site"
            });
        }
    }
}
