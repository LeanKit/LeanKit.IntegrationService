# LeanKit Integration Service

* [Installation and Overview Guide](./docs/overview.md)
* [Change log](./docs/changelog.md)
* [Downloads](./docs/downloads.md)

The **LeanKit Integration Service** synchronizes items between a Target system and LeanKit.

- Supported Target systems: Visual Studio Online, Microsoft Team Foundation Server (TFS) 2010/2012/2013, JIRA 5.x+, and GitHub
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

- Microsoft .NET 4.5, or higher
- Visual Studio 2012 with Update 3, or higher
- [node.js](http://nodejs.org/) and [Grunt](http://gruntjs.com/) -- to bundle and minify JavaScript files for *Release* builds
- [Team Foundation Server 2013 Object Model Installer](http://visualstudiogallery.msdn.microsoft.com/3278bfa7-64a7-4a75-b0da-ec4ccb8d21b6)\*

\* *If you do not need support for Visual Studio Online or Team Foundation Server, you can remove references to `IntegrationService.Targets.TFS` from your projects.*

### Configuring node.js

- Download and install node.js. Allow node.js to be added to your PATH.
- From a command line, execute the following commands:

```
npm install -g grunt
npm install -g grunt-cli
```

- From a command line, change to the `IntegrationService/Build` directory and execute:

```
npm install
```

This will read the package.json file and install additional dependencies for building the project.

## Questions?

Visit [support.leankit.com](http://support.leankit.com).

## Copyright

Copyright &copy; 2013-2014 LeanKit Inc.

## License

LeanKit Integration Service is licensed under [MIT](http://www.opensource.org/licenses/mit-license.php). Refer to license.txt for more information.
