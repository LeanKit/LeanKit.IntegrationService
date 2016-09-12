### June 29, 2016

*   Fixed issue caused by missing Priority field in JIRA issue

### June 23, 2016

*   Creating a LeanKit card in an unmapped lane should not trigger creating a new item in the target system.

### May 25, 2016

*   Synchronization improvements and fixes.

### May 20, 2016

*   Synchronization improvements and fixes.

### March 22, 2016

*   Synchronization improvements and fixes.

### March 9, 2016

*   Fix for synchronization conflicts.
*   Improvements to logging.

### February 1, 2016

*   Fix for issue states or issue types that contain a slash (/) causing problems in the Configuration UI.

### January 28, 2016

*   Additional support for NTLM authentication for TFS.

### January 21, 2016

*   Fixed issue where in some scenarios a board update could trigger updating a target item, even when "Update Target Items" is disabled.

### November 6, 2015

*   Fixed issue with "Update Target Items" setting being ignored when service is restarted, potentially causing the states of target items being updated for cards that were moved while the service was stopped. 

### July 23, 2015

*   Fixed JIRA formatting issues of descriptions synchronized between JIRA and LeanKit.

### June 29, 2015

*   Fixed JIRA issue where reopening an issue did not move LeanKit card back to appropriate lane.

### June 11, 2015

*   Added support for additional JIRA priorities

### May 7, 2015

*   Updated LeanKit polling to reduce risk of race condition

### March 30, 2015

*   JIRA integration now uses cookie authentication, to be more compatible with proxy servers.
*   Now supports alternative LeanKit domains, such as leankit.co. Must supply the full URL of the domain, e.g. https://company.leankit.co

### February 9, 2015

*   Fixed issue affecting JIRA integration running on non-US cultures

### December 8, 2014

*   Fixed issue affecting JIRA and GitHub synchronizing title and descriptions that contain double quotes
*   Updated to the latest LeanKit.API.Client

### September 3, 2014

*   Fixed synchronization of tags with TFS
*   Fixed issue with adding cards to default drop lane from JIRA

### August 6, 2014

*   Official support for Visual Studio Online and TFS 2013\. Requires the updated [TFS 2013 Object Model](http://visualstudiogallery.msdn.microsoft.com/3278bfa7-64a7-4a75-b0da-ec4ccb8d21b6)
*   Optimized loading of VSO/TFS project members
*   Requires .NET Framework 4.5

### July 15, 2014

*   Bug fixes and improvements.

### April 21, 2014

*   Fixed issue with task card updates and attachments.

### March 31, 2014

*   Fixed issue with moving a task card onto the parent board and create target items (2-way sync) is enabled.
*   Fixed issue with deleting a task card or its parent.
*   Updated to the latest version of the LeanKit API Client Library.
*   Better exception handling for board updates.

### March 17, 2014

*   TFS: Updated exception handling around updating and creating LeanKit cards.
*   JIRA: Added support for updating epics.

### March 5, 2014

*   JIRA: Fixed issue with creating cards from new JIRA issues.
*   JIRA: Fixed issue causing the service to attempt to create cards that already exist.
*   JIRA: Fixed issue with creating a new card in LeanKit would create a "bug" issue type in JIRA, regardless of card type mapping.
*   GitHub: Fixed issue retrieving all available repositories.
*   GitHub: Fixed bad URL link to original GitHub issue.
*   GitHub: Fixed state-to-lane mapping issue.
*   GitHub: Fixed issue with card not moving if state of issue is updated in GitHub.