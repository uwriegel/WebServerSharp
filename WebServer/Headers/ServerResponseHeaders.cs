using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebServer
{
    class ServerResponseHeaders
    {
        public ServerResponseHeaders(Server server)
        {
            this.server = server;
        }

        public int Status { get; set; } = 200;
        public string StatusDescription { get; set; } = "OK";
        public int ContentLength
        {
            get
            {
                if (_ContentLength == null)
                {
                    if (headers.ContainsKey("Content-Length"))
                    {
                        int val = -1;
                        int.TryParse(headers["Content-Length"], out val);
                        _ContentLength = val;
                    }
                }
                return _ContentLength ?? 0;
            }
            private set
            {
                _ContentLength = value;
            }
        }
        int? _ContentLength;

        public void Initialize(string contentType, int contentLength, string lastModified, bool noCache)
        {
            ContentLength = contentLength;
            if (contentType == "video/mp4")
            {
                // TODO: Wahrscheinlich unnötig, da SendMp4
                Add("ETag", "\"0815\"");
                Add("Accept-Ranges", "bytes");
                Add("Content-Type", contentType);
                Add("Keep-Alive", "timeout = 5, max = 99");
                Add("Connection", "Keep-Alive");
            }
            else
            {
                if (contentType != null)
                    Add("Content-Type", contentType);
                if (!string.IsNullOrEmpty(lastModified))
                    Add("Last-Modified", lastModified);
            }
            Add("Content-Length", $"{contentLength}");

            if (noCache)
            {
                Add("Cache-Control", "no-cache,no-store");
                Add("Expires", (DateTime.Now.Subtract(new TimeSpan(1, 0, 0))).ToUniversalTime().ToString("r"));
            }
        }

        public void InitializeJson(int contentLength)
        {
            ContentLength = contentLength;
            Add("Content-Length", $"{contentLength}");
            Add("Content-Type", "application/json; charset=UTF-8");
            Add("Cache-Control", "no-cache,no-store");
        }

        public void Add(string key, string value)
        {
            if (key == "Content-Length")
            {
                int val = -1;
                int.TryParse(value, out val);
                ContentLength = val;
            }
            headers[key] = value;
        }

        public void SetInfo(int status, int contentLength)
        {
            ContentLength = contentLength;
            Status = status;
        }

        public byte[] Access(string httpResponseString, byte[] payload = null)
        {
            if (!headers.ContainsKey("Content-Length"))
                headers["Connection"] = "close";
            headers["Date"] = DateTime.Now.ToUniversalTime().ToString("R");
            headers["Server"] = "UR Web Server";

            if (server.Configuration.XFrameOptions != XFrameOptions.NotSet)
                headers["X-Frame-Options"] = server.Configuration.XFrameOptions.ToString();
            var headerLines = headers.Select(n => $"{n.Key}: {n.Value}");
            var headerString = $"{httpResponseString} {Status} {StatusDescription}\r\n" + string.Join("\r\n", headerLines) + "\r\n\r\n";

            if (payload == null)
                return ASCIIEncoding.ASCII.GetBytes(headerString);
            else
            {
                var result = new byte[ASCIIEncoding.ASCII.GetByteCount(headerString) + payload.Length];
                var headerBytes = ASCIIEncoding.ASCII.GetBytes(headerString, 0, headerString.Length, result, 0);
                Array.Copy(payload, 0, result, headerBytes, payload.Length);
                return result;
            }
        }

        Server server;
        Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
