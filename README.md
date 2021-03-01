<p align="center"><img width="300px" src="http://dotnetify.net/content/images/dotnetify-logo.png"></p>

![alt build](https://github.com/dsuryd/dotNetify-LoadTester.Profiles/actions/workflows/build.yml/badge.svg)
[![NuGet](https://img.shields.io/nuget/v/DotNetify.LoadTester.Profiles.svg?style=flat-square)](https://www.nuget.org/packages/DotNetify.LoadTester.Profiles/)

## DotNetify.LoadTester.Profiles

This extension to **DotNetify.LoadTester** comes with view models to test common workloads in dotNetify applications and a CLI-based test runner.

### Installation

Start by creating a .NET Core console project, then add the NuGet library \*DotNetify.LoadTester.Profiles\*. In the `Main` method, pass the arguments to **LoadTestRunner.RunAsync**:

```csharp
using DotNetify.LoadTester;
...

private async static Task Main(string[] args)
{
    var loggerFactory = LoggerFactory.Create(configure => configure.AddConsole());
    await LoadTestRunner.RunAsync(args, loggerFactory);
}
```

Run the project from the command prompt with the following arguments:

```
 -s, --server      Required. Hub server URL(s).
 -p, --profile     Test profile [echo, sharedecho, broadcast, chatroom] (default: echo)
 -c, --client      Number of clients (default: 20).
 -d, --rampdown    Ramp down period in seconds (default: 10).
 -i, --interval    Message interval in milliseconds (default: 1000).
 -g, --group       Number of multicast groups (default: 1).
```

You will need to point it to a dotNetify application server. Assuming you already have one, the last step is to add the same NuGet library to the project and register the profile view models:

```csharp
using DotNetify.LoadTester;
...

app.UseDotNetify(config =>
{
  config.RegisterLoadProfiles();
});
```

### Workload Profiles

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

 
