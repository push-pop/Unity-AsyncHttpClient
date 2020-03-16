using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityAsyncHttp.Content
{
    public class ProgressableStreamContent : HttpContent
    {
        /// <summary>
        /// Lets keep buffer of 20kb
        /// </summary>
        private const int defaultBufferSize = 5 * 4096;

        private HttpContent content;
        private int bufferSize;

        private Action<float> progress;

        public ProgressableStreamContent(HttpContent content, Action<float> progress)
            : this(content, defaultBufferSize, progress) { }


        public ProgressableStreamContent(HttpContent content, int bufferSize, Action<float> progress)
        {
            if (content == null)
            {
                throw new ArgumentNullException("content");
            }
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException("bufferSize");
            }

            this.content = content;
            this.bufferSize = bufferSize;
            this.progress = progress;

            foreach (var h in content.Headers)
            {
                this.Headers.Add(h.Key, h.Value);
            }
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {

            return Task.Run(async () =>
            {
                var buffer = new Byte[this.bufferSize];
                long size;
                TryComputeLength(out size);
                var uploaded = 0;


                using (var sinput = await content.ReadAsStreamAsync())
                {
                    float lastPct = 0f;
                    while (true)
                    {
                        var length = sinput.Read(buffer, 0, buffer.Length);
                        if (length <= 0) break;

                        uploaded += length;
                        var pctUploaded = (float)uploaded / (float)size;
                        if (pctUploaded - lastPct > .1f)
                        {
                            progress?.Invoke(pctUploaded);
                            lastPct = pctUploaded;
                        }


                        stream.Write(buffer, 0, length);
                        stream.Flush();
                    }
                }
                stream.Flush();
            });
        }

        protected override bool TryComputeLength(out long length)
        {
            length = content.Headers.ContentLength.GetValueOrDefault();
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                content.Dispose();
            }
            base.Dispose(disposing);
        }

    }
}
