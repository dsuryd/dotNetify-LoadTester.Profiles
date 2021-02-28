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
   public static class ChatRoomTest
   {
      public class ChatState
      {
         public ChatRoomVM.Payload Messages_add { get; set; }
      }

      public class FriendStats
      {
         public uint Count { get; set; }
         public DateTime LastTickTime { get; set; }
         public double LatencySum { get; set; }
         public double AverageLatency => Count > 0 ? Math.Round(LatencySum / Count, 2) : 0;
         public double IntervalSum { get; set; }
         public double AverageInterval => Count > 0 ? Math.Round(IntervalSum / Count, 2) : 0;
         public List<int> Sequences { get; } = new List<int>();
         public int Missed => MissedSequences.Count;
         public List<int> MissedSequences => Enumerable.Range(Sequences.Min(), (int) Count).Except(Sequences).ToList();
      }

      public class ChatStats
      {
         public DateTime LastLogTime { get; set; }
         public ConcurrentDictionary<string, FriendStats> FriendStats { get; } = new ConcurrentDictionary<string, FriendStats>();
      }

      private static uint LogInterval = 1000;

      public static LoadTestBuilder Build(string serverUrl, ILogger logger, uint numClients, uint messageInterval, uint numGroups)
      {
         var clientStats = new ConcurrentDictionary<string, ChatStats>();
         var clientMessageSequences = new ConcurrentDictionary<string, int>();
         var random = new Random();

         uint chattyPosters = numClients >= 10 ? numClients / 10 : numClients;
         uint casualPosters = numClients >= 10 ? numClients / 5 : 0;
         uint lurkers = numClients - chattyPosters - casualPosters;

         string getRoom(uint index) => $"Room{(index % numGroups) + 1}";
         VMConnectOptions getVMConnectOptions(uint index, string clientId) => new VMConnectOptions(new { ChatRoom = getRoom(index), ClientId = clientId });

         var chattyInterval = messageInterval * 11;
         var casualInterval = messageInterval * 59;
         logger.LogInformation($"Rooms={numGroups}, Chatty={chattyPosters}, Chatty Interval={chattyInterval}ms, Casual={casualPosters}, Casual Interval={casualInterval}ms, Lurkers={lurkers}");

         LogInterval = (chattyPosters + casualPosters) * (20000 / messageInterval);
         logger.LogTrace($"Log interval={LogInterval}ms");

         return new LoadTestBuilder(serverUrl)
            .AddLogger(logger)

            // Lurkers.
            .AddClient(lurkers, (clientBuilder, index) =>
            {
               clientBuilder
                  .AddPrefix(getRoom(index) + "-lurker")
                  .Connect(nameof(ChatRoomVM), getVMConnectOptions(index, clientBuilder.ClientId))
                  .OnServerResponse((IClientVM vm, ServerResponse response) => OnServerResponse(vm, response, logger, clientStats))
                  ;
            })
            // Chatty posters.
            .AddClient(chattyPosters, (clientBuilder, index) =>
            {
               clientBuilder
                  .AddPrefix(getRoom(index) + "-chatty")
                  .Connect(nameof(ChatRoomVM), getVMConnectOptions(index, clientBuilder.ClientId))
                  .Wait(100 * index)
                  .Dispatch(vm => Dispatch(vm, clientMessageSequences))
                  .RepeatContinuously(chattyInterval)
                  .OnServerResponse((IClientVM vm, ServerResponse response) => OnServerResponse(vm, response, logger, clientStats))
                  ;
            })
            // Occasional posters.
            .AddClient(casualPosters, (clientBuilder, index) =>
            {
               clientBuilder
                  .AddPrefix(getRoom(index))
                  .Connect(nameof(ChatRoomVM), getVMConnectOptions(index, clientBuilder.ClientId))
                  .Wait(100 * index)
                  .Dispatch(vm => Dispatch(vm, clientMessageSequences))
                  .RepeatContinuously(casualInterval)
                  .OnServerResponse((IClientVM vm, ServerResponse response) => OnServerResponse(vm, response, logger, clientStats))
                  ;
            })
            .OnCompleted(() =>
            {
               int allMissed = 0;
               int allReceived = 0;
               double allLatencySum = 0;
               double minLatency = double.MaxValue;
               double maxLatency = 0;

               foreach (var clientId in clientStats.Keys.OrderBy(x => x))
               {
                  var stat = clientStats[clientId];
                  foreach (var friendStat in stat.FriendStats)
                  {
                     allReceived += friendStat.Value.Sequences.Count;
                     allMissed += friendStat.Value.Missed;
                     allLatencySum += friendStat.Value.LatencySum;
                     minLatency = friendStat.Value.AverageLatency < minLatency ? friendStat.Value.AverageLatency : minLatency;
                     maxLatency = friendStat.Value.AverageLatency > maxLatency ? friendStat.Value.AverageLatency : maxLatency;

                     if (friendStat.Value.Missed > 0)
                        logger.LogError($"[{clientId}] Sender={friendStat.Key} Missed={JsonSerializer.Serialize(friendStat.Value.MissedSequences)}");
                  }
               }

               double pctMissed = Math.Round((double) allMissed / allReceived * 100, 2);
               double avgLatency = Math.Round(allLatencySum / allReceived, 2);
               logger.LogInformation($"Clients={clientStats.Count}, Min latency={minLatency}, Max latency={maxLatency}, Avg Latency={avgLatency}, Received={allReceived}, Missed={pctMissed}%");
            });
      }

      private static object Dispatch(IClientVM vm, ConcurrentDictionary<string, int> clientMessageSequences)
      {
         var sequence = clientMessageSequences.AddOrUpdate(vm.ClientId, 1, (_, oldValue) => oldValue + 1);
         return new
         {
            SendMessage = new ChatRoomVM.Payload
            {
               MessageId = $"{vm.ClientId}.{sequence}",
               SenderId = vm.ClientId,
               Time = DateTime.UtcNow,
               Sequence = sequence
            }
         };
      }

      private static void OnServerResponse(IClientVM vm, ServerResponse response, ILogger logger, ConcurrentDictionary<string, ChatStats> clientStats)
      {
         var state = response.As<ChatState>();
         if (state.Messages_add != null)
         {
            DateTime now = DateTime.Now;
            ChatStats stat = clientStats.GetOrAdd(vm.ClientId, new ChatStats { LastLogTime = now });
            lock (stat)
            {
               var friendStat = stat.FriendStats.GetOrAdd(state.Messages_add.SenderId, new FriendStats() { LastTickTime = now });
               lock (friendStat)
               {
                  if (!friendStat.Sequences.Contains(state.Messages_add.Sequence))
                  {
                     var latency = (DateTime.UtcNow - state.Messages_add.Time).TotalMilliseconds;

                     friendStat.Count++;
                     friendStat.LatencySum += latency;
                     friendStat.IntervalSum += (now - friendStat.LastTickTime).TotalMilliseconds;
                     friendStat.LastTickTime = now;
                     friendStat.Sequences.Add(state.Messages_add.Sequence);
                  }
               }

               var delta = (now - stat.LastLogTime).TotalMilliseconds;
               if (delta > LogInterval)
               {
                  long receivedCount = stat.FriendStats.Values.Sum(x => x.Count);
                  long missedCount = stat.FriendStats.Values.Sum(x => x.Missed);
                  double averageLatency = Math.Round(stat.FriendStats.Values.Sum(x => x.LatencySum) / receivedCount, 2);
                  logger.LogTrace($"[{vm.ClientId}] Senders={stat.FriendStats.Count}, Avg Latency={averageLatency}, Received={receivedCount}" + (missedCount > 0 ? $", Missed={missedCount}" : ""));
                  stat.LastLogTime = now;
               }
            }
         }
      }
   }
}