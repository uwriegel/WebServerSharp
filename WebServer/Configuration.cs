using System.IO;
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

        public int SocketTimeout { get; set; } = 80;

        public int Port { get; set; } = 80;
        public int TlsPort { get; set; } = 443;
        public bool IsTlsEnabled { get; set; }

        public bool TlsRedirect { get; set; }
        public X509Certificate2 Certificate { get; set; }
        public SslProtocols TlsProtocols { get; set; } = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;        
        public XFrameOptions XFrameOptions { get; set; }  
    }
}