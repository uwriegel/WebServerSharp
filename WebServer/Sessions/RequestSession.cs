using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebServer
{
    class RequestSession
    {
        public IRequestHeaders Headers { get; private set; }

        public string UrlRoot
        {
            get
            {
                if (_UrlRoot == null)
                    _UrlRoot = $"http{(string)(Server.Configuration.IsTlsEnabled ? "s" : null)}://{Headers.Host}";
                return _UrlRoot;
            }
        }
        string _UrlRoot;        

        public SocketSession SocketSession { get; private set; }

        public Server Server { get; private set; }
        
        public string HttpResponseString
        {
            get
            {
                if (_HttpResponseString == null)
                    _HttpResponseString = (Headers as RequestHeaders).Http10 ? "HTTP/1.0" : "HTTP/1.1";
                return _HttpResponseString;
            }
        }
        string _HttpResponseString;    

        public RequestSession(Server server, SocketSession socketSession, Stream networkStream)
        {
            this.responseHeaders = new ServerResponseHeaders(server);
            this.SocketSession = socketSession;
            this.Server = server;
            this.networkStream = networkStream;
        }        

        public async Task<bool> StartAsync()
        {
            try
            {
                int result = await networkStream.ReadAsync(readBuffer, 0, readBuffer.Length);
                if (result == 0)
                    return false;
                return await ReceiveAsync(result);
            }
            catch (Exception e) when (e is IOException || e is CloseException || e is SocketException)
            {
                Close(true);
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error has occurred while reading socket: {e}");
                Close(true);
                return false;
            }
        }

        public void Close(bool fullClose = false)
        {
            if (fullClose)
            {
                networkStream.Close();
                isClosed = true;
            }
            else
                SocketSession.Client.Client.Shutdown(SocketShutdown.Send);
        }

        public async Task SendErrorAsync(string htmlHead, string htmlBody, int errorCode, string errorText)
        {
            string response = string.Format("<html><head>{0}</head><body>{1}</body></html>", htmlHead, htmlBody);
            byte[] responseBytes = Encoding.UTF8.GetBytes(response);

            responseHeaders.Status = errorCode;
            responseHeaders.StatusDescription = errorText;
            responseHeaders.Add("Content-Length", $"{responseBytes.Length}");
            responseHeaders.Add("Content-Type", "text/html; charset = UTF-8");
            var headerBuffer = responseHeaders.Access(HttpResponseString);
            await WriteAsync(headerBuffer, 0, headerBuffer.Length);
            await WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        public async Task Send503Async()
        {
            string headerString = $"{HttpResponseString} 503 Service Unavailable\r\nContent-Length: 0\r\n\r\n";
            byte[] vorspannBuffer = ASCIIEncoding.ASCII.GetBytes(headerString);
            await networkStream.WriteAsync(vorspannBuffer, 0, vorspannBuffer.Length);

            //responseHeaders.SetInfo(305, 0);
        }

        public async Task SendNotFoundAsync()
        {
            await SendErrorAsync(
@"<title>CAESAR</title>
<Style> 
html {
    font-family: sans-serif;
}
h1 {
    font-weight: 100;
}
</Style>",
                "<h1>Datei nicht gefunden</h1><p>Die angegebene Resource konnte auf dem Server nicht gefunden werden.</p>",
                404, "Not Found");
        }

        public async Task SendFileAsync(string file)
        {
            if (file.EndsWith(".mp4", StringComparison.InvariantCultureIgnoreCase)
                || file.EndsWith(".mkv", StringComparison.InvariantCultureIgnoreCase)
                || file.EndsWith(".mp3", StringComparison.InvariantCultureIgnoreCase)
                || file.EndsWith(".wav", StringComparison.InvariantCultureIgnoreCase))
                await SendRangeAsync(file);
            else
                await InternalSendFileAsync(file);
        }

        public async Task SendStreamAsync(Stream stream, string contentType, string lastModified, bool noCache)
        {
            if (!noCache)
            {
                string isModifiedSince = Headers["if-modified-since"];
                if (isModifiedSince == NotModified)
                {
                    await Send304Async();
                    return;
                }
            }

            if (Headers.ContentEncoding != ContentEncoding.None &&
                (contentType.StartsWith("application/javascript", StringComparison.CurrentCultureIgnoreCase)
                    || contentType.StartsWith("text/", StringComparison.CurrentCultureIgnoreCase)))
            {
                var ms = new MemoryStream();

                Stream compressedStream;
                switch (Headers.ContentEncoding)
                {
                    case ContentEncoding.Deflate:
                        responseHeaders.Add("Content-Encoding", "deflate");
                        compressedStream = new DeflateStream(ms, System.IO.Compression.CompressionMode.Compress, true);
                        break;
                    case ContentEncoding.GZip:
                        responseHeaders.Add("Content-Encoding", "gzip");
                        compressedStream = new GZipStream(ms, System.IO.Compression.CompressionMode.Compress, true);
                        break;
                    default:
                        compressedStream = null;
                        break;
                }
                using (compressedStream)
                {
                    await stream.CopyToAsync(compressedStream);
                    compressedStream.Close();
                    stream = ms;
                }
                ms.Position = 0;
            }

            responseHeaders.Initialize(contentType, (int)stream.Length, lastModified, noCache);

            if (contentType.StartsWith("application/javascript", StringComparison.CurrentCultureIgnoreCase)
                || contentType.StartsWith("text/css", StringComparison.CurrentCultureIgnoreCase)
                || contentType.StartsWith("text/html", StringComparison.CurrentCultureIgnoreCase))
            {
                responseHeaders.Add("Expires", DateTime.Now.ToUniversalTime().ToString("r"));
                //responseHeaders.Add("Cache-Control", "must-revalidate");
                //responseHeaders.Add("Expires", "-1");
            }

            var headerBuffer = responseHeaders.Access(HttpResponseString);
            await WriteAsync(headerBuffer, 0, headerBuffer.Length);

            if (Headers.Method == Method.HEAD)
                return;

            byte[] bytes = new byte[8192];
            while (true)
            {
                int read = await stream.ReadAsync(bytes, 0, bytes.Length);
                if (read == 0)
                    return;
                await WriteAsync(bytes, 0, read);
            }
        }

        public async Task WriteAsync(byte[] buffer) => await networkStream.WriteAsync(buffer, 0, buffer.Length);

        public async Task WriteAsync(byte[] buffer, int offset, int length) => await networkStream.WriteAsync(buffer, offset, length);

        async Task InternalSendFileAsync(string file)
        {
            FileInfo fi = new FileInfo(file);
            bool noCache = Server.Configuration.NoCacheFiles.Contains(file.ToLower());

            if (!noCache)
            {
                string isModifiedSince = Headers["if-modified-since"];
                if (isModifiedSince != null)
                {
                    int pos = isModifiedSince.IndexOf(';');
                    if (pos != -1)
                        isModifiedSince = isModifiedSince.Substring(0, pos);
                    DateTime ifModifiedSince = Convert.ToDateTime(isModifiedSince);
                    DateTime fileTime = fi.LastWriteTime.AddTicks(-(fi.LastWriteTime.Ticks % TimeSpan.FromSeconds(1).Ticks));
                    TimeSpan diff = fileTime - ifModifiedSince;
                    if (diff <= TimeSpan.FromMilliseconds(0))
                    {
                        await Send304Async();
                        return;
                    }
                }
            }

            string contentType = null;
            switch (fi.Extension)
            {
                case ".html":
                case ".htm":
                    contentType = "text/html; charset=UTF-8";
                    break;
                case ".css":
                    contentType = "text/css; charset=UTF-8";
                    break;
                case ".js":
                    contentType = "application/javascript; charset=UTF-8";
                    break;
                case ".appcache":
                    contentType = "text/cache-manifest";
                    break;
                default:
                    contentType = MimeTypes.GetMimeType(fi.Extension);
                    break;
            }

            DateTime dateTime = fi.LastWriteTime;
            string lastModified = dateTime.ToUniversalTime().ToString("r");

            try
            {
                using (Stream stream = File.OpenRead(file))
                    await SendStreamAsync(stream, contentType, lastModified, noCache);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not send file: {e}");
            }
        }

        async Task<bool> ReceiveAsync(int bufferPosition)
        {
            try
            {
                Headers = new RequestHeaders();
                var result = await (Headers as Headers).InitializeAsync(networkStream, readBuffer, bufferPosition);
                bufferEndPosition = result.BufferEndPosition;
                readFromBuffer = bufferEndPosition > 0;
                bufferReadCount = result.BufferReadCount;

                //string refererPath = null;
                //if (headers.ContainsKey("Referer"))
                //{
                //    string path = headers["Referer"];
                //    path = path.Substring(path.IndexOf("//") + 2);
                //    refererPath = path.Substring(path.IndexOf('/') + 1);
                //}

                //string query = null;
                // var extResult = CheckExtension(Headers.Url);
                // if (extResult != null)
                // {
                //     string path = extResult.Url;
                //     query = Headers.Url.Substring(path.Length).Trim('/');
                //     return extResult.Extension.Request(this, Headers.Method, path, query) && !isClosed;
                // }

                bool isDirectory = Headers.Url.EndsWith("/");
                string file = CheckFile(Headers.Url);
                if (!string.IsNullOrEmpty(file))
                    await SendFileAsync(file);
                else
                {
                    // if (Headers.Url == "/$$GC")
                    // {
                    //     GC.Collect();
                    //     SendOK("Speicher wurde bereinigt");
                    // }
                    // else if (Headers.Url == "/$$Resources")
                    //     Resources.Current.Send(this);
                    if (!isDirectory)
                        await RedirectDirectoryAsync(Headers.Url + '/');
                    else
                    {
                        if (Headers.Url.Length > 2)
                        {
                            string url = Headers.Url.Substring(0, Headers.Url.Length - 1);
                            string relativePath = url.Replace('/', '\\');
                            relativePath = relativePath.Substring(1);
                            string path = Path.Combine(Server.Configuration.Webroot, relativePath);
                            FileInfo fi = new FileInfo(path);
                            if (fi.Exists)
                            {
                                await RedirectDirectoryAsync(url);
                                return true;
                            }
                        }
                        else if (Headers.Url == "/")
                        {
                            string relativePath = "root\\index.html";
                            string path = Path.Combine(Server.Configuration.Webroot, relativePath);
                            FileInfo fi = new FileInfo(path);
                            if (fi.Exists)
                            {
                                await RedirectDirectoryAsync("/root/");
                                return true;
                            }
                        }
                        await SendNotFoundAsync();
                    }
                }
                return true;
            }
            catch (ServiceUnavailableException)
            {
                await Send503Async();
                return false;
            }
            catch (SocketException se)
            {
                if (se.SocketErrorCode == SocketError.TimedOut)
                {
                    Console.WriteLine($"Socket session closed, Timeout has occurred");
                    Close(true);
                    return false;
                }
                return true;
            }
            catch (CloseException)
            {
                Close(true);
                return false;
            }
            catch (ObjectDisposedException)
            {
                Close(true);
                return false;
            }
            catch (IOException)
            {
                Close(true);
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Socket session closed, an error has occurred while receiving: {e}");
                Close(true);
                return false;
            }
        }

        async Task RedirectDirectoryAsync(string redirectedUrl, bool checkIfExists = true)
        {
            if (checkIfExists && string.IsNullOrEmpty(CheckFile(redirectedUrl)))
            {
                await SendNotFoundAsync();
                return;
            }

            if (!string.IsNullOrEmpty(Headers.Host))
            {
                string response = "<html><head>Moved permanently</head><body><h1>Moved permanently</h1>The specified resource moved permanently.</body</html>";
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                string redirectHeaders = $"{HttpResponseString} 301 Moved Permanently\r\nLocation: {UrlRoot}{redirectedUrl}\r\nContent-Length: {responseBytes.Length}\r\n\r\n";

                byte[] vorspannBuffer = ASCIIEncoding.ASCII.GetBytes(redirectHeaders);
                await networkStream.WriteAsync(vorspannBuffer, 0, vorspannBuffer.Length);
                await networkStream.WriteAsync(responseBytes, 0, responseBytes.Length);
            }
        }

        string CheckFile(string url)
        {
            int raute = url.IndexOf('#');
            if (raute != -1)
                url = url.Substring(0, raute);
            int qm = url.IndexOf('?');
            if (qm != -1)
                url = url.Substring(0, qm);

            bool isDirectory = url.EndsWith("/");

            string path;
            url = Uri.UnescapeDataString(url);

            string relativePath = Path.DirectorySeparatorChar != '/'  ?  url.Replace('/', Path.DirectorySeparatorChar) :  url;
            relativePath = relativePath.Substring(1);
            path = Path.Combine(Server.Configuration.Webroot, relativePath);

            if (File.Exists(path))
                return path;
            else if (!isDirectory)
                return null;

            string subpath = path;
            path = Path.Combine(subpath, "index.html");
            if (File.Exists(path))
                return path;
            return null;
        }

        async Task SendRangeAsync(string file)
        {
            // TODO: umstellen auf ResponseHeader
            // TODO: mp4, mp3, ...
            FileInfo fi = new FileInfo(file);
            using (Stream stream = File.OpenRead(file))
                await SendRangeAsync(stream, fi.Length, file, null);
        }

        async Task SendRangeAsync(Stream stream, long fileLength, string file, string contentType)
        {
            string rangeString = Headers["range"];
            if (rangeString == null)
            {
                if (!string.IsNullOrEmpty(file))
                    await InternalSendFileAsync(file);
                else
                    await SendStreamAsync(stream, contentType, DateTime.Now.ToUniversalTime().ToString("r"), true);
                return;
            }

            rangeString = rangeString.Substring(rangeString.IndexOf("bytes=") + 6);
            int minus = rangeString.IndexOf('-');
            long start = 0;
            long end = fileLength - 1;
            if (minus == 0)
                end = long.Parse(rangeString.Substring(1));
            else if (minus == rangeString.Length - 1)
                start = long.Parse(rangeString.Substring(0, minus));
            else
            {
                start = long.Parse(rangeString.Substring(0, minus));
                end = long.Parse(rangeString.Substring(minus + 1));
            }

            var contentLength = end - start + 1;
            if (string.IsNullOrEmpty(contentType))
                contentType = "video/mp4";
            string headerString =
$@"{HttpResponseString} 206 Partial Content
ETag: ""0815""
Accept-Ranges: bytes
Content-Length: {contentLength}
Content-Range: bytes {start}-{end}/{fileLength}
Keep-Alive: timeout=5, max=99
Connection: Keep-Alive
Content-Type: {contentType}

";
            byte[] vorspannBuffer = ASCIIEncoding.ASCII.GetBytes(headerString);
            await networkStream.WriteAsync(vorspannBuffer, 0, vorspannBuffer.Length);
            byte[] bytes = new byte[40000];
            long length = end - start;
            stream.Seek(start, SeekOrigin.Begin);
            long completeRead = 0;
            while (true)
            {
                int read = await stream.ReadAsync(bytes, 0, Math.Min(bytes.Length, (int)(contentLength - completeRead)));
                if (read == 0)
                    return;
                completeRead += read;
                await networkStream.WriteAsync(bytes, 0, read);
                if (completeRead == contentLength)
                    return;
            }
        }

        async Task Send304Async()
        {
            string headerString = $"{HttpResponseString} 304 Not Modified\r\n\r\n";
            byte[] vorspannBuffer = ASCIIEncoding.ASCII.GetBytes(headerString);
            responseHeaders.SetInfo(304, 0);
            await WriteAsync(vorspannBuffer, 0, vorspannBuffer.Length);
        }
        
        const string webSocketKeyConcat = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        Stream networkStream;
        byte[] readBuffer = new byte[20000];

        ServerResponseHeaders responseHeaders;

        const string NotModified = "Fri, 01 Jun 2012 08:28:30 GMT";
        /// <summary>
        /// Die Position im Buffer, an der die Headerdaten aufhören und die eigentlichen Daten anfangen, unmittelbar nach Einlesen der Header aus dem Netzwerkstrom
        /// </summary>
        int bufferEndPosition;
        /// <summary>
        /// Wenn im Buffer nach dem Header bereits payload eingelesen wurde
        /// </summary>
        bool readFromBuffer;
        /// <summary>
        /// Anzahl bereits einglesener Bytes im Buffer, unmittelbar nach Einlesen der Header aus dem Netzwerkstrom
        /// </summary>
        int bufferReadCount;
        bool isClosed;        
    }
}