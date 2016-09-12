# Table of Contents

*   [Overview](#overview)
*   [Requirements](#requirements)
    *   [Requirements for Visual Studio Team Services (VSTS) and Microsoft Team Foundation Server (TFS)](#requirements-vso-tfs)
        *   [Enable alternate credentials for VSTS](#vso-alternate-credentials)
    *   [Requirements for JIRA](#requirements-jira)
    *   [Update Card ID Settings in LeanKit](#card-id-settings)
*   [Installation](#installation)
    *   [Running from the Command-Line](#command-line)
    *   [Installing as a service](#installing-as-service)
    *   [Upgrading](#upgrading)
    *   [Uninstalling](#uninstalling)
    *   [Changing the default configuration port number](#default-port-number)
*   [Configuration](#configuration)
    *   [Launching the configuration management application](#config-mgmt)
    *   [Connecting to your LeanKit account](#connecting-to-leankit)
    *   [Connecting to your Target system](#connecting-to-target)
    *   [Global settings](#global-settings)
    *   [Mapping a Target project to a LeanKit board](#mapping)
    *   [Removing a Target system project to LeanKit board mapping](#remove-mapping)
*   [Troubleshooting](#troubleshooting)
    *   [Common Issues](#common-issues)
    *   [Viewing the logs](#logs)
    *   [Customize logging](#customize-logging)

# <a name="overview"></a>Overview

The LeanKit Integration Service synchronizes items between a Target system and LeanKit.

*   Supported Target systems: Visual Studio Team Services (VSTS), Microsoft Team Foundation Server (TFS), Atlassian JIRA, and GitHub
*   Runs as a Windows service, or from command line for testing
*   One Target + one LeanKit account per instance
*   Multiple Target projects / LeanKit boards per instance
*   Map any status in the Target system to any lane on a LeanKit board
*   Monitors the Target system, looking for new items and updates
*   When new items are added to the Target system, new cards are added to LeanKit
*   Monitors LeanKit boards for card moves and updates
*   When new cards are added to LeanKit, new items are added to the Target system (optional)
*   Supports state workflow (e.g. Active > Resolved > Closed)
*   Target system query to check for new/updated items can be fully customized
*   Synchronizes the following equivalent fields: title, description, priority, card type, tags, assigned to, due date, and card size.

![LeanKitIntegrationOverview_150.png](./images/LeanKitIntegrationOverview_150.png)

# <a name="requirements"></a>Requirements

Before installing and configuring the Integration Service, make sure the computer meets minimum requirements. The Integration Service requires a PC running Windows 7, Windows Server 2008, or later, and the [Microsoft .NET Framework 4.5](http://www.microsoft.com/en-us/download/details.aspx?id=30653).

## <a name="requirements-vso-tfs"></a>Requirements for Visual Studio Team Services (VSTS) and Microsoft Team Foundation Server (TFS)

### Install the TFS 2013 Object Model Client

The LeanKit Integration Service supports VSTS and TFS versions 2010, 2012, 2013, and 2015\. Regardless of the version used as the Target, the [Team Foundation Server 2013 Object Model client](http://visualstudiogallery.msdn.microsoft.com/3278bfa7-64a7-4a75-b0da-ec4ccb8d21b6) must be installed.

### <a name="vso-alternate-credentials"></a>Enable alternate credentials for Visual Studio Team Services

To connect to VSTS, you will need to create and enable "alternate credentials" for the VSTS account used to query and update work items in VSTS.

*   Sign in to the VSTS account you are integrating, such as https://your-account.visualstudio.com
*   In the top-right corner, click on your account name and then select _My Profile_
*   Select the _Credentials_ tab
*   Click the _Enable alternate credentials and set password_ link
*   Enter a password. It is suggested that you choose a unique password here (not associated with any other accounts)
*   Click _Save Changes_

## <a name="requirements-jira"></a>Requirements for JIRA

The minimum version of JIRA supported is 5.0.

## <a name="card-id-settings"></a>Update Card ID Settings in LeanKit

To function properly, the LeanKit boards that are synchronized with the LeanKit Integration Service need to have “Card ID” support enabled. The LeanKit Integration Service uses this feature to store the correlating item ID from the Target system.

1.  Sign in to your LeanKit account.
2.  Navigate to the board you will be synchronizing with your Target system.
3.  Click the **Configure Board Settings** icon (see the following image).
4.  Click the **Card ID Settings** tab.
5.  Click **Display external card ID field in card edit dialog**.
6.  Click **Enable header** (optional) and choose **Display text from external card ID field**.
7.  Click **Save and Close**.

![image21.gif](./images/image21.gif)

![leankit-card-id-settings.gif](./images/leankit-card-id-settings.gif)

# <a name="installation"></a>Installation

1. [Download](./downloads.md) the LeanKit Integration Service .zip file.
1. Extract the LeanKit Integration Service into a folder on the computer where the service will be installed.
1. Open an **Administrator** command prompt, and change the current directory to where the Integration Service is extracted.

## <a name="command-line"></a>Running from the Command-Line

To run the Integration Service as a command-line application, simply run the executable. Type `IntegrationService.exe` and press ENTER.

![image22.gif](./images/image22.gif)

Running the LeanKit Integration Service from the command-line is useful for watching the activity between the Target system and LeanKit.

![image00.gif](./images/image00.gif)

## <a name="installing-as-service"></a>Installing as a service

Open a command prompt as an administrator, and type the following:

`> IntegrationService.exe install`

The service will be installed and started immediately. By default, the Integration Service runs as the local service account.

![image17.gif](./images/image17.gif)

### Installing more than one instance of the service

First, create separate folders for each instance, and copy all the files for the Integration Service into each folder. Next, open a command prompt as an administrator, and type the following for each instance, replacing [instance_name] with the desired instance name:

`> IntegrationService.exe install /instance:[instance_name]`

The service will be installed using the service name plus the instance name, and started immediately.

## <a name="upgrading"></a>Upgrading

1.  Download and extract latest version of the LeanKit Integration Service into a separate folder.
2.  Make a backup of the following files, for safe keeping: **config-live.json**, **config-edit.json**, and **IntegrationService.exe.config**.
3.  Stop the existing Integration Service, if it is running.
4.  Copy and overwrite all the files from the latest LeanKit Integration Service.
5.  Restart the LeanKit Integration Service.

## <a name="uninstalling"></a>Uninstalling

Open a command prompt as an administrator, and type the following:

`> IntegrationService.exe uninstall`

![image18.gif](./images/image18.gif)

### Uninstalling a named instance

Open a command prompt as an administrator, and type the following:

`> IntegrationService.exe uninstall /instance:[instance_name]`

## <a name="default-port-number"></a>Changing the default configuration port number

Locate and open the file IntegrationService.exe.config in a text editor. Find the application setting “ConfigurationSitePort” and change the value to the desired port number. Save the file, and restart the service.

```
<?xml version="1.0" encoding="utf-8"?>
<configuration> 
  <appSettings> 
    <add key="ConfigurationSitePort" value="8090" />
  </appSettings> 
  ... 
</configuration>
```

# <a name="configuration"></a>Configuration

The LeanKit Integration Service includes a web-based management application for configuring the connections and mappings between one or more projects in a Target system and one or more LeanKit boards.

## <a name="config-mgmt"></a>Launching the configuration management application

1.  Open a web browser on the machine where the LeanKit Integration Service is installed.
2.  Browse to [http://localhost:8090/](http://localhost:8090/)

By default, the web interface for managing the LeanKit Integration Service is available at [http://localhost:8090/](http://localhost:8090/). To change the port number (8090), see [Changing the default configuration port number](#default-port-number).

## <a name="connecting-to-leankit"></a>Connecting to your LeanKit account

The first time you access the LeanKit Integration Service configuration application, you will be prompted to connect to your Leankit account. The LeanKit account user must have permission to access, create, and update cards with the boards you wish to integrate with your Target system.

1.  Enter your account name. Your account name can be found in the URL you use to access your LeanKit account.
2.  Enter your account email address.
3.  Enter your password.
4.  Click **Connect**.

![image14.gif](./images/image14.gif)

Once you successfully connect to your LeanKit account, click **Next: Connect To Target** to continue.

![image09.gif](./images/image09.gif)

## <a name="connecting-to-target"></a>Connecting to your Target system

1.  Choose the type of Target system you are connecting to. Choices are TFS, JIRA, GitHub Issues, and GitHub Pull Requests.  
    _  
    Note: If you are connecting to Visual Studio Team Services, choose **TFS**. You must also [enable alternate credentials](#vso-alternate-credentials).  

    _
2.  Enter your host address.  

    If required for self-hosted TFS or JIRA, change the host prefix from https:// to http://  

    For Visual Studio Team Services or TFS, also include the name of the project collection.  

    For GitHub, enter only the name of the account or organization associated with the desired repository.  

3.  Enter your account user name.  

    _Note: For **JIRA**, you must use your account **username** instead of your email address.Your username can be found by going to your account Profile._  

4.  Enter your password.
5.  Click **Connect**.

![image12.gif](./images/image12.gif)

Once you successfully connect to your Target system, click **Next: Check Global Settings** to continue.

![image02.gif](./images/image02.gif)

## <a name="global-settings"></a>Global settings

**Check Target for updates every [____] milliseconds**. By default, the LeanKit Integration Service will check the Target system every 60,000 milliseconds (60 seconds) for new changes.

**Synchronize items created after: [____]**. By default, the LeanKit Integration Service will check only items created after January 1, 2013.

Once you updated or verified these global settings, click **Next: Configure Boards and Projects** to continue.

![image15.gif](./images/image15.gif)

## <a name="mapping"></a>Mapping a Target project to a LeanKit board

1.  On the left, select a LeanKit board.
2.  On the right, select a Target project from the drop-down list.
3.  Click **Configure…** to continue.

![image01.gif](./images/image01.gif)

![image24.gif](./images/image24.gif)

### Choose how LeanKit cards are created

Under the **Selection** tab, choose the project statuses and Target item types that you wish to synchronize with your LeanKit board.

_Note: For Visual Studio Team Services or TFS accounts, you may also select a project Iteration Path to further refine the items that are selected for synchronization._

![image16.gif](./images/image16.gif)

#### Using a custom Target query

The LeanKit Integration Service supports custom queries for selecting items from the Target system to synchronize with your LeanKit board. When you select **Custom Query**, the **Simple Selection** area will be hidden, and a text box will appear where you may enter a custom query.

![image07.gif](./images/image07.gif)

#### Using a custom query for Visual Studio Team Services or TFS

The LeanKit Integration Service supports custom Work Item queries. You may enter any valid Work Item query. Your Work Item query must include the following:

`[System.ChangedDate] > '{0}'`

This is a placeholder used to filter items in the query to only those that have changed since the last date and time Work Items were found for synchronization.

The default Work Item query used by the LeanKit Integration Service is:

```
[System.TeamProject] = '<ProjectName>' 
  AND [System.IterationPath] UNDER '<ProjectName>\\<IterationName>' 
  AND ([System.State] = '<First State Selected>' 
       OR [System.State] = '<Second State Selected>' OR ... ) 
  AND ([System.WorkItemType] <> '<First Excluded Type Selected>' 
       AND [System.WorkItemType] <> '<Second Excluded Type Selected>' AND ...) 
  AND [System.ChangedDate] > '{0}'
```

A full explanation of the query capabilities available for Visual Studio Team Services and TFS is beyond the scope of this documentation. Please consult your Work Item query documentation.

#### Using a custom query for JIRA

The LeanKit Integration Service supports custom queries with JIRA using the JIRA Query Language (JQL). You may enter any valid JQL. Your custom JIRA query must include the following:

`updated > '{0}'`

This is a placeholder used to filter items in the query to only those that have changed since the last date and time JIRA issues were found for synchronization.

The default JIRA query used by the LeanKit Integration Service is:

```
project='<Project Name>' 
and (status='<First Selected Status>' or status='<Second Selected Status>' [or <additional statuses>]) 
and updated > '{0}'   
order by created asc
```

A full explanation of the query capabilities available for JIRA is beyond the scope of this documentation. Please consult your JIRA Query Language documentation.

#### Using a custom query for GitHub

The LeanKit Integration Service does not support custom queries for GitHub. Please use the **Simple Selection** option.

### Assign Target project item statuses to LeanKit board lanes

Under the **Lanes and States** tab, click on a LeanKit board lane, and then select the Target project item statuses (states) that are appropriate for that lane. When new Target items are created or updated, a LeanKit card will be created in, or optionally moved to, the equivalent lane on the board. As cards are moved to different lanes, the associated Target item will be updated to the equivalent status.

In the following example, we are choosing Target item states “New,” “Ready,” and “To Do” to be assigned (mapped) to the LeanKit “To Do” lane.

![image19.gif](./images/image19.gif)

_Note: All statuses checked on the Selection tab must be mapped to a lane. The required statuses will remain highlighted until they have been added._

To remove an assigned status from a lane, click the ‘X’ next to the status.

#### Adding a custom state workflow

In some cases, a Target project may not allow an item’s state to move directly from state to another in one step. These projects enforce a “state workflow” that an item must progress through. You can add one or more workflows to each LeanKit lane to express the order of states an item must go through to match the equivalent state of the lane.

To add a new workflow:

1.  Select the appropriate lane.
2.  Check the box for **Build Workflow**.
3.  Select each state, in the correct order.
4.  Click the **check mark icon** when finished.

![image05.gif](./images/image05.gif)

_Note: The order of states and workflows added to a lane will be the same order the LeanKit Integration Service will attempt to set a Target system’s item state when updating._

### Map Target item types to LeanKit card types

Under the **Card Types** tab, you can customize how the Target project’s item types are mapped to LeanKit card types.

To create a new card type mapping:

1.  Click the **Add** button.
2.  Choose a LeanKit card type from the first drop-down list.
3.  Choose a Target item type from the second drop-down list.
4.  Click the **check mark icon** to save.

![image10.gif](./images/image10.gif)

To remove a card type mapping, click the ‘X’ icon next to the listed mapping.

### Synchronization options

Under the **Options** tab, you can customize how the LeanKit Integration Service will keep the Target project and LeanKit board synchronized.

**Create a new corresponding LeanKit card:** When enabled, a new LeanKit card will be created each time a new item is created in the Target system that matches the selection criteria.

**Create a new corresponding Target item:** When enabled, a new Target item will be created each time a new LeanKit card is added to the board being monitored.

**Update Cards:** When enabled and a Target item is updated, the associated LeanKit card will be updated (e.g. Title, Description, or Due Date). This does not apply to changes to Target item state (see next).

**Move Cards when State changes:** When enabled and a Target item’s state is updated, the associated LeanKit card will be moved to the equivalent lane (if mapped).

**Update Target Items:** When enabled and a LeanKit card is updated or moved, the associated Target item will be updated (e.g. Title, Description, or Due Date).

**When creating LeanKit cards, tag the card with the Target system:** When enabled and a new card is created, the card will be tagged with the name of the Target system (e.g. TFS or JIRA). This is useful when integrating more than one Target system with a single LeanKit board.

![image13.gif](./images/image13.gif)

### Saving a configuration

Whenever you make changes to a LeanKit board to Target project mapping, please be sure to click the **Save** button to save your changes.

![image11.gif](./images/image11.gif)

### Activating and restarting the LeanKit Integration Service

The **Activate…** tab is used to review and enable changes to your configuration. After creating or updating a Target system and LeanKit board configuration, you must activate the new configuration before the LeanKit Integration Service will recognize those changes. A summary report is displayed of all the Target system project to LeanKit board mappings and configured options. After reviewing this report, click the **Activate Now** button to restart the service and enable the new or updated configuration.

![image08.gif](./images/image08.gif)

## <a name="remove-mapping"></a>Removing a Target system project to LeanKit board mapping

1.  If not already selected, click on the **Board Configuration** tab.
2.  On the left, click the LeanKit board and Target project mapping you wish to remove.
3.  On the right, click on the **Options** tab.
4.  At the bottom, click on **Remove Mapping…**.
5.  Click **Remove** to confirm.
6.  Click on the **Activate…** tab.
7.  Click **Activate Now** to restart the service with the new configuration.

_Note: Removing a mapping will stop items from being synchronized. However, it will **not** remove any items that have been created in LeanKit or the Target system as a result of the previous configuration._

![image04.gif](./images/image04.gif)

# <a name="troubleshooting"></a>Troubleshooting

## <a name="common-issues"></a>Common Issues

When running the LeanKit Integration Service the first time, you see:

**IntegrationService has Stopped Working**

-or-

**System.BadImageFormatException**

This could be caused by not having the Microsoft .NET Framework 4.5 installed. Please see [Requirements](#requirements).

When trying to access the management application with your browser, you see:

**ERR_CONNECTION_RESET**

This may be due to a local firewall issue. You may need to either allow port 8090, or change the port number to something that is allowed by your local firewall, e.g. 8080\. See [Changing the default configuration port number](#default-port-number).

When trying to connect to LeanKit, you see:

**Could not connect to LeanKit. Please verify your credentials.**

This could be due to the Integration Service being installed behind a proxy server. To configure the Integration Service to use a proxy server, edit IntegrationService.exe.config, and update the following configuration section just before the closing </configuration> tag. It should look something like:

```
<system.net>  
  <defaultProxy>  
    <proxy bypassonlocal="true" usesystemdefault="true" />  
  </defaultProxy>  
</system.net>
```

Add a "useDefaultCredentials" setting.

```
<system.net>  
  <defaultProxy useDefaultCredentials="true">  
    <proxy bypassonlocal="true" usesystemdefault="true" />  
  </defaultProxy>  
</system.net>
```

Or, if more granular control of the proxy is required, it could look something like:

```
<system.net>  
  <defaultProxy enabled="true" useDefaultCredentials="false">  
    <proxy usesystemdefaults="true" proxyaddress="http://192.168.1.10:3128" bypassonlocal="true" />  
  </defaultProxy>  
</system.net>
```

For more proxy help, please review the documentation on [.NET proxy configuration](http://msdn.microsoft.com/en-us/library/kd3cf2ex(v=vs.110).aspx).

## <a name="logs"></a>Viewing the logs

The LeanKit Integration Service logs all informational and error messages to the `[InstallPath]/Log` folder. A log file can assist in troubleshooting connection or synchronization issues. Each day’s logs will be stored in a file with the name `[yyyyMMdd].txt`.

## <a name="customize-logging"></a>Customize logging

The LeanKit Integration Service uses [log4net](http://logging.apache.org/log4net/), a very flexible open-source logging utility that can log activity to a variety of outputs. For example, log4net can be configured to log to a database such as SQL Server or Oracle, or send an email whenever an error occurs.

To customize how logs are generated, locate and edit the `logging.config` file found in the folder where the LeanKit Integration Service is installed. There are many examples provided in the [log4net documentation](http://logging.apache.org/log4net/release/config-examples.html).