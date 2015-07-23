//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using RestSharp;

namespace IntegrationService.Util
{
	public static class Extensions
	{
		public const int MaxCardDescriptionSize = 20000;
		private static readonly Logger Log = Logger.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public static string LeanKitHtmlToJiraPlainText(this string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return string.Empty;
			if (text.StartsWith("<p>")) text = text.Substring(3);
			if (text.EndsWith("</p>")) text = text.Remove(text.Length - 4);
			return text
				.Replace("\\n", "")
				.Replace("\r", "")
				.Replace("\n", "")
				.Replace("<p>", "")
				.Replace("</p>", "\\r\\n\\r\\n")
				.Replace("<br />", "\\r\\n")
				.Replace("\"", "\\\"");
		}

		public static string JiraPlainTextToLeanKitHtml(this string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return string.Empty;
			var tmp = "<p>" + text + "</p>";
			tmp = tmp.Replace("\r\n\r\n", "</p>\n<p>")
				.Replace("\r\n", "<br />");
			return tmp;
		}

		public static string SanitizeCardDescription(this string description)
		{
			if (string.IsNullOrEmpty(description)) return description;
			return (description.Length < MaxCardDescriptionSize) ? description : description.Substring(0, MaxCardDescriptionSize);
		}

		public static string FormatSafely(this string input, params object[] args)
		{
			try
			{
				return string.Format(CultureInfo.CurrentCulture, input, args);
			}
			catch (Exception ex)
			{
				var argsCount = (null == args) ? 0 : args.Length;
				Log.Info(ex, "An exception occurred formatting the string [{0}] with [{1}] arguments", input, argsCount);
				return input;
			}
		}

		public static void Info(this string message)
		{
			if (message != null) Log.Info(message);
		}

		public static void Debug(this string message)
		{
			if (message != null) Log.Debug(message);
		}

		public static void Debug(this RestRequest request, IRestClient client)
		{
			var requestParams = new StringBuilder();

			if (request.Parameters.Count > 0)
			{
				requestParams.Append("?");
				for (var i = 0; i < request.Parameters.Count; i++)
				{
					var p = request.Parameters[i];
					if (i > 0) requestParams.Append("&");
					requestParams.AppendFormat("{0}={1}", p.Name, p.Value);
				}
			}
			Log.Debug("Attempting API: {0} {1}{2}", request.Method, client.BaseUrl + request.Resource, requestParams);
		}

		public static void Warn(this string message)
		{
			if (message != null) Log.Warn(message);
		}

		public static void Error(this string message)
		{
			if (message != null) Log.Error(message);
		}

		public static void Error(this string message, Exception ex)
		{
			if (message != null) Log.Error(ex, message);
		}
	}
}
