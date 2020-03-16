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

        public void Get(string path, Action<string> callback = null)
        {
            MakeRequest(path, HttpMethod.Get, null, callback);
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


        void MakeRequest(string path, HttpMethod method, HttpContent content, Action<string> callback)
        {
            UriBuilder uriBuilder = new UriBuilder()
            {
                Host = _host,
                Port = _port,
                Path = path
            };

            HttpRequestMessage req = new HttpRequestMessage(method, uriBuilder.Uri);
            req.Content = content;

            MakeRequest(req, callback);
        }

        void MakeRequest(HttpRequestMessage req, Action<string> callback = null)
        {
            Task.Run(() => MakeRequestAsync(req, callback));
        }

        async void MakeRequestAsync(HttpRequestMessage req, Action<string> callback)
        {
            var strRes = "";

            var res = await _client.SendAsync(req, CancellationToken.None);
            strRes = await res.Content.ReadAsStringAsync();

            Dispatcher.RunOnMainThread(() => callback?.Invoke(strRes));
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