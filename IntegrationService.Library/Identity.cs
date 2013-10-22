//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System.Text;

namespace IntegrationService
{
	public class Identity
	{
		public long LeanKit { get; set; }
		public string LeanKitTitle { get; set; }
		public string Target { get; set; }
		public string TargetName { get; set; }

		public override string ToString() 
		{
			var sb = new StringBuilder();
			sb.AppendLine("          LeanKit : " + LeanKit.ToString());
			sb.AppendLine("          Target  : " + Target);
			return sb.ToString();
		}
	}
}