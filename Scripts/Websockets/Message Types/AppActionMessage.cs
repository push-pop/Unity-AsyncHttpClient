using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace UnityAsyncHttp.Websockets.MessageTypes
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AppAction
    {
        Start,
        Stop
    }

    [Serializable]
    public class AppActionMessage : IWebsocketMessage
    {
        public AppAction action { get; set; }

        public int id { get; set; }

    }
}
