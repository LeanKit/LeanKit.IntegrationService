//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoMapper;
using IntegrationService.API.Models;
using IntegrationService.Util;
using ServiceStack.ServiceHost;

namespace IntegrationService.API
{

	[Route("/configuration")]
	public class ConfigurationRequest : Request { }

	[Route("/leankit-login")]
	public class LeankitLoginRequest : Request { }

    [Route("/target-login")]
	public class TargetLoginRequest : Request { }

    [Route("/mapping/{BoardId}")]
	public class MappingRequest : BoardMappingModel { }

    [Route("/settings")]
	public class SettingsRequest : ConfigurationSettingsModel { }

    [Route("/configuration/text")]
	public class ConfigurationTextRequest { }

    [Route("/activate")]
	public class ActivationRequest { }

    public class ConfigService:ServiceBase
    {
        public object Get(ConfigurationRequest request)
        {
            ConfigurationModel configModel;
            try
            {
                var store = GetConfigurationStorage();
                var config = store.Load() ?? new Configuration();

                // map to ConfigurationModel
                configModel = Mapper.Map<ConfigurationModel>(config);

                // strip 'https://' from target host name
                if (configModel.Target != null
                    && !string.IsNullOrEmpty(configModel.Target.Host)
                    && configModel.Target.Host.StartsWith("http"))
                {
	                var uri = new Uri(configModel.Target.Host);
	                configModel.Target.Host = uri.Host + uri.PathAndQuery;
                }

            }
            catch (Exception ex)
            {
				ex.Message.Error();
                return ServerError(ex.Message);
            }
         
            return OK(configModel);
        }



        public object Put(MappingRequest request)
        {
            var model = (BoardMappingModel) request;

            var mapping = Mapper.Map<BoardMapping>(model);

            var localStorage = GetConfigurationStorage();
            var config = localStorage.Load();

            // ensure the mapping collection exists
            if(config.Mappings==null)
                config.Mappings= new List<BoardMapping>();

            // if this mapping is already in the mapping collection, delete it
            var existingMapping = config.Mappings.FirstOrDefault(x => x.Identity.LeanKit == mapping.Identity.LeanKit);
            if (existingMapping != null)
                config.Mappings.Remove(existingMapping);

            // add the revised mapping and save
            config.Mappings.Add(mapping);


            
            localStorage.Save(config);

            return OK();
        }

        public object Delete(MappingRequest request)
        {
            var configStore = GetConfigurationStorage();
            if (configStore == null) return ServerError("No Configuration is available");

            var config = configStore.Load();
            if (config == null) return ServerError("Error attempting to load configuration.");

            var mapping = config.Mappings.FirstOrDefault(x => x.Identity.LeanKit == request.BoardId);
            if (mapping == null)
                return ServerError(string.Format("There is no mapping for board [{0}]", request.BoardId));

            try
            {
                config.Mappings.Remove(mapping);
                configStore.Save(config);
            }
            catch (Exception ex)
            {
                return ServerError("An unexpected error occurred while attempting to remove the requested mapping: " + ex.Message);
            }

            return OK();
        }

        public object Post(SettingsRequest request)
        {
            var configStore = GetConfigurationStorage();
            if (configStore == null) return ServerError("No Configuration is available");

            var config = configStore.Load();
            if (config == null) return ServerError("Error attempting to load configuration.");

            try
            {
                config.PollingFrequency = request.PollingFrequency;
                config.EarliestSyncDate = request.EarliestSyncDate;

                configStore.Save(config);
            }
            catch(Exception ex)
            {
                return ServerError("An unexpected error occurred while attempting to save changes: " + ex.Message);
            }

            return OK();
        }

        public object Get(ConfigurationTextRequest request)
        {
            var config = GetConfiguration();
            var lines = config.ToString().Split('\n');
            var output = lines.Aggregate("<ol>", (current, line) => current + ("<li>" + line + "</li>"));
            output += "</ol>";
            return OK(output);
        }


        public object Put(ActivationRequest request)
        {
            try
            {
                IntegrationService.Reset();
            }
            catch (Exception ex)
            {
                return ServerError(ex.Message);
            }
            return OK();
        }

        private LocalStorage<Configuration> GetConfigurationStorage()
        {
	        var dir = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory;
			
			if (dir == null) throw new Exception("Could not access current installation directory.");

			var curFolder = dir.FullName;
            var storagefile = Path.Combine(curFolder, "config-edit.json");
            return new LocalStorage<Configuration>(storagefile);
        }

        private Configuration GetConfiguration()
        {
            return GetConfigurationStorage().Load();
        }
    }
}


