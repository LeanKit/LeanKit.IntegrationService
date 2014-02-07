//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using IntegrationService.Targets;
using IntegrationService.Util;
using ServiceStack.Text;
using Topshelf;

namespace IntegrationService
{
	public class IntegrationService : ServiceControl
	{
		private IBoardSubscriptionManager _subscriptions = new BoardSubscriptionManager();
		private TargetBase _target;
		private ConfigurationService _configurationService;
		public static IntegrationService Instance { get; private set; }
		private bool _runOnce;
		private HostControl _hostControl;

		public IntegrationService() { Instance = this; }

		public void Start(bool runOnce) { Start(null, runOnce); }

		public bool Start(HostControl hostControl) { return Start(hostControl, false); }

		public bool Start(HostControl hostControl, bool runOnce)
		{
			_hostControl = hostControl;
			_runOnce = runOnce;
			var types = GetAllTypes();

			// model mapping configuration
			var mappings = types.Where(x => x.IsClass && typeof (IModelMapping).IsAssignableFrom(x));

			foreach (var mapping in mappings)
			{
				var inst = (IModelMapping) Activator.CreateInstance(mapping);
				inst.Init();
			}

			_configurationService = (ConfigurationService) Activator.CreateInstance(typeof (ConfigurationService));
			
			if (!runOnce) new Thread(_configurationService.Process).Start();

			StartIntegration(types);

			return true;
		}

		private static List<System.Type> GetAllTypes()
		{
			var assemblies = Assembly.GetExecutingAssembly().GetReferencedAssemblies();
			var types = new List<System.Type>();
			foreach (var assemblyName in assemblies)
			{
				var assembly = Assembly.Load(assemblyName);
				types.AddRange(assembly.GetTypes());
			}
			types.AddRange(Assembly.GetExecutingAssembly().GetTypes());
			return types;
		}

		public static void Reset()
		{
			if (Instance == null)
			{
				("The IntegrationService has not been initialized.").Print();
				return;
			}

			Instance.Stop(null);

			// copy new config file
			var dir = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory;
			if (dir != null)
			{
				var curFolder = dir.FullName;
				var src = Path.Combine(curFolder, "config-edit.json");
				var dest = Path.Combine(curFolder, "config-live.json");
				if (File.Exists(src))
					File.Copy(src, dest, true);
			}

			var types = GetAllTypes();
			Instance.StartIntegration(types);
		}

		public bool Stop(HostControl hostControl)
		{
			if (_target != null)
			{
				_target.Shutdown();
				_target = null;
			}
			_subscriptions.Shutdown();
			_subscriptions = new BoardSubscriptionManager();
			return true;
		}

		public void StartConfigService()
		{
			var types = Assembly.GetExecutingAssembly().GetTypes();

			// model mapping configuration
			var mappings = types.Where(x => x.IsClass && typeof (IModelMapping).IsAssignableFrom(x));

			foreach (var mapping in mappings)
			{
				var inst = (IModelMapping) Activator.CreateInstance(mapping);
				inst.Init();
			}

			_configurationService = (ConfigurationService) Activator.CreateInstance(typeof (ConfigurationService));
			new Thread(_configurationService.Process).Start();
		}

		private void StartIntegration(IEnumerable<System.Type> types)
		{
			const string noConfigMessage = "\n\nThis Service has not been configured or the configuration has changed.\n\nUse the Configuration Utility to configure and activate Integrations.\n\n";

			var configuration = LoadConfiguration();

			// pick correct implementation class for specified target type
			if (configuration != null && configuration.Target != null && !string.IsNullOrEmpty(configuration.Target.Type))
			{
				var targetType = configuration.Target.Type.ToLowerInvariant();
				var implementations = types.Where(x => x.IsClass &&
													   !x.IsAbstract &&
													   x.IsSubclassOf(typeof(TargetBase))).ToList();

				if (!implementations.Any())
				{
					"No integrations found.".Print();
					noConfigMessage.Print();
				}

				var implementation = implementations.FirstOrDefault(x => x.Name.ToLowerInvariant() == targetType);

				if (implementation != null)
				{
					_target = (TargetBase) Activator.CreateInstance(implementation, _subscriptions);
					_target.RunOnce = _runOnce;
					_target.StopIntegration += TargetOnStopIntegration;
					new Thread(_target.Process).Start();
				}
				else
				{
					string.Format("No integration found matching [{0}]. Valid integrations are : {1}", targetType,
					              string.Join(", ", implementations.Select(x => x.Name).ToList())).Print();
					noConfigMessage.Print();
				}
			}
			else
			{
				if (!_runOnce) noConfigMessage.Print();
				else
				{
					"You must configure this service before running an integration.".Print();
				}
			}
		}

		private void TargetOnStopIntegration(object sender, EventArgs eventArgs)
		{
			if (_hostControl != null) _hostControl.Stop();
		}

		private Configuration LoadConfiguration()
		{
			try
			{
				var configurationProvider = new ConfigurationProvider();
				return configurationProvider.GetConfiguration();
			}
			catch (ConfigurationErrorsException)
			{
				return null;
			}
		}

	}
}