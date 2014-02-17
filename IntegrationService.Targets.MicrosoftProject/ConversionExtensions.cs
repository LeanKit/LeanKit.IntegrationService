//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using LeanKit.API.Client.Library.TransferObjects;
using net.sf.mpxj;
using net.sf.mpxj.ExtensionMethods;

namespace IntegrationService.Targets.MicrosoftProject
{
    public static class MicrosoftProjectConversionExtensions
    {

        public static int LeanKitPriority(this Task task)
        {
           return CalculateLeanKitPriority(task);
        }

		public static int CalculateLeanKitPriority(Task task) 
		{
			//LK Priority: 0 = Low, 1 = Normal, 2 = High, 3 = Critical
			//Unfuddle Priority: 100 = LOWEST, 200 = VERY_LOW, 300 = LOWER, 400 = LOW, 
			//					 500 = MEDIUM, 600 = HIGH, 700 = HIGHER, 800 = VERY_HIGH,
			//					 900 = HIGHEST, 1000 = DO_NOT_LEVEL

			int lkPriority = 1; // default to 1 - Normal
			if (task == null)
				return lkPriority;

			switch (task.Priority.Value) 
			{
				case 900:
				case 800:
					return 3;
					break;
				case 700:
				case 600:
					return 2;
					break;
				case 400:
				case 300:
				case 200:
				case 100:
					return 0;
					break;
				case 500:
				default:
					return 1;
					break;
			}
		}

		public static CardType LeanKitCardType(this Task task, BoardMapping project, Dictionary<LeanKitField, List<string>> importFields)
		{
			var cardTypeFields = importFields.GetTargetFieldsFor(LeanKitField.CardType);
	        foreach (var cardTypeField in cardTypeFields)
	        {
		        var res = task.GetText(cardTypeField);
		        if (!string.IsNullOrEmpty(res))
					return CalculateLeanKitCardType(project, res);
	        }
	        return CalculateLeanKitCardType(project, "");
        }

		public static CardType CalculateLeanKitCardType(BoardMapping project, string issueTypeName) 
		{
			var boardId = project.Identity.LeanKit;

			if (!string.IsNullOrEmpty(issueTypeName)) 
			{
				var mappedWorkType = project.Types.FirstOrDefault(x => x.Target.ToLowerInvariant() == issueTypeName.ToLowerInvariant());
				if (mappedWorkType != null) {
					var definedVal = project.ValidCardTypes.FirstOrDefault(x => x.Name.ToLowerInvariant() == mappedWorkType.LeanKit.ToLowerInvariant());
					if (definedVal != null) {
						return definedVal;
					}
				}
				var implicitVal = project.ValidCardTypes.FirstOrDefault(x => x.Name.ToLowerInvariant() == issueTypeName.ToLowerInvariant());
				if (implicitVal != null) {
					return implicitVal;
				}
			}

			return project.ValidCardTypes.FirstOrDefault(x => x.IsDefault);
		}

		public static string GetClassOfService(this Task task, Dictionary<LeanKitField, List<string>> importFields)
		{
			var cosFields = importFields.GetTargetFieldsFor(LeanKitField.ClassOfService);
			foreach (var cosField in cosFields)
			{
				var res = task.GetText(cosField);
				if (!string.IsNullOrEmpty(res))
					return res;
			}
			return "";
		}

		public static bool GetIsBlocked(this Task task, Dictionary<LeanKitField, List<string>> importFields)
		{
			var isBlockedFields = importFields.GetTargetFieldsFor(LeanKitField.IsBlocked);
			foreach (var blockedField in isBlockedFields)
			{
				var res = task.GetText(blockedField);
				if (!string.IsNullOrEmpty(res))
					if (res.ToLowerInvariant() == "yes")
						return true;
			}
			return false;					
		}

		public static string GetBlockedReason(this Task task, Dictionary<LeanKitField, List<string>> importFields)
		{
			var reasonFields = importFields.GetTargetFieldsFor(LeanKitField.BlockedReason);
			foreach (var reasonField in reasonFields)
			{
				var res = task.GetText(reasonField);
				if (!string.IsNullOrEmpty(res))
					return res;
			}
			return "";
		}

