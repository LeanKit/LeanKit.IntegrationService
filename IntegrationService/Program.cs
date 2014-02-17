//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Linq;
using ServiceStack.Text;
using Topshelf;

namespace IntegrationService
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var runOnce = args.Any( arg => arg.Equals("-runonce", StringComparison.OrdinalIgnoreCase));

			if (!runOnce)
			{
				HostFactory.Run(x =>
				{
					var configOnly = false;
					x.AddCommandLineDefinition("config", p => configOnly = true);

					x.Service<IntegrationService>(s =>
					{
						s.ConstructUsing(name => new IntegrationService());
						s.WhenStarted((service, host) =>
						{
							if (configOnly)
							{
								service.StartConfigService();
								return true;
							}

							return service.Start(host);
						});
						s.WhenStopped((tc, host) => tc.Stop(host));
					});
					x.RunAsLocalSystem();
					x.EnableShutdown();

					x.SetDescription("LeanKit Integration Service");
					x.SetDisplayName("LeanKit Integration Service");
					x.SetServiceName("LeanKit-Integration-Service");
				});

			}
			else
			{
				"Manually running the integration once...".Print();
				var service = new IntegrationService();
				service.Start(true);
			}
		}
	}
}
