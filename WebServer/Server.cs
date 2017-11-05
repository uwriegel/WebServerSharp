using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace WebServer
{

    public class Server
    {
        public string PhysicalPath { get { return Configuration.Webroot; } }
        public Configuration Configuration { get; private set; }
        public bool IsStarted { get; private set; }
        
        public Server(Configuration configuration)
        {
            Console.WriteLine("Initializing Server...");
            Configuration = configuration;
            if (string.IsNullOrEmpty(configuration.DomainName))
                configuration.DomainName = Dns.GetHostEntry(Environment.MachineName).HostName;
            Console.WriteLine($"Domain name: {configuration.DomainName}");

            if (configuration.LocalAddress != IPAddress.Any)
                Console.WriteLine($"Binding to local address: {configuration.LocalAddress}");

            if (Configuration.IsTlsEnabled)
            {
                Console.WriteLine("Initializing TLS");

                // InitializeTls();
                // Console.WriteLine($"Listening on secure port {configuration.TlsPort}");
                // var result = Ipv6TcpListenerFactory.Create(configuration.TlsPort);
                // tlsListener = result.Listener;
                // if (!result.Ipv6)
                //     Console.WriteLine("IPv6 or IPv6 dual mode not supported, switching to IPv4");

                // if (configuration.TlsRedirect)
                // {
                //     Console.WriteLine("Initializing TLS redirect");
                //     result = Ipv6TcpListenerFactory.Create(configuration.Port);
                //     tlsRedirectorListener = result.Listener;
                //     if (!result.Ipv6)
                //         Console.WriteLine("IPv6 or IPv6 dual mode not supported, switching to IPv4");
                // }
                Console.WriteLine("TLS initialized");
            }
            
            if (!Configuration.TlsRedirect)
            {
                Console.WriteLine($"Listening on port {configuration.Port}");
                var result = Ipv6TcpListenerFactory.Create(configuration.Port);
                listener = result.Listener;
                if (!result.Ipv6)
                    Console.WriteLine("IPv6 or IPv6 dual mode not supported, switching to IPv4");
            }

            Console.WriteLine("Server initialized");
        }

        static Server()
        {
            ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ThreadPool.SetMinThreads(1000, 1000);
        }  

        public void Start()
        {
            try
            {
                Console.WriteLine("Starting HTTP Listener...");
                if (listener != null)
                    listener.Start();
                if (tlsListener != null)
                    tlsListener.Start();
                IsStarted = true;
                if (listener != null)
                    StartConnecting(listener, false);
                if (tlsListener != null)
                    StartConnecting(tlsListener, true);

                // if (tlsRedirectorListener != null)
                // {
                //     tlsRedirectorListener.Start();
                //     StartTlsRedirecting();
                // }
                Console.WriteLine("HTTP Listener started");
            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                throw;
            }
            catch (Exception e)
            {
                IsStarted = false;
                Console.WriteLine($"Could not start HTTP Listener: {e}");
            }
        }

        public void Stop()
        {
            try
            {
                Console.WriteLine("Stopping HTTP Listener...");
                IsStarted = false;

                listener?.Stop();
                tlsListener?.Stop();
                tlsRedirectorListener?.Stop();

                Console.WriteLine("HTTP Listener stopped");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not stop HTTP Listener: {0}", e);
            }
        }              

        async void StartConnecting(TcpListener listener, bool isSecured)
        {
            if (!IsStarted)
                return;
            try
            {
                while (IsStarted)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    OnConnected(client, isSecured);
                }
            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.Interrupted && !IsStarted)
            {
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error occurred in connecting thread: {e}");
            }
        }
        
        async void OnConnected(TcpClient tcpClient, bool isSecured)
        {
            try
            {
                if (!IsStarted)
                    return;

                var session = new SocketSession(this, tcpClient, isSecured);
                await session.ReceiveAsync();
            }
            catch (SocketException se) when (se.NativeErrorCode == 10054)
            { }
            catch (ObjectDisposedException)
            {
                // Stop() aufgerufen
                return;
            }
            catch (Exception e)
            {
                if (!IsStarted)
                    return;
                Console.WriteLine($"Error in OnConnected occurred: {e}");
            }
        }

        TcpListener listener;
        TcpListener tlsListener = null;
        TcpListener tlsRedirectorListener = null;        
    }
}