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
using System.Threading;
using System.Threading.Tasks;

namespace DotNetify.LoadTester.Profiles
{
   public class BroadcastVM : MulticastVM
   {
      private int _count;
      private Timer _timer;
      private readonly object _timerLock = new object();

      public class Payload
      {
         public DateTime Time { get; set; }
         public int Sequence { get; set; }
      }

      public int TickInterval { get; set; } = 1000;

      public Payload Tick { get => Get<Payload>(); set => Set(value); }

      public override string GroupName => nameof(BroadcastVM);

      public override Task OnCreatedAsync()
      {
         _timer = new Timer(state =>
         {
            if (Monitor.TryEnter(_timerLock))
            {
               Tick = new Payload { Time = DateTime.UtcNow, Sequence = ++_count };
               PushUpdates();

               Monitor.Exit(_timerLock);
            }
         }, null, 1000, TickInterval);

         return Task.CompletedTask;
      }

      protected override void Dispose(bool disposing)
      {
         if (disposing)
            _timer.Dispose();
      }
   }
}