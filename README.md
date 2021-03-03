<p align="center"><img width="300px" src="http://dotnetify.net/content/images/dotnetify-logo.png"></p>

![alt build](https://github.com/dsuryd/dotNetify-LoadTester.Profiles/actions/workflows/build.yml/badge.svg)
[![NuGet](https://img.shields.io/nuget/v/DotNetify.LoadTester.Profiles.svg?style=flat-square)](https://www.nuget.org/packages/DotNetify.LoadTester.Profiles/)

## DotNetify.LoadTester.Profiles

This extension to [**DotNetify.LoadTester**](https://dotnetify.net/core/loadtester) provides a tool for [dotNetify sponsors](https://github.com/sponsors/dsuryd) to perform load  test on a dotNetify application server under common types of workloads.  If you are not a sponsor, you can still run this tool, but the number of clients will be limited to 5.

### How To Run

Run the TestAppServer project on the server machine, then run the TestRunner project from a command line on the client machine.  

The test runner supports the following options:
```
 -s, --server      Required. Hub server URL(s).
 -p, --profile     Test profile [echo, sharedecho, broadcast, chatroom] (default: echo)
 -c, --client      Number of clients (default: 5).
 -d, --rampdown    Ramp down period in seconds (default: 10).
 -i, --interval    Message interval in milliseconds (default: 1000).
 -g, --group       Number of multicast groups for chatroom (default: 1).
```

You can enable detailed logging by setting the log level in `appsettings.json` to `Trace`.

### Test Profiles

##### Echo

Continuous back and forth communication between the client and the server. The server sends a message to the client and waits for the response before sending the next message. Each client is served by its own view model instance.

The message payload contains sequence number and timestamp to allow the test to detect for undelivered messages and measure the average message latency.

##### Shared Echo

This profile is similar to **Echo**, but all clients sharing the same view model instance. The view model uses the connection ID from `IConnectionContext` to differentiate the clients.

##### Broadcast

The server pushes updates to all clients at regular intervals. A single multicast view model instance is used.

The message payload contains sequence number and timestamp to allow the test to detect for undelivered messages and measure the average interval between updates.

##### Chat Room

This profile models chat rooms where clients are sending and receiving messages with each other and within groups. The clients within a group are configured into 3 types:

- Chatty posters (10%): send message every 11 seconds.
- Casual posters (20%): send message every 59 seconds.
- Lurkers (70%): only receive messages.

The message payload contains sequence number and timestamp to allow the test to detect for undelivered messages and measure the average message latency.

 
