using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityAsyncHttp.Utilities;
using UnityAsyncHttp.Websockets.MessageTypes;

namespace UnityAsyncHttp.Websockets.Client
{
    public class WebsocketClient : IDisposable
    {
        string _host = "localhost";
        int _port = 3000;
        string _path = "ws";

        Dictionary<string, object> _queryParams = new Dictionary<string, object>();

        ClientWebSocket _ws;

        List<ArraySegment<byte>> _msgsToSend = new List<ArraySegment<byte>>();

        AutoResetEvent _sendWaitEvent = new AutoResetEvent(false);
        object _msgLockObject = new object();

        private bool _hasWork;
        public Action OnConnect;
        public Action<string> OnMessage;
        public Action OnDisconnect;

        public void Open(string host, int port, string path, Dictionary<string, object> queryParams = null)
        {
            _host = host;
            _port = port;
            _path = path;
            _queryParams = (queryParams != null) ? queryParams : _queryParams;

            Task.Run(() => Connect());

            OnDisconnect += () =>
            {
                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5f));
                    await Connect();

                });
            };
        }

        public async Task Connect()
        {
            _ws = new ClientWebSocket();
            _ws.Options.AddSubProtocol("Tls");
            _ws.Options.KeepAliveInterval = TimeSpan.Zero;

            var queryString = "";
            foreach (var item in _queryParams)
            {
                queryString += $"{item.Key}={item.Value}&";
            }

            UriBuilder builder = new UriBuilder()
            {
                Host = _host,
                Port = _port,
                Scheme = "ws",
                Path = _path,
                Query = queryString
            };

            var uri = builder.Uri;

            try
            {
                await _ws.ConnectAsync(uri, CancellationToken.None);

                Dispatcher.RunOnMainThread(() => OnConnect?.Invoke());

                await Task.WhenAll(Receive(_ws), Send(_ws));
            }
            catch (Exception ex)
            {
                Dispatcher.LogError(string.Format("Exception: {0}", ex.Message));
            }
            finally
            {
                if (_ws != null)
                    _ws.Dispose();

                _ws = null;

                Dispatcher.RunOnMainThread(() => OnDisconnect?.Invoke());
                Dispatcher.Log("Websocket Task Complete");
            }
        }

        public async Task Send(ClientWebSocket ws)
        {
            while (ws.State == WebSocketState.Open)
            {
                List<ArraySegment<byte>> toSend;

                lock (_msgLockObject)
                {
                    if (!_hasWork) continue;

                    toSend = new List<ArraySegment<byte>>(_msgsToSend.Count);

                    foreach (var item in _msgsToSend)
                    {
                        var src = item.Array;
                        var dst = new byte[src.Length];
                        var length = src.Length;

                        Array.Copy(src, dst, length);
                        toSend.Add(new ArraySegment<byte>(dst));
                    }

                    _msgsToSend.Clear();
                    _hasWork = false;
                }

                try
                {
                    foreach (var bytes in toSend)
                    {
                        await _ws.SendAsync(
                             bytes,
                            WebSocketMessageType.Text,
                            true,
                           CancellationToken.None
                            );
                    }
                }
                catch (Exception e)
                {
                    if (_ws != null)
                        _ws.Dispose();

                    _ws = null;

                    Dispatcher.LogError(string.Format("Exception: {0}", e.Message));
                }

                _sendWaitEvent.WaitOne(1000);
            }

        }

        public async Task Receive(ClientWebSocket ws)
        {
            while (ws.State == WebSocketState.Open)
            {
                ArraySegment<byte> bytesReceived = new ArraySegment<byte>(new byte[1024]);

                try
                {
                    WebSocketReceiveResult result = await _ws.ReceiveAsync(
                        bytesReceived,
                       CancellationToken.None);

                    var msg = Encoding.UTF8.GetString(bytesReceived.Array);

                    Dispatcher.RunOnMainThread(() => OnMessage?.Invoke(msg));
                }
                catch (Exception e)
                {
                    if (_ws != null)
                        _ws.Dispose();

                    _ws = null;

                    Dispatcher.LogError(string.Format("Exception: {0}", e.Message));
                }

            }
        }

        async void DisposeAsync()
        {
            CancellationTokenSource tokensrc = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "OK", tokensrc.Token);
            }
            catch (Exception e)
            {
                Dispatcher.LogError(string.Format("Exception: {0}", e));
            }
            finally

            {
                if (_ws != null)
                    _ws.Dispose();

                _ws = null;
            }
        }

        public void QueueMessage(IWebsocketMessage msg)
        {
            QueueMessage(JsonConvert.SerializeObject(msg));
        }

        public void QueueMessage(string msg)
        {
            lock (_msgLockObject)
            {
                _msgsToSend.Add(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)));
                _hasWork = true;
                _sendWaitEvent.Set();
            }
        }

        public void Dispose()
        {
            Dispatcher.RunAsync(DisposeAsync);
        }

        void IDisposable.Dispose()
        {
            Dispose();
        }
    }
}