//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntegrationService
{
	public class Lane
	{
		public long Id { get; set; }
		public string Name { get; set; }
		public bool IsFirst { get; set; }
		public bool IsLast { get; set; }
		public List<long> ChildLaneIds { get; set; }

		public bool HasChildLanes
		{
			get { return (ChildLaneIds != null && ChildLaneIds.Any()); }
		}

		public override string ToString() 
		{
			var sb = new StringBuilder();
			sb.AppendLine("          Id :                " + Id.ToString());
			sb.AppendLine("          Name :              " + Name);
			sb.AppendLine("          IsFirst :           " + IsFirst.ToString());
			sb.AppendLine("          IsLast :            " + IsLast.ToString());
			if (HasChildLanes)
			{
				sb.AppendLine("          ChildLaneIds :  " + string.Join(",", ChildLaneIds));
			}
			return sb.ToString();
		}
	}
}