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
   public static class BroadcastTest
   {
      public class BroadcastState
      {
         public BroadcastVM.Payload Tick { get; set; }
      }

      public class BroadcastStats
      {
         public uint Count { get; set; }
         public List<int> Sequences { get; set; } = new List<int>();
         public DateTime LastTickTime { get; set; }
         public double IntervalSum { get; set; }
         public double AverageInterval => Count > 0 ? Math.Round(IntervalSum / Count, 2) : 0;
         public int Missed => MissedSequences.Count;
         public List<int> MissedSequences => Enumerable.Range(Sequences.Min(), (int) Count).Except(Sequences).ToList();
      }

      public static LoadTestBuilder Build(string serverUrl, ILogger logger, uint numClients, uint tickInterval)
      {
         var clientStats = new ConcurrentDictionary<string, BroadcastStats>();
         var vmConnectOptions = new VMConnectOptions(new { TickInterval = tickInterval });

         logger.LogInformation($"Tick Interval={tickInterval}ms");

         return new LoadTestBuilder(serverUrl)
            .AddLogger(logger)
            .AddClient(numClients, (clientBuilder, index) =>
            {
               clientBuilder
                  .Connect(nameof(BroadcastVM), vmConnectOptions)
                  .OnServerResponse((IClientVM vm, ServerResponse response) =>
                  {
                     var state = response.As<BroadcastState>();
                     if (state.Tick != null)
                     {
                        DateTime now = DateTime.Now;
                        BroadcastStats stat = clientStats.GetOrAdd(vm.ClientId, new BroadcastStats() { LastTickTime = now });
                        lock (stat)
                        {
                           if (!stat.Sequences.Contains(state.Tick.Sequence))
                           {
                              stat.Count++;
                              stat.IntervalSum += (now - stat.LastTickTime).TotalMilliseconds;
                              stat.LastTickTime = now;
                              stat.Sequences.Add(state.Tick.Sequence);
                           }
                        }

                        logger.LogTrace($"[{vm.ClientId}] Sequence={state.Tick.Sequence}, Avg Interval={stat.AverageInterval}, Received={stat.Sequences.Count}" + (stat.Missed > 0 ? $", Missed={stat.Missed}" : ""));
                     }
                  })
                  ;
            })
            .OnCompleted(() =>
            {
               double allIntervalSum = 0;
               double minInterval = double.MaxValue;
               double maxInterval = 0;
               int allMissed = 0;
               int allReceived = 0;
               foreach (var clientId in clientStats.Keys.OrderBy(x => x))
               {
                  var stat = clientStats[clientId];
                  allIntervalSum += stat.AverageInterval;
                  allReceived += stat.Sequences?.Count ?? 0;
                  allMissed += stat.Missed;
                  minInterval = stat.AverageInterval < minInterval ? stat.AverageInterval : minInterval;
                  maxInterval = stat.AverageInterval > maxInterval ? stat.AverageInterval : maxInterval;

                  if (stat.Missed > 0)
                     logger.LogError($"[{clientId}] Missed={JsonSerializer.Serialize(stat.MissedSequences)}");
               }

               double allAvgInterval = Math.Round(allIntervalSum / clientStats.Count, 2);
               double pctMissed = Math.Round((double) allMissed / allReceived * 100, 2);

               logger.LogInformation($"Clients={clientStats.Count}, Received={allReceived}, Missed={pctMissed}%, Min interval={minInterval}, Max interval={maxInterval}, Avg interval: {allAvgInterval}");
            });
      }
   }
}