using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace WebServer
{
    public class Configuration
    {
        public IPAddress LocalAddress { get; set; } = IPAddress.Any;

        public string DomainName { get; set; }

        public string Webroot
        {
            get
            {
                if (string.IsNullOrEmpty(_Webroot))
                    _Webroot = Directory.GetCurrentDirectory();
                return _Webroot;
            }
            set { _Webroot = value; }
        }
        string _Webroot;    

        public int SocketTimeout { get; set; } = 20000;

        public int Port { get; set; } = 80;
        public int TlsPort { get; set; } = 443;
        public bool IsTlsEnabled { get; set; }

        public bool TlsRedirect { get; set; }
        public X509Certificate2 Certificate { get; set; }
        public SslProtocols TlsProtocols { get; set; } = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;        
        public XFrameOptions XFrameOptions { get; set; }  

        public string[] AppCaches { get; set; }
        public string[] NoCacheFiles
        {
            get
            {
                if (_NoCacheFiles == null)
                    _NoCacheFiles = InitializeAppCaches();
                return _NoCacheFiles;
            }
        }
        string[] _NoCacheFiles;
        
        string[] InitializeAppCaches()
        {
            var ncf = Enumerable.Empty<string>();
            if (AppCaches == null)
                return new string[0];
            foreach (var appcache in AppCaches)
            {
                var file = Path.Combine(Webroot, appcache);
                var root = Path.GetDirectoryName(file);
                using (StreamReader sr = new StreamReader(file))
                {
                    string content = sr.ReadToEnd();
                    int start = content.IndexOf("CACHE:\r\n") + 8;
                    int stop = content.IndexOf("\r\n\r\n", start);
                    string caches = content.Substring(start, stop - start);
                    var cacheFiles = caches.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    ncf = ncf.Concat(cacheFiles.Select(n => Path.Combine(root, n.Replace('/', '\\')).ToLower()));
                }
            }
            return ncf.ToArray();
        }
    }
}