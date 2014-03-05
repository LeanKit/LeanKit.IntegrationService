//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;

namespace IntegrationService
{
    public class Configuration
    {
		public Configuration()
		{
			PollingFrequency = 5;
			PollingUnits = "minutes";
			PollingTime = null;
			EarliestSyncDate = new DateTime(2013, 1, 1);
			Mappings = new List<BoardMapping>();
			Target = new ServerConfiguration();
			LeanKit = new ServerConfiguration();
		}

        public int PollingFrequency { get; set; }
		public string PollingUnits { get; set; }
		public TimeSpan? PollingTime { get; set; }
		public bool PollingRunOnce { get; set; }
        public ServerConfiguration Target { get; set; }
        public ServerConfiguration LeanKit { get; set; }
        public List<BoardMapping> Mappings { get; set; }
        public DateTime EarliestSyncDate { get; set; }
        public string LocalStoragePath { get; set; }
		public bool CreateTargetItems { get; set; }

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.Append(Environment.NewLine);
			sb.AppendLine("PollingFrequency :        " + PollingFrequency.ToString());
			sb.AppendLine("PollingUnits:             " + PollingUnits);
			sb.AppendLine("PollingTime:              " + PollingTime.ToString());
			sb.AppendLine("PollingRunOnce:           " + PollingRunOnce.ToString());
			sb.AppendLine("LocalStoragePath :        " + LocalStoragePath);
			sb.AppendLine("EarliestSyncDate :        " + EarliestSyncDate.ToUniversalTime().ToString("o"));
            sb.AppendLine("LeanKit :                 " + Environment.NewLine + LeanKit);
			sb.AppendLine("Target :                  " + Environment.NewLine + Target);
			sb.AppendLine("Mappings :                ");
			foreach (var boardMapping in Mappings)
			{
				sb.Append(boardMapping + Environment.NewLine);
			}
			return sb.ToString();
		}

		public int GetEffectivePollingFrequency()
		{
			if (string.IsNullOrEmpty(PollingUnits))
				return PollingFrequency;

			switch (PollingUnits.ToLowerInvariant()) 
			{
				case "seconds":
					return PollingFrequency * 1000;
				case "minutes":
					return PollingFrequency * 1000 * 60;					
				case "hours":
					return PollingFrequency * 1000 * 60 * 60;				
				case "milliseconds":
				default:
					return PollingFrequency;
			}						
		}
    }


}
