The **LeanKit Integration Service** synchronizes items between a Target system and LeanKit.

- Supported Target systems: Microsoft Team Foundation Server (TFS), JIRA, and GitHub
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

## Building

To build the LeanKit Integration Service from source, you will need the following:

- Visual Studio 2012 with Update 3, or higher
- [node.js](http://nodejs.org/) and [Grunt](http://gruntjs.com/) -- to bundle and minify JavaScript files for *Release* builds
- [Microsoft Visual Studio Team Explorer 2010](http://www.microsoft.com/en-us/download/details.aspx?id=329)\*
- [Team Explorer for Microsoft Visual Studio 2012](http://www.microsoft.com/en-us/download/details.aspx?id=30656)\*

\* *If you do not need support for TFS, you can remove references to `IntegrationService.Targets.TFS` from your projects.*

### Configuring node.js

- Download and install node.js. Allow node.js to be added to your PATH.
- From a command line, execute the following commands:

    npm install -g grunt
    npm install -g grunt-cli

- From a command line, change to the `IntegrationService/Build` directory and execute:

    npm install

This will read the package.json file and install additional dependencies for building the project.
	
## Questions?

Visit [support.leankit.com](http://support.leankit.com).

## Copyright

Copyright &copy; 2013 LeanKit Inc.

## License

LeanKit Integration Service is licensed under [MIT](http://www.opensource.org/licenses/mit-license.php). Refer to license.txt for more information.