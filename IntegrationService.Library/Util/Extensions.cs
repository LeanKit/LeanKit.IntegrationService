//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Reflection;
using RestSharp;

namespace IntegrationService.Util
{
	public static class Extensions
	{
		private static readonly Logger Log = Logger.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public const int MaxCardDescriptionSize = 20000;

		public static string SanitizeCardDescription(this string description)
		{
			if (string.IsNullOrEmpty(description)) return description;
			return (description.Length < MaxCardDescriptionSize) ? description : description.Substring(0, MaxCardDescriptionSize);
		}

		public static string FormatSafely(this string input, params object[] args)
		{
			try
			{
				return String.Format(CultureInfo.CurrentCulture, input, args);
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
			Log.Debug("Attempting API: {0} {1}", request.Method, client.BaseUrl + request.Resource);
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
