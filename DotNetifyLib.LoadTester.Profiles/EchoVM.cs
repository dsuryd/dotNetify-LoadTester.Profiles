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
using System.Threading.Tasks;

namespace DotNetify.LoadTester.Profiles
{
   public class EchoVM : BaseVM
   {
      private int _count;
      private double _deltaSum = 0;
      private readonly IConnectionContext _context;

      public class Payload
      {
         public string ConnectionId { get; set; }
         public DateTime Time { get; set; }
         public int Sequence { get; set; }
         public double PrevLatency { get; set; }
         public double AvgLatency { get; set; }
      }

      public Payload Ping { get => Get<Payload>(); set => Set(value); }

      public int PingInterval { get; set; } = 1000;

      public EchoVM(IConnectionContext context)
      {
         _context = context;
         _ = SendPing();
      }

      public void Pong(Payload payload)
      {
         double delta = (DateTime.UtcNow - payload.Time).TotalMilliseconds;
         _deltaSum += delta;

         _ = SendPing(Math.Round(delta, 2));
      }

      private async Task SendPing(double latency = 0)
      {
         await Task.Delay(PingInterval);

         double avgLatency = _count > 0 ? Math.Round(_deltaSum / _count, 2) : 0;
         Ping = new Payload
         {
            ConnectionId = _context.ConnectionId,
            Time = DateTime.UtcNow,
            Sequence = ++_count,
            PrevLatency = latency,
            AvgLatency = avgLatency
         };
         PushUpdates();
      }
   }
}