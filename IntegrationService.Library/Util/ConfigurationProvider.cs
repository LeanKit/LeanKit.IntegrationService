//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Configuration;
using System.IO;
using System.Reflection;
using ServiceStack.Text;

namespace IntegrationService.Util
{
    public class ConfigurationProvider:IConfigurationProvider<Configuration>
    {
	    public Configuration GetConfiguration()
	    {
		    var dir = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory;
		    if (dir == null) throw new Exception("Could not access application directory.");
		    var curFolder = dir.FullName;
		    var configJsonFile = Path.Combine(curFolder, "config-live.json");
		    Configuration config;
		    if (!File.Exists(configJsonFile))
		    {
			    throw new ConfigurationErrorsException(
				    "Missing config-live.json file. You must have at least one of the config files.");
		    }
		    try
		    {
			    using (var stream = new StreamReader(configJsonFile))
			    {
				    config = JsonSerializer.DeserializeFromReader<Configuration>(stream);
			    }
		    }
		    catch (Exception ex)
		    {
			    throw new ConfigurationErrorsException(ex.Message);
		    }

		    if (config != null)
		    {
			    config.LocalStoragePath = Path.Combine(curFolder, "localstore.json");
		    }

		    ValidateConfiguration(config);

		    return config;
	    }

	    private void ValidateConfiguration(Configuration config)
        {
            if (config == null) throw new ConfigurationErrorsException("Configuration is invalid.");
            if (config.LeanKit == null) throw new ConfigurationErrorsException("Configuration is missing LeanKit config section.");
            if (config.LeanKit.Url == null) throw new ConfigurationErrorsException("Configuration is missing LeanKit Host definition.");
            if (config.LeanKit.User == null) throw new ConfigurationErrorsException("Configuration is missing LeanKit User definition.");
            if (config.LeanKit.Password == null) throw new ConfigurationErrorsException("Configuration is missing LeanKit Password definition.");
            if (config.Target == null) throw new ConfigurationErrorsException("Configuration is missing Target section.");
            if (config.Target.Url == null) throw new ConfigurationErrorsException("Configuration is missing Target Host definition.");
            if (config.Target.User == null)
            {
	            if (!config.Target.Protocol.ToLowerInvariant().StartsWith("file"))
	            {
		            throw new ConfigurationErrorsException("Configuration is missing Target User definition.");
	            }
            }
            if (config.Target.Password == null)
            {
	            if (!config.Target.Protocol.ToLowerInvariant().StartsWith("file"))
	            {
		            throw new ConfigurationErrorsException("Configuration is missing Target Password definition.");
	            }
            }
            if (config.Mappings == null) throw new ConfigurationErrorsException("Configuration is missing Mappings section.");
            foreach (var mapping in config.Mappings)
            {
                if (mapping.Identity == null) throw new ConfigurationErrorsException("Configuration Mapping is missing Identity section.");
                if (mapping.Identity.LeanKit == 0) throw new ConfigurationErrorsException("Mapping Identity is missing LeanKit Board Id");
                if (mapping.Identity.Target == null) throw new ConfigurationErrorsException("Mapping Identity is missing Target project Id");
            }
        }

    }
}
