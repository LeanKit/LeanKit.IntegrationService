//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Reflection;

namespace IntegrationService.Util
{
    public static class Extensions
    {
        private static readonly Logger Log = Logger.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static string FormatSafely(this string input, params object[] args)
        {
            try
            {
                return String.Format(CultureInfo.CurrentCulture, input, args);
            }
            catch (Exception ex)
            {
                var argsCount = (null == args) ? 0 : args.Length;
                Log.Info(ex, "An exception occurred formatting the string [{0}] with [{1}] arguments",
                         input, argsCount);
                return input;
            }
        }

		public static void Info(this string message)
		{
			Log.Info(message);
		}

		public static void Debug(this string message)
		{
			Log.Debug(message);
		}
	
		public static void Warn(this string message)
		{
			Log.Warn(message);
		}

		public static void Error(this string message)
		{
			Log.Error(message);
		}

    }
}
