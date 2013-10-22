//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System.Linq;
using Kanban.API.Client.Library;
using Kanban.API.Client.Library.TransferObjects;

namespace IntegrationService.Targets.GitHub
{
    public static class GithubConversionExtensions
    {

        public static int LeanKitPriority(this GitHubIssues.Issue issue)
        {
           return CalculateLeanKitPriority(issue);
        }

		public static int CalculateLeanKitPriority(GitHubIssues.Issue issue) 
		{
			// NOTE: GitHub does not use priorities for issues. The only thing we could do is use labels.
			// However the default labels of bug, duplicate, enhancement, invalid, question and wont fix 
			// do not map well to priorities. We can look for custom labels but that would be up to the 
			// GitHub admin to create the custom labels
			// If you would like to use different priorities/labels than the OOTB LeanKit priorities 
			// then you will have to alter this method

			const int lkPriority = 1; // default to 1 - Normal

			// LK Priority: 0 = Low, 1 = Normal, 2 = High, 3 = Critical
			if (issue != null && issue.Labels != null && issue.Labels.Any()) {
				foreach (var label in issue.Labels) {
					switch (label.Name.ToLowerInvariant()) {
						case "critical":
							return 3;
						case "high":
							return 2;
						case "normal":
							return 1;
						case "low":
							return 0;
					}
				}
			}

			// else just set it to default of Normal
			return lkPriority;
		}


        public static CardType LeanKitCardType(this GitHubIssues.Issue issue, BoardMapping project)
        {
            return CalculateLeanKitCardType(project, issue);
        }

		public static CardType CalculateLeanKitCardType(BoardMapping project, GitHubIssues.Issue issue) 
		{
			// NOTE: GitHub does not use types for issues. It uses labels. 
			// Default labels are: bug, duplicate, enhancement, invalid, question, wont fix
			// of those bug and enhancement are the ones that fit a type the best. 
			// bug could be mapped to bug/issue in LeanKit and enhancement mapped to improvement/feature in LeanKit

			var defaultCardType = project.ValidCardTypes.FirstOrDefault(x => x.IsDefault);

			if (issue != null && issue.Labels != null && issue.Labels.Any()) {
				foreach (var label in issue.Labels) {
					var mappedWorkType = project.Types.FirstOrDefault(x => x.Target.ToLowerInvariant() == label.Name.ToLowerInvariant());
					if (mappedWorkType != null) {
						var definedVal = project.ValidCardTypes.FirstOrDefault(x => x.Name.ToLowerInvariant() == mappedWorkType.LeanKit.ToLowerInvariant());
						if (definedVal != null) {
							return definedVal;
						}
					}
					var implicitVal = project.ValidCardTypes.FirstOrDefault(x => x.Name.ToLowerInvariant() == label.Name.ToLowerInvariant());
					if (implicitVal != null) {
						return implicitVal;
					}
				}
			}
			return defaultCardType;
		}

		public static long? LeanKitAssignedUserId(this GitHubIssues.Issue issue, long boardId, ILeanKitApi leanKit)
		{
			return CalculateLeanKitAssignedUserId(boardId, issue, leanKit);
		}

		public static long? CalculateLeanKitAssignedUserId(long boardId, GitHubIssues.Issue issue, ILeanKitApi leanKit) 
		{
			if (issue == null)
				return null;

			if (issue.Assignee != null && !string.IsNullOrEmpty(issue.Assignee.Login)) {
				var lkUser = leanKit.GetBoard(boardId).BoardUsers.FirstOrDefault(x => x != null &&
					(((!string.IsNullOrEmpty(x.EmailAddress)) && x.EmailAddress.ToLowerInvariant() == issue.Assignee.Login.ToLowerInvariant()) ||
					((!string.IsNullOrEmpty(x.FullName)) && x.FullName.ToLowerInvariant() == issue.Assignee.Login.ToLowerInvariant()) ||
					((!string.IsNullOrEmpty(x.UserName)) && x.UserName.ToLowerInvariant() == issue.Assignee.Login.ToLowerInvariant())));
				if (lkUser != null)
					return lkUser.Id;
			}

			return null;
		}

		public static int LeanKitPriority(this GitHubPulls.Pull pull) 
		{
			return CalculateLeanKitPriority(pull);
		}

		public static int CalculateLeanKitPriority(GitHubPulls.Pull issue)
		{
			const int lkPriority = 1; // default to 1 - Normal
			// LK Priority: 0 = Low, 1 = Normal, 2 = High, 3 = Critical
			// else just set it to default of Normal
			return lkPriority;
		}

	    public static CardType LeanKitCardType(this GitHubPulls.Pull pull, BoardMapping project) 
		{
			return CalculateLeanKitCardType(project, pull);
		}

		public static CardType CalculateLeanKitCardType(BoardMapping project, GitHubPulls.Pull pull) 
		{
			var defaultCardType = project.ValidCardTypes.FirstOrDefault(x => x.IsDefault);

			return defaultCardType;
		}

		public static long? LeanKitAssignedUser(this GitHubPulls.Pull pull, long boardId, ILeanKitApi leanKit)
		{
			return CalculateLeanKitAssignedUserId(boardId, pull, leanKit);
		}

		public static long? CalculateLeanKitAssignedUserId(long boardId, GitHubPulls.Pull pull, ILeanKitApi leanKit) 
		{
			if (pull == null)
				return null;

			if (pull.Base != null && pull.Base.User != null && !string.IsNullOrEmpty(pull.Base.User.Login)) {
				var lkUser = leanKit.GetBoard(boardId).BoardUsers.FirstOrDefault(x => x != null &&
					(((!string.IsNullOrEmpty(x.EmailAddress)) && x.EmailAddress.ToLowerInvariant() == pull.Base.User.Login.ToLowerInvariant()) ||
					((!string.IsNullOrEmpty(x.FullName)) && x.FullName.ToLowerInvariant() == pull.Base.User.Login.ToLowerInvariant()) ||
					((!string.IsNullOrEmpty(x.UserName)) && x.UserName.ToLowerInvariant() == pull.Base.User.Login.ToLowerInvariant())));
				if (lkUser != null)
					return lkUser.Id;
			}

			return null;
		}
    }
}