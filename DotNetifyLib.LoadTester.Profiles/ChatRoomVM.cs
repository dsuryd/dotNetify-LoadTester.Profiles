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

namespace DotNetify.LoadTester.Profiles
{
   public class ChatRoomVM : MulticastVM
   {
      private readonly List<Payload> _messages = new List<Payload>();
      private readonly IHubCallerContextAccessor _accessor;

      public class Payload
      {
         public string MessageId { get; set; }
         public string SenderId { get; set; }
         public DateTimeOffset Time { get; set; }
         public int Sequence { get; set; }
      }

      [ItemKey(nameof(Payload.MessageId))]
      public Payload[] Messages
      {
         get
         {
            lock (_messages)
               return _messages.ToArray();
         }
      }

      public override string GroupName
      {
         get
         {
            var context = _accessor.CallerContext;
            if (context.Items.ContainsKey(nameof(ChatRoom)))
               return context.Items[nameof(ChatRoom)]?.ToString();
            return null;
         }
      }

      public string ChatRoom { get; set; }
      public string ClientId { get; set; }

      public ChatRoomVM(IHubCallerContextAccessor accessor)
      {
         _accessor = accessor;
      }

      public void SendMessage(Payload payload)
      {
         lock (_messages)
         {
            _messages.Add(payload);
            this.AddList(nameof(Messages), payload);
            PushUpdates();
         }
      }
   }
}