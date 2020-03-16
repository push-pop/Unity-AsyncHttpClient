using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityAsyncHttp.Websockets.MessageTypes
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AppState
    {
        Startup,
        Idle,
        Session,
        MediaUpload,
        Shutdown
    }

    [Serializable]
    public class StatusUpdateMessage : IWebsocketMessage
    {
        public AppState appState { get; set; }
        public int id { get; set; }
    }
}
