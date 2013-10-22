//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Reflection;
using AutoMapper;
using IntegrationService.Util;
using NUnit.Framework;

namespace IntegrationService.Tests.ConfigurationTests
{
    public class MappingConfiguration
    {
        public static void Init()
        {
			var libraryMappings = Assembly.GetAssembly(typeof (BoardMapping)).GetTypes().Where(x => x.IsClass && typeof (IModelMapping).IsAssignableFrom(x));

			foreach (var inst in libraryMappings.Select(mapping => (IModelMapping) Activator.CreateInstance(mapping)))
			{
				inst.Init();
			}

			var mappings = Assembly.GetAssembly(typeof (IntegrationService)).GetTypes().Where(x => x.IsClass && typeof (IModelMapping).IsAssignableFrom(x));

			foreach (var inst in mappings.Select(mapping => (IModelMapping) Activator.CreateInstance(mapping)))
            {
                inst.Init();
            }

        }
    }

    [TestFixture]
    public class MappingTests
    {
        [Test]
        public void AutoMapperTests()
        {
            MappingConfiguration.Init();
            Mapper.AssertConfigurationIsValid();
        }
    }
}
