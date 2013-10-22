//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace IntegrationService
{
    public class AppSettings
    {
        public DateTime RecentQueryDate { get; set; }
        public Dictionary<long, long> BoardVersions { get; set; }
    }
}