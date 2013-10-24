//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Linq;
using Kanban.API.Client.Library.TransferObjects;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using RestSharp.Contrib;

namespace IntegrationService.Targets.TFS
{
    public static class ConversionExtensions
    {

        public static int LeanKitPriority(this WorkItem workItem)
        {
			const int lkPriority = 1; // default to 1 - Normal

            if (workItem == null) return lkPriority;

			var tfsPriority = "";
            if (workItem.Fields != null)
            {
                if (workItem.Fields.Contains("Priority") && workItem.Fields["Priority"].Value != null)
                    tfsPriority = workItem.Fields["Priority"].Value.ToString();
            }

            return CalculateLeanKitPriority(tfsPriority);

        }

        public static string LeanKitDescription(this WorkItem workItem, int tfsVersion)
        {
            if (workItem.Fields == null) return "";
			return workItem.Fields.Contains("Repro Steps") 
				? workItem.Fields["Repro Steps"].Value.ToString() 
				: EnsureHtmlEncode(workItem.Fields["Description"].Value.ToString(), tfsVersion);
        }

		private static string EnsureHtmlEncode(string text, int tfsVersion)
		{
			if (string.IsNullOrEmpty(text.Trim()))
				return text;

			if (tfsVersion > 2010)
				return text;

			if (IsHtmlEncoded(text))
				return text;
			
			return HttpUtility.HtmlEncode(text);
		}

		private static bool IsHtmlEncoded(string text)
		{
			return (HttpUtility.HtmlDecode(text) != text);
		}

        public static int CalculateLeanKitPriority(string tfsPriority)
        {

			var lkPriority = 1; // default to 1 - Normal

            if (string.IsNullOrEmpty(tfsPriority))
                return lkPriority;

			int tfsPriorityInt;
			var gotInt = int.TryParse(tfsPriority, out tfsPriorityInt);
            if (gotInt)
            {
                if (tfsPriorityInt > 0 && tfsPriorityInt < 5)
                    lkPriority = tfsPriorityInt - 1;
            }
            //LK Priority: 0 = Low, 1 = Normal, 2 = High, 3 = Critical
            //TFS Priority: 1-4

            return lkPriority;
        }

        public static bool UseReproSteps(this WorkItem workItem)
        {
			return (workItem.Fields != null && workItem.Fields.Contains("Repro Steps"));
        }

        public static CardType LeanKitCardType(this WorkItem workItem, BoardMapping project)
        {
            return CalculateLeanKitCardType(project, workItem.Type.Name);
        }

        public static CardType CalculateLeanKitCardType(BoardMapping project, string tfsWorkItemTypeName)
        {
            if (!String.IsNullOrEmpty(tfsWorkItemTypeName))
            {
                var mappedWorkType = project.Types.FirstOrDefault(x => x.Target.ToLowerInvariant() == tfsWorkItemTypeName.ToLowerInvariant());
                if (mappedWorkType != null)
                {
                    var definedVal = project.ValidCardTypes.FirstOrDefault(x => x.Name.ToLowerInvariant() == mappedWorkType.LeanKit.ToLowerInvariant());
                    if (definedVal != null)
                    {
                        return definedVal;
                    }
                }
                var implicitVal = project.ValidCardTypes.FirstOrDefault(x => x.Name.ToLowerInvariant() == tfsWorkItemTypeName.ToLowerInvariant());
                if (implicitVal != null)
                {
                    return implicitVal;
                }
            }
            return project.ValidCardTypes.FirstOrDefault(x => x.IsDefault);

        }
    }

}