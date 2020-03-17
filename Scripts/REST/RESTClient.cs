using Networking.MimeTypes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityAsyncHttp.Content;
using UnityAsyncHttp.Utilities;

namespace UnityAsyncHttp.Rest.Client
{


    public class RESTClient
    {
        public enum ResponseType
        {
            Text,
            File
        }
        public RESTClient(string host, int port)
        {
            _host = host;
            _port = port;
        }

        string _host = @"localhost";

        int _port = 3000;

        public Action<string> OnError;
        public Action<float> OnUploadProgress;

        private static readonly HttpClient _client = new HttpClient();
        CancellationTokenSource tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10f));

        public void Get(string path, ResponseType respType = ResponseType.Text, Action<string> callback = null)
        {
            MakeRequest(path, HttpMethod.Get, null, callback);
        }

        public void GetFile(string path, string fileName, Action<string> callback = null)
        {
            MakeRequest(path, HttpMethod.Get, null, callback, ResponseType.File, fileName);
        }

        public void Put(string path, Dictionary<string, string> content = null, Action<string> callback = null)
        {
            HttpContent httpContent = new StringContent(content.ToJSONString(), Encoding.UTF8, "application/json");

            MakeRequest(path, HttpMethod.Put, httpContent, callback);
        }

        public void Post(string path, Dictionary<string, string> content = null, Action<string> callback = null)
        {
            HttpContent httpContent = new StringContent(content.ToJSONString(), Encoding.UTF8, "application/json");

            MakeRequest(path, HttpMethod.Post, httpContent, callback);
        }

        public void UploadFiles(List<string> filePaths, string reqPath, Dictionary<string, string> formData = null, Action<string> callback = null)
        {
            var req = new HttpRequestMessage();
            var multi = new MultipartFormDataContent();

            if (formData != null)
            {
                foreach (var item in formData)
                {
                    multi.Add(new StringContent(item.Value), item.Key);
                }
            }

            List<FileStream> streams = new List<FileStream>();
            int i = 0;

            foreach (var path in filePaths)
            {
                try
                {
                    var uri = new Uri(path, UriKind.RelativeOrAbsolute);
                    var absPath = Path.GetFullPath(uri.ToString());

                    var fileInfo = new FileInfo(absPath);
                    var imgBytes = File.ReadAllBytes(absPath);

                    var stream = new FileStream(absPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                    var streamContent = new StreamContent(stream);

                    var progressContent = new ProgressableStreamContent(
                        streamContent,
                        4096,
                        (pct) =>
                        {
                            Dispatcher.RunOnMainThread(() => OnUploadProgress?.Invoke(pct));
                            Dispatcher.Log(pct.ToString());
                        });
                    progressContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                    {
                        FileName = fileInfo.Name,
                        Name = "file_" + i
                    };
                    progressContent.Headers.ContentType =
                        new MediaTypeHeaderValue(MimeTypeMap.GetMimeType(fileInfo.Extension));


                    multi.Add(progressContent);
                    i++;
                }
                catch (Exception e)
                {
                    OnError?.Invoke("Exc: " + e.Message);
                }

            }

            MakeRequest(reqPath, HttpMethod.Post, multi, (str) =>
            {
                foreach (var stream in streams)
                {
                    stream.Close();
                }
                callback?.Invoke(str);
            });
        }

        public void UploadFile(string filePath, string reqPath, Dictionary<string, string> formData = null, Action<string> callback = null)
        {
            UploadFiles(new List<string>() { filePath }, reqPath, formData, callback);
        }

        void MakeRequest(string path, HttpMethod method, HttpContent content = null, Action<string> callback = null, ResponseType respType = ResponseType.Text, string fileSavePath = null)
        {
            Task.Run(() => MakeRequestAsync(path, method, content, respType, callback, fileSavePath));
        }

        async void MakeRequestAsync(string path, HttpMethod method, HttpContent content, ResponseType respType, Action<string> callback, string fileSavePath)
        {
            UriBuilder uriBuilder = new UriBuilder()
            {
                Host = _host,
                Port = _port,
                Path = path
            };

            var msg = new HttpRequestMessage(method, uriBuilder.Uri);

            if (content != null)
                msg.Content = content;

            var res = await _client.SendAsync(msg, CancellationToken.None);

            if (res.IsSuccessStatusCode)
            {
                if (respType == ResponseType.Text)
                {
                    var str = await res.Content.ReadAsStringAsync();
                    Dispatcher.RunOnMainThread(() => callback?.Invoke(str));
                }

                else if (respType == ResponseType.File)
                {
                    var fullPath = Path.Combine(UnityEngine.Application.persistentDataPath, fileSavePath);
                    using (var fs = new FileStream(fullPath, FileMode.CreateNew))
                    {
                        await res.Content.CopyToAsync(fs);
                        Dispatcher.RunOnMainThread(() => callback?.Invoke(fullPath));
                    }
                }
            }
        }
    }

    public static class DictionaryExt
    {
        public static string ToJSONString(this Dictionary<string, string> dict)
        {
            return JsonConvert.SerializeObject(dict);
        }
    }
}