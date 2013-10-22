//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System.Linq;
using Kanban.API.Client.Library;
using Kanban.API.Client.Library.TransferObjects;

namespace IntegrationService.Targets.JIRA
{
    public static class JiraConversionExtensions
    {

        public static int LeanKitPriority(this Jira.Issue issue)
        {
           return CalculateLeanKitPriority(issue);
        }

		public static int CalculateLeanKitPriority(Jira.Issue issue) 
		{
			//LK Priority: 0 = Low, 1 = Normal, 2 = High, 3 = Critical
			//Jira Priority: Blocker/Critical, Major, Minor, Trivial
			int lkPriority = 1; // default to 1 - Normal
			if (issue == null ||
				issue.Fields == null ||
				issue.Fields.Priority == null ||
				string.IsNullOrEmpty(issue.Fields.Priority.Name))
				return lkPriority;

			switch (issue.Fields.Priority.Name) {
				case "Blocker":
				case "Critical":
					return 3;
					break;
				case "Major":
					return 2;
					break;
				case "Trivial":
					return 0;
				case "Minor":
				default:
					return 1;
					break;
			}
		}

        public static CardType LeanKitCardType(this Jira.Issue issue, BoardMapping project)
        {
            return CalculateLeanKitCardType(project, issue.Fields.IssueType.Name);
        }

		public static CardType CalculateLeanKitCardType(BoardMapping project, string issueTypeName) 
		{
			var boardId = project.Identity.LeanKit;

			if (!string.IsNullOrEmpty(issueTypeName)) {
				var mappedWorkType = project.Types.FirstOrDefault(x => x.Target.ToLowerInvariant() == issueTypeName.ToLowerInvariant());
				if (mappedWorkType != null) {
					var definedVal =
						project.ValidCardTypes.FirstOrDefault(x => x.Name.ToLowerInvariant() == mappedWorkType.LeanKit.ToLowerInvariant());
					if (definedVal != null) {
						return definedVal;
					}
				}
				var implicitVal =
					project.ValidCardTypes.FirstOrDefault(x => x.Name.ToLowerInvariant() == issueTypeName.ToLowerInvariant());
				if (implicitVal != null) {
					return implicitVal;
				}
			}

			return project.ValidCardTypes.FirstOrDefault(x => x.IsDefault);
		}

		public static long? LeanKitAssignedUserId(this Jira.Issue issue, long boardId, ILeanKitApi leanKit)
		{
			return CalculateLeanKitAssignedUserId(boardId, issue, leanKit);
		}

		public static long? CalculateLeanKitAssignedUserId(long boardId, Jira.Issue issue, ILeanKitApi leanKit) 
		{
			if (issue == null)
				return null;

			if (issue.Fields != null &&
				issue.Fields.Assignee != null &&
				(!string.IsNullOrEmpty(issue.Fields.Assignee.Name) ||
				!string.IsNullOrEmpty(issue.Fields.Assignee.DisplayName) ||
				!string.IsNullOrEmpty(issue.Fields.Assignee.EmailAddress))) {
				//user.MailAddress
				var lkUser = leanKit.GetBoard(boardId).BoardUsers.FirstOrDefault(x => x != null &&
					(((!string.IsNullOrEmpty(x.EmailAddress)) && (!string.IsNullOrEmpty(issue.Fields.Assignee.EmailAddress)) && x.EmailAddress.ToLowerInvariant() == issue.Fields.Assignee.EmailAddress.ToLowerInvariant()) ||
					((!string.IsNullOrEmpty(x.FullName)) && (!string.IsNullOrEmpty(issue.Fields.Assignee.Name)) && x.FullName.ToLowerInvariant() == issue.Fields.Assignee.Name.ToLowerInvariant()) ||
					((!string.IsNullOrEmpty(x.UserName)) && (!string.IsNullOrEmpty(issue.Fields.Assignee.Name)) && x.UserName.ToLowerInvariant() == issue.Fields.Assignee.Name.ToLowerInvariant()) ||
					((!string.IsNullOrEmpty(x.FullName)) && (!string.IsNullOrEmpty(issue.Fields.Assignee.DisplayName)) && x.FullName.ToLowerInvariant() == issue.Fields.Assignee.DisplayName.ToLowerInvariant())));
				if (lkUser != null)
					return lkUser.Id;
			}

			return null;
		}
    }
}