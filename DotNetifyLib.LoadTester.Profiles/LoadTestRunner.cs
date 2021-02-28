/*
Copyright 2021 Dicky Suryadi

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using DotNetify.LoadTester.Profiles;
using Microsoft.Extensions.Logging;

namespace DotNetify.LoadTester
{
   public static class LoadTestRunner
   {
      private class TestSettings
      {
         [Option('s', "server", Required = true, HelpText = "Hub server URL(s).")]
         public IEnumerable<string> Servers { get; set; }

         [Option('p', "profile", Required = false, HelpText = "Test profile [echo, sharedecho, broadcast, chatroom] (default: echo)")]
         public string Profile { get; set; } = "echo";

         [Option('c', "client", Required = false, HelpText = "Number of clients (default: 20).")]
         public uint Clients { get; set; } = 20;

         [Option('r', "run", Required = false, HelpText = "Run duration in seconds (default: 60).")]
         public uint RunDuration { get; set; } = 60;

         [Option('u', "rampup", Required = false, HelpText = "Ramp up period in seconds (default: 10).")]
         public uint RampUpPeriod { get; set; } = 10;

         [Option('d', "rampdown", Required = false, HelpText = "Ramp down period in seconds (default: 10).")]
         public uint RampDownPeriod { get; set; } = 10;

         [Option('i', "interval", Required = false, HelpText = "Message interval in milliseconds (default: 1000).")]
         public uint Interval { get; set; } = 1000;

         [Option('g', "group", Required = false, HelpText = "Number of multicast groups (default: 1).")]
         public uint Groups { get; set; } = 1;
      }

      public async static Task RunAsync(string[] args, ILoggerFactory loggerFactory)
      {
         TestSettings settings = new TestSettings();
         Parser.Default.ParseArguments<TestSettings>(args).WithParsed(o => settings = o);

         if (settings.Servers == null)
            return;

         settings.Servers = settings.Servers.Select(x => int.TryParse(x, out int _) ? $"https://localhost:{x}" : x);

         try
         {
            switch (settings.Profile.ToLower())
            {
               case "echo":
                  await RunEchoTestAsync(loggerFactory, settings);
                  break;

               case "sharedecho":
                  await RunSharedEchoTestAsync(loggerFactory, settings);
                  break;

               case "broadcast":
                  await RunBroadcastTestAsync(loggerFactory, settings);
                  break;

               case "chatroom":
                  await RunChatRoomTestAsync(loggerFactory, settings);
                  break;

               default:
                  Console.WriteLine($"Unknown profile: {settings.Profile}.");
                  return;
            }
         }
         catch (Exception ex)
         {
            loggerFactory.CreateLogger(nameof(LoadTestRunner)).LogError(ex.Message);
         }
      }

      private async static Task RunEchoTestAsync(ILoggerFactory loggerFactory, TestSettings settings)
      {
         var tasks = new List<Task>();
         foreach (var serverUrl in settings.Servers)
         {
            var task = EchoTest
               .Build(serverUrl, loggerFactory.CreateLogger(nameof(EchoTest)), settings.Clients, settings.Interval)
               .SetRampUpPeriod(TimeSpan.FromSeconds(settings.RampUpPeriod))
               .SetRampDownPeriod(TimeSpan.FromSeconds(settings.RampDownPeriod))
               .RunAsync(TimeSpan.FromSeconds(settings.RunDuration));

            tasks.Add(task);
         }

         await Task.WhenAll(tasks);
      }

      private async static Task RunSharedEchoTestAsync(ILoggerFactory loggerFactory, TestSettings settings)
      {
         var tasks = new List<Task>();
         foreach (var serverUrl in settings.Servers)
         {
            var task = SharedEchoTest
            .Build(serverUrl, loggerFactory.CreateLogger(nameof(SharedEchoTest)), settings.Clients, settings.Interval)
            .SetRampUpPeriod(TimeSpan.FromSeconds(settings.RampUpPeriod))
            .SetRampDownPeriod(TimeSpan.FromSeconds(settings.RampDownPeriod))
            .RunAsync(TimeSpan.FromSeconds(settings.RunDuration));

            tasks.Add(task);
         }

         await Task.WhenAll(tasks);
      }

      private async static Task RunBroadcastTestAsync(ILoggerFactory loggerFactory, TestSettings settings)
      {
         var tasks = new List<Task>();
         foreach (var serverUrl in settings.Servers)
         {
            var task = BroadcastTest
            .Build(serverUrl, loggerFactory.CreateLogger(nameof(BroadcastTest)), settings.Clients, settings.Interval)
            .SetRampUpPeriod(TimeSpan.FromSeconds(settings.RampUpPeriod))
            .SetRampDownPeriod(TimeSpan.FromSeconds(settings.RampDownPeriod))
            .RunAsync(TimeSpan.FromSeconds(settings.RunDuration));

            tasks.Add(task);
         }

         await Task.WhenAll(tasks);
      }

      private async static Task RunChatRoomTestAsync(ILoggerFactory loggerFactory, TestSettings settings)
      {
         var tasks = new List<Task>();
         foreach (var serverUrl in settings.Servers)
         {
            var task = ChatRoomTest
            .Build(serverUrl, loggerFactory.CreateLogger(nameof(ChatRoomTest)), settings.Clients, settings.Interval, settings.Groups)
            .SetRampUpPeriod(TimeSpan.FromSeconds(settings.RampUpPeriod))
            .SetRampDownPeriod(TimeSpan.FromSeconds(settings.RampDownPeriod))
            .RunAsync(TimeSpan.FromSeconds(settings.RunDuration));

            tasks.Add(task);
         }

         await Task.WhenAll(tasks);
      }
   }
}