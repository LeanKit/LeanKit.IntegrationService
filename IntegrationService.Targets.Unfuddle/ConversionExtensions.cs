//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System.Linq;
using Kanban.API.Client.Library.TransferObjects;

namespace IntegrationService.Targets.Unfuddle
{
    public static class UnfuddleConversionExtensions
    {

        public static int LeanKitPriority(this Unfuddle.Ticket ticket)
        {
           return CalculateLeanKitPriority(ticket);
        }

		public static int CalculateLeanKitPriority(Unfuddle.Ticket ticket) 
		{
			//LK Priority: 0 = Low, 1 = Normal, 2 = High, 3 = Critical
			//Unfuddle Priority: 5 = Highest, 4 = High, 3 = Normal, 2 = Low, 1 = Lowest
			int lkPriority = 1; // default to 1 - Normal
			if (ticket == null)
				return lkPriority;

			switch (ticket.Priority) {
				case 5:
					return 3;
					break;
				case 4:
					return 2;
					break;
				case 2:
				case 1:
					return 0;
					break;
				case 3:
				default:
					return 1;
					break;
			}
		}

        public static CardType LeanKitCardType(this Unfuddle.Ticket ticket, BoardMapping project)
        {
            return CalculateLeanKitCardType(project, "");
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
    }
}