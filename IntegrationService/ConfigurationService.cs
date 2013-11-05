//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Configuration;
using System.Net;
using IntegrationService.Util;
using ServiceStack.Text;

namespace IntegrationService
{
	public class ConfigurationService
	{
		public void Process()
		{
			"Starting AppHost".Print();
			var appHost = new AppHost();
			appHost.Init();

			var urlConfig = ConfigurationManager.AppSettings["ConfigurationSiteUrl"];
			var url = (urlConfig != null && urlConfig.StartsWith("http")) ? urlConfig : "http://+";

			var port = "8090";
			if (ConfigurationManager.AppSettings["ConfigurationSitePort"] != null)
			{
				port = ConfigurationManager.AppSettings["ConfigurationSitePort"];
			}
	
			int portNumber;
			if (!int.TryParse(port, out portNumber))
			{
				string.Format("Invalid ConfigurationSitePort value '{0}', using default value 8090.", port).Warn();
				portNumber = 8090;
			}
			var validPort = false;
			while (!validPort)
			{
				try
				{
					appHost.Start(string.Format("{0}:{1}/", url, portNumber));
					validPort = true;
				}
				catch (HttpListenerException)
				{
					string.Format("HTTP Port {0} not available, trying {1}", portNumber, portNumber + 1).Warn();
					portNumber++;
				}
				catch (Exception e)
				{
					e.Message.Error();
					break;
				}
			}
			
			if (!validPort) return;

			if (url.Equals("http://+", StringComparison.InvariantCultureIgnoreCase)) url = "http://localhost";
			string.Format("Browse to {0}:{1}/ to configure.", url, portNumber).Print();
		}

		public void Shutdown()
		{
			// Things to clean up go here
		}
	}
}