		public static string GetTags(this Task task, Dictionary<LeanKitField, List<string>> importFields)
		{
			var tagsFields = importFields.GetTargetFieldsFor(LeanKitField.Tags);
			List<string> tags = new List<string>();
			foreach (var tagsField in tagsFields)
			{
				var tag = task.GetText(tagsField);
				if (!string.IsNullOrEmpty(tag))
					tags.Add(tag);
			}
			return string.Join(",", tags);
		}

		/// <summary></summary>
		/// <param name="task"></param>
		/// <param name="textFieldName">Example: Text2</param>
		/// <returns></returns>
		public static string GetText(this Task task, string textFieldName)
		{
			if (textFieldName != "None")
			{
				var idx = GetTargetTextFieldNumber(textFieldName);

				if (idx > 0 && idx <= 20)
					return task.GetText(idx);
			}
			return string.Empty;
		}

		public static DateTime? GetDueDate(this Task task, Dictionary<LeanKitField, List<string>> importFields)
		{
			var dateFields = importFields.GetTargetFieldsFor(LeanKitField.DueDate);
			foreach (var dateField in dateFields)
			{
				switch (dateField)
				{
					case "BaselineFinish":
						if (task.BaselineFinish != null)
							return task.BaselineFinish.ToDateTime();
						break;
					case "Finish":
						if (task.Finish != null)
							return task.Finish.ToDateTime();
						break;
					case "EarlyFinish":
						if (task.EarlyFinish != null)
							return task.EarlyFinish.ToDateTime();
						break;
					default:
						//do nothing
						break;
				}
			}
			return null;
		}

		public static DateTime? GetStartDate(this Task task, Dictionary<LeanKitField, List<string>> importFields)
		{
			var dateFields = importFields.GetTargetFieldsFor(LeanKitField.StartDate);
			foreach (var dateField in dateFields)
			{
				switch (dateField) 
				{
					case "BaselineStart":
						if (task.BaselineStart != null)
							return task.BaselineStart.ToDateTime();
						break;
					case "Start":
						if (task.Start != null)
							return task.Start.ToDateTime();
						break;
					case "EarlyStart":
						if (task.EarlyStart != null)
							return task.EarlyStart.ToDateTime();
						break;
					default:
						break;
				}			
			}
			return null;
		}

		public static int GetSize(this Task task, Dictionary<LeanKitField, List<string>> importFields)
		{
			var sizeFields = importFields.GetTargetFieldsFor(LeanKitField.Size);
			foreach (var sizeField in sizeFields)
			{
				switch (sizeField)
				{
					case "BaselineWork":
						if (task.BaselineWork.Duration >= 1)
							return (int) task.BaselineWork.Duration;
						break;
					case "Work":
						if (task.Work.Duration >= 1)
							return (int) task.Work.Duration;
						break;
					case "BaselineCost":
						if (task.BaselineCost.intValue() >= 1)
							return task.BaselineCost.intValue();
						break;
					case "Cost":
						if (task.Cost.intValue() >= 1)
							return task.Cost.intValue();
						break; 

				}
			}
			return 0;
		}

		public static List<string> GetTargetFieldsFor(this Dictionary<LeanKitField, List<String>> importFields, LeanKitField leanKitField) 
		{
			var values = new List<string>();
			if (importFields.ContainsKey(leanKitField)) 
			{
				if (importFields[leanKitField].Any()) 
				{
					if (!importFields[leanKitField].Contains("None"))
						return importFields[leanKitField];
				}
			}
			return values;
		}

		private static int GetTargetTextFieldNumber(this String textField)
		{
			if (string.IsNullOrEmpty(textField))
				return 0;

			var targetInt = 0;
			int.TryParse(textField.Replace("Text", ""), out targetInt);
			return targetInt;
		}
    }
}