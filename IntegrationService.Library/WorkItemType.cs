//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System.Text;

namespace IntegrationService
{
	public class WorkItemType
	{
		public string LeanKit { get; set; }
		public string Target { get; set; }

		public override string ToString() 
		{
			var sb = new StringBuilder();
			sb.Append("Target." + Target + " == LeanKit." + LeanKit);
			return sb.ToString();
		}
	}
}