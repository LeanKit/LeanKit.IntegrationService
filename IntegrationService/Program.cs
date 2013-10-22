//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using Topshelf;

namespace IntegrationService
{
	public class Program
	{
		public static void Main()
		{
			HostFactory.Run(x =>
				{
					var configOnly = false;
					x.AddCommandLineSwitch("config", p => configOnly = true);
					x.Service<IntegrationService>(s =>
						{
							s.ConstructUsing(name => new IntegrationService());
							s.WhenStarted(tc =>
								{
									if (configOnly)
										tc.StartConfigService();
									else
										tc.Start();
								});
							s.WhenStopped(tc => tc.Stop());
						});
					x.RunAsLocalSystem();

					x.SetDescription("LeanKit Integration Service");
					x.SetDisplayName("LeanKit Integration Service");
					x.SetServiceName("LeanKit-Integration-Service");
				});
		}
	}
}
