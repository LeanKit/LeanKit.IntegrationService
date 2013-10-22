LeanKit Integration Service
===========================

The LeanKit Integration Service synchronizes items between a Target system and LeanKit.

- Supported Target systems: TFS, JIRA, and GitHub
- Runs as a Windows service, or from command line for testing
- One Target + LeanKit account per instance
- Multiple Target projects / LeanKit boards per instance
- Map any status in the Target system to any lane on a LeanKit board
- Monitors the Target system, looking for new items and updates
- When new items are added to the Target system, new cards are added to LeanKit
- Monitors LeanKit boards for card moves and updates
- When new cards are added to LeanKit, new items are added to the Target system (optional)
- Supports state workflow (e.g. Active > Resolved > Closed)
- Target system query to check for new/updated items can be fully customized

Copyright
=========

Copyright &copy; 2013 LeanKit Inc.

License
=======

LeanKit Integration Service is licensed under [MIT](http://www.opensource.org/licenses/mit-license.php). Refer to license.txt for more information.