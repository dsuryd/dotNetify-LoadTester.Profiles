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

using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DotNetify.LoadTester.Profiles
{
   public class ChatRoomMiddleware : IMiddleware
   {
      public Task Invoke(DotNetifyHubContext context, NextDelegate next)
      {
         if (context.CallType == "Request_VM")
         {
            var data = NormalizeType(context.Data);
            if (data is JObject)
            {
               var vmArg = (data as JObject)["$vmArg"];
               if (vmArg is JObject)
               {
                  var chatRoom = (vmArg as JObject)[nameof(ChatRoomVM.ChatRoom)]?.ToString();
                  context.CallerContext.Items[nameof(ChatRoomVM.ChatRoom)] = chatRoom;
               }
            }
         }

         return next(context);
      }

      private static object NormalizeType(object data)
      {
         if (data == null)
            return null;
         else if (data is JsonElement jElement)
         {
            // System.Text.Json protocol.
            var value = JToken.Parse(jElement.GetRawText());
            return value is JValue ? (value as JValue).Value : value;
         }
         else if (data is JObject)
            // Newtonsoft.Json protocol.
            return data as JObject;
         else if (!(data.GetType().IsPrimitive || data is string))
            // MessagePack protocol.
            return JObject.FromObject(data);

         return data;
      }
   }
}