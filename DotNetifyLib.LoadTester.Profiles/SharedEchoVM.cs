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
using System.Threading.Tasks;

namespace DotNetify.LoadTester.Profiles
{
   public class SharedEchoVM : MulticastVM
   {
      private readonly IConnectionContext _context;
      private readonly ConcurrentDictionary<string, PingInfo> _pingInfo = new ConcurrentDictionary<string, PingInfo>();

      private class PingInfo
      {
         public int Count { get; set; } = 1;
         public double DeltaSum { get; set; }
      }

      public EchoVM.Payload Ping { get; set; }

      public int PingInterval { get; set; } = 1000;

      public SharedEchoVM(IConnectionContext context)
      {
         _context = context;
      }

      public void Pong(EchoVM.Payload payload)
      {
         var info = _pingInfo.GetOrAdd(_context.ConnectionId, new PingInfo());

         double latency = 0;
         if (!string.IsNullOrEmpty(payload.ConnectionId))
         {
            double delta = (DateTime.UtcNow - payload.Time).TotalMilliseconds;
            info.DeltaSum += delta;
            latency = Math.Round(delta, 2);
         }

         _ = SendPingAsync(info, latency);
      }

      private async Task SendPingAsync(PingInfo info, double latency = 0)
      {
         await Task.Delay(PingInterval);

         var avgLatency = info.Count > 0 ? Math.Round(info.DeltaSum / info.Count, 2) : 0;
         Send(new string[] { _context.ConnectionId }, nameof(Ping), new EchoVM.Payload
         {
            ConnectionId = _context.ConnectionId,
            Time = DateTime.UtcNow,
            Sequence = info.Count++,
            PrevLatency = latency,
            AvgLatency = avgLatency
         });
      }
   }
}