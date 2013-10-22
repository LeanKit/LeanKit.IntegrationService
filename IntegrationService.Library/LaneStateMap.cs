//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Text;

namespace IntegrationService
{
	public class LaneStateMap
	{
		public long Lane { get; set; }
		public string LaneName { get; set; }
		public string State { get; set; }
		public List<string> States { get; set; }

		public override string ToString() 
		{
			var sb = new StringBuilder();
			sb.AppendLine("          Lane : " + Lane.ToString());
			sb.AppendLine("          LaneName : " + LaneName);
			sb.AppendLine("          State : " + State);
			return sb.ToString();
		}
	}
}