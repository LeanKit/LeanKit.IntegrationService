//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Text;

namespace IntegrationService
{
	public class ServerConfiguration
	{
		public string Url
		{
			get { return Protocol + Host; }
		}

		public string Protocol { get; set; }
		public string Host { get; set; }
		public string User { get; set; }
		public string Password { get; set; }
		public string Type { get; set; }

		public override string ToString() 
		{
			var sb = new StringBuilder();
			sb.Append("     Type :     " + Type + Environment.NewLine);
			sb.Append("     Url  :     " + Url + Environment.NewLine);
			sb.Append("     Protocol : " + Protocol + Environment.NewLine);
			sb.Append("     Host :     " + Host + Environment.NewLine);
			sb.Append("     User :     " + User + Environment.NewLine);
			sb.Append("     Password : " + (!string.IsNullOrEmpty(Password) ? "******" : "Undefined"));
			return sb.ToString();
		}
	}
}