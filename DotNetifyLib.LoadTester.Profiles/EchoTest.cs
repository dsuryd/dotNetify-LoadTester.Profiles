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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DotNetify.LoadTester.Profiles
{
   public static class EchoTest
   {
      public class EchoState
      {
         public EchoVM.Payload Ping { get; set; }
      }

      public class EchoStats
      {
         public string ConnectionId { get; set; }
         public uint Count { get; set; }
         public double AverageLatency { get; set; }
         public List<int> Sequences { get; } = new List<int>();
         public int Missed => Enumerable.Range(1, (int) Count).Except(Sequences).Count();
         public List<int> MissedSequences => Enumerable.Range(1, (int) Count).Except(Sequences).ToList();
         public int Misdelivered { get; set; }
      }

      public static LoadTestBuilder Build(string serverUrl, ILogger logger, uint numClients, uint pingInterval)
      {
         var clientStats = new ConcurrentDictionary<string, EchoStats>();
         var vmConnectOptions = new VMConnectOptions(new { PingInterval = pingInterval });

         logger.LogInformation($"Ping Interval={pingInterval}ms");

         return new LoadTestBuilder(serverUrl)
            .AddLogger(logger)
            .AddClient(numClients, (clientBuilder, index) =>
            {
               clientBuilder
                  .Connect(nameof(EchoVM), vmConnectOptions)
                  .OnServerResponse((IClientVM vm, ServerResponse response) =>
                  {
                     var state = response.As<EchoState>();
                     if (state.Ping != null)
                     {
                        EchoStats stat = clientStats.GetOrAdd(vm.ClientId, new EchoStats());
                        lock (stat)
                        {
                           if (!stat.Sequences.Contains(state.Ping.Sequence))
                           {
                              stat.Count++;
                              stat.AverageLatency = state.Ping.AvgLatency;
                              stat.Sequences.Add(state.Ping.Sequence);

                              if (stat.ConnectionId == null)
                                 stat.ConnectionId = state.Ping.ConnectionId;

                              if (state.Ping.ConnectionId != stat.ConnectionId)
                              {
                                 stat.Misdelivered++;
                                 logger.LogError($"[{vm.ClientId}] Misdelivered={stat.Misdelivered} (intended={state.Ping.ConnectionId})");
                              }
                           }
                        }

                        logger.LogTrace($"[{vm.ClientId}] Sequence={state.Ping.Sequence}, Prev Latency={state.Ping.PrevLatency}, Avg Latency={state.Ping.AvgLatency}, Received={stat.Sequences.Count}" + (stat.Missed > 0 ? $", Missed={stat.Missed}" : ""));

                        var args = new
                        {
                           Pong = new EchoVM.Payload
                           {
                              ConnectionId = stat.ConnectionId,
                              Time = state.Ping.Time,
                              Sequence = state.Ping.Sequence,
                              PrevLatency = state.Ping.PrevLatency,
                              AvgLatency = state.Ping.AvgLatency
                           }
                        };
                        vm.Dispatch(args);
                     }
                  })
                  ;
            })
            .OnCompleted(() =>
            {
               double allLatencySum = 0;
               double minLatency = double.MaxValue;
               double maxLatency = 0;
               int allMissed = 0;
               int allReceived = 0;
               int allMisdelivered = 0;
               foreach (var clientId in clientStats.Keys.OrderBy(x => x))
               {
                  var stat = clientStats[clientId];
                  allLatencySum += stat.AverageLatency;
                  allReceived += stat.Sequences.Count;
                  allMissed += stat.Missed;
                  allMisdelivered += stat.Misdelivered;
                  minLatency = stat.AverageLatency < minLatency ? stat.AverageLatency : minLatency;
                  maxLatency = stat.AverageLatency > maxLatency ? stat.AverageLatency : maxLatency;

                  if (stat.Missed > 0)
                     logger.LogError($"[{clientId}] Missed={JsonSerializer.Serialize(stat.MissedSequences)}");
               }

               double allAvgLatency = Math.Round(allLatencySum / clientStats.Count, 2);
               double pctMissed = Math.Round((double) allMissed / allReceived * 100, 2);

               logger.LogInformation($"Clients={clientStats.Count}, Received={allReceived}, Missed={pctMissed}%, Min latency={minLatency}, Max latency={maxLatency}, Avg latency: {allAvgLatency}");

               if (allMisdelivered > 0)
                  logger.LogError($"Misdelivered={allMisdelivered}");
            });
      }
   }
}