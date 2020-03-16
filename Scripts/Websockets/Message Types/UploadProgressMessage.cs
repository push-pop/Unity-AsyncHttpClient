using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityAsyncHttp.Websockets.MessageTypes
{
    public class UploadProgressMessage : IWebsocketMessage
    {
        public int id { get; set; }
        public float progress { get; set; }
    }
}
