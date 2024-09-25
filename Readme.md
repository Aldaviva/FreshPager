<img src="FreshPager/freshping.ico" alt="Freshping" height="24" /> FreshPager
===

[![GitHub Actions](https://img.shields.io/github/actions/workflow/status/Aldaviva/FreshPager/dotnet.yml?branch=master&logo=github)](https://github.com/Aldaviva/FreshPager/actions/workflows/dotnet.yml) [![Testspace](https://img.shields.io/testspace/tests/Aldaviva/Aldaviva:FreshPager/master?passed_label=passing&failed_label=failing&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCA4NTkgODYxIj48cGF0aCBkPSJtNTk4IDUxMy05NCA5NCAyOCAyNyA5NC05NC0yOC0yN3pNMzA2IDIyNmwtOTQgOTQgMjggMjggOTQtOTQtMjgtMjh6bS00NiAyODctMjcgMjcgOTQgOTQgMjctMjctOTQtOTR6bTI5My0yODctMjcgMjggOTQgOTQgMjctMjgtOTQtOTR6TTQzMiA4NjFjNDEuMzMgMCA3Ni44My0xNC42NyAxMDYuNS00NFM1ODMgNzUyIDU4MyA3MTBjMC00MS4zMy0xNC44My03Ni44My00NC41LTEwNi41UzQ3My4zMyA1NTkgNDMyIDU1OWMtNDIgMC03Ny42NyAxNC44My0xMDcgNDQuNXMtNDQgNjUuMTctNDQgMTA2LjVjMCA0MiAxNC42NyA3Ny42NyA0NCAxMDdzNjUgNDQgMTA3IDQ0em0wLTU1OWM0MS4zMyAwIDc2LjgzLTE0LjgzIDEwNi41LTQ0LjVTNTgzIDE5Mi4zMyA1ODMgMTUxYzAtNDItMTQuODMtNzcuNjctNDQuNS0xMDdTNDczLjMzIDAgNDMyIDBjLTQyIDAtNzcuNjcgMTQuNjctMTA3IDQ0cy00NCA2NS00NCAxMDdjMCA0MS4zMyAxNC42NyA3Ni44MyA0NCAxMDYuNVMzOTAgMzAyIDQzMiAzMDJ6bTI3NiAyODJjNDIgMCA3Ny42Ny0xNC44MyAxMDctNDQuNXM0NC02NS4xNyA0NC0xMDYuNWMwLTQyLTE0LjY3LTc3LjY3LTQ0LTEwN3MtNjUtNDQtMTA3LTQ0Yy00MS4zMyAwLTc2LjY3IDE0LjY3LTEwNiA0NHMtNDQgNjUtNDQgMTA3YzAgNDEuMzMgMTQuNjcgNzYuODMgNDQgMTA2LjVTNjY2LjY3IDU4NCA3MDggNTg0em0tNTU3IDBjNDIgMCA3Ny42Ny0xNC44MyAxMDctNDQuNXM0NC02NS4xNyA0NC0xMDYuNWMwLTQyLTE0LjY3LTc3LjY3LTQ0LTEwN3MtNjUtNDQtMTA3LTQ0Yy00MS4zMyAwLTc2LjgzIDE0LjY3LTEwNi41IDQ0UzAgMzkxIDAgNDMzYzAgNDEuMzMgMTQuODMgNzYuODMgNDQuNSAxMDYuNVMxMDkuNjcgNTg0IDE1MSA1ODR6IiBmaWxsPSIjZmZmIi8%2BPC9zdmc%2B)](https://aldaviva.testspace.com/spaces/283429) [![Coveralls](https://img.shields.io/coveralls/github/Aldaviva/FreshPager?logo=coveralls)](https://coveralls.io/github/Aldaviva/FreshPager?branch=master)

When Freshping detects an outage, trigger an alert in PagerDuty (and resolve when it's up again).

This is helpful because Freshping's only built-in notification system is email, and the Gmail Android app is extremely slow to notify you of new messages: notifications are often hours late. PagerDuty, on the other hand, has realtime alerting with SMS messages and mobile app push notifications for Android and iOS.

This is a free, open-source, no sign-up, self-hostable alternative to the [Zapier Freshping + PagerDuty integration](https://zapier.com/apps/freshping/integrations/pagerduty/1146377/trigger-new-incidents-in-pagerduty-for-new-freshping-alerts).

<!-- MarkdownTOC autolink="true" bracket="round" autoanchor="false" levels="1,2,3" bullets="1.,-,-,-" -->

1. [Prerequisites](#prerequisites)
1. [Installation](#installation)
1. [Configuration](#configuration)
1. [Execution](#execution)
1. [Signal Flow](#signal-flow)

<!-- /MarkdownTOC -->

## Prerequisites
- [.NET Runtime 8 or later](https://dotnet.microsoft.com/en-us/download)
- [Freshping account](https://freshping.io/)
    - ⛔ [Freshworks disabled the ability to sign up for new Freshping accounts](https://support.freshping.io/en/support/solutions/articles/50000006524-suspension-of-new-signups-faqs) in 2023, so if you don't already have an account, you can't create one anymore.
    - All billing plans are compatible: Sprout (free, 5 integrations), Blossom (10 integrations), and Garden (15 integrations)
- Ability to listen on a public WAN TCP port for incoming HTTP requests from Freshping without being blocked by a NAT or firewall
- [PagerDuty account](https://www.pagerduty.com/sign-up/) (the [free plan](https://www.pagerduty.com/sign-up-free/?type=free) is sufficient)

## Installation
1. Download the ZIP file for your operating system and CPU architecture from the [latest release page](https://github.com/Aldaviva/FreshPager/releases/latest).
1. Extract the ZIP file to a directory of your choice, such as `C:\Program Files\FreshPager\` or `/opt/freshpager/`.
    - When installing updates, don't overwrite `appsettings.json`.
1. Install the service so it will start automatically when your computer boots.
    - Windows: `& '.\Install service.ps1'`
        - If this PowerShell script doesn't run, try removing the Mark of the Web by unblocking the file or calling `Set-ExecutionPolicy RemoteSigned`.
    - Linux with systemd:
        ```sh
        sudo cp freshpager.service /etc/systemd/system/
        sudo systemctl daemon-reload
        sudo systemctl enable freshpager.service
        ```
        - If the installation directory is not `/opt/freshpager/`, make sure to update `freshpager.service` to match.

## Configuration
1. Create an Integration in PagerDuty and get its Integration Key.
    1. Sign into your [PagerDuty account](https://app.pagerduty.com/).
    1. Go to Services › Service Directory.
    1. Select an existing Service for which you want to publish events, or create a new Service.
    1. In the Integrations tab of the Service, add a new Integration.
    1. Under Most popular integrations, select Events API V2, then click Add.
    1. Expand the newly-created Integration and copy its **Integration Key**, which will be used to authorize this program to send Events to the correct Service.
1. Edit the `appsettings.json` configuration file.
    |Key|Example Value|Description|
    |-|-|-|
    |`pagerDutyIntegrationKeysByFreshpingCheck`|<pre lang="json">{ "My Server": "y5mfp…" }</pre>|Object where each key is the name of a check in Freshping, and its value is the Integration Key you created for the matching PagerDuty Service in Step 1.|
    |`kestrel.endpoints.https.url`|`http://0.0.0.0:37374`|URI containing the TCP port on which to listen for HTTP requests from the Freshping webhook client. Must be publicly accessible on the WAN.|
1. Create Webhook integration in Freshping.
    1. Sign into your [Freshworks account](https://login.freshworks.com/email-login).
    1. Go to your Freshping Dashboard.
    1. Go to Settings › Integrations.
    1. Under Webhook, select **+ Create Integration**.
    1. Set the Webook Name to any name you want.
    1. Set the Event Type to Up/Down.
    1. Select the Checks that should trigger the alert.
    1. Set the Callback URL to the location of your FreshPager server, such as `http://myserver.example.com:37374/`.
    1. Leave the request body set to Simple.
    1. Click Save.

## Execution
1. Start the service.
    - Windows: `Restart-Service Freshpager`
    - Linux with systemd: `sudo systemctl restart freshpager.service`

## Signal Flow
1. <img src="FreshPager/freshping.ico" alt="Freshping" height="12" /> Freshping detects and confirms that a Check is down.
1. <img src="FreshPager/freshping.ico" alt="Freshping" height="12" /> Freshping sends an HTTP POST request to each Webhook integration subscribed to Up/Down events on that Check.
1. Your <img src="https://gravatar.com/avatar/53218ea2108534d012156993e92f2e35?size=12" alt="Aldaviva" height="12" /> FreshPager server receives the HTTP POST request from <img src="FreshPager/freshping.ico" alt="Freshping" height="12" /> Freshping.
1. <img src="https://gravatar.com/avatar/53218ea2108534d012156993e92f2e35?size=12" alt="Aldaviva" height="12" /> FreshPager looks up the Integration Key in its configuration based on the Check name from the request body.
1. <img src="https://gravatar.com/avatar/53218ea2108534d012156993e92f2e35?size=12" alt="Aldaviva" height="12" /> FreshPager sends an Events API V2 request to <img src="https://raw.githubusercontent.com/Aldaviva/PagerDuty/master/PagerDuty/icon.png" alt="PagerDuty" height="12" /> PagerDuty to trigger an alert on the Service that contains the Integration Key.
1. <img src="https://raw.githubusercontent.com/Aldaviva/PagerDuty/master/PagerDuty/icon.png" alt="PagerDuty" height="12" /> PagerDuty creates a new incident for this alert, and returns a unique key for this alert, which <img src="https://gravatar.com/avatar/53218ea2108534d012156993e92f2e35?size=12" alt="Aldaviva" height="12" /> FreshPager stores in memory.
1. When <img src="FreshPager/freshping.ico" alt="Freshping" height="12" /> Freshping detects that the Check is up again, it sends another POST request to <img src="https://gravatar.com/avatar/53218ea2108534d012156993e92f2e35?size=12" alt="Aldaviva" height="12" /> FreshPager, which resolves the previously-created <img src="https://raw.githubusercontent.com/Aldaviva/PagerDuty/master/PagerDuty/icon.png" alt="PagerDuty" height="12" /> PagerDuty alert using the same unique key.
