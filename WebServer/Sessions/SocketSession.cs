using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace WebServer
{
    /// <summary>
    /// Bei HTTP wird die Socket für mehrere Aufrufe wiederverwendet.
    /// Hiermit wird eine solche Session implementiert, im Gegensatz zur logischen <see cref="RequestSession"/>, die bei jedem Aufruf neu angelegt wird
    /// </summary>
    class SocketSession
    {
        public TcpClient Client { get; private set; }

        public bool UseTls { get; }

        public SocketSession(Server server, TcpClient client, bool useTls)
        {
            this.UseTls = useTls;
            this.server = server;
            this.Client = client;
            client.ReceiveTimeout = server.Configuration.SocketTimeout;
            client.SendTimeout = server.Configuration.SocketTimeout;
        }        

        public void BeginReceive()
        {
            try
            {
                if (networkStream == null)
                    networkStream = UseTls ? GetTlsNetworkStream(Client) : Client.GetStream();

                var session = new RequestSession(server, this, networkStream);
                session.BeginStart();
            }
            catch (AuthenticationException ae)
            {
                Console.WriteLine($"An authentication error has occurred while reading socket, session: {Client.Client.RemoteEndPoint as IPEndPoint}, error: {ae}");
            }
            catch (Exception e) when (e is IOException || e is CloseException || e is SocketException)
            {
                Close();
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error has occurred while reading socket, error: {e}");
            }
        }        

        public void Close()
        {
            Client.Close();
        }

        static Stream GetTlsNetworkStream(NetworkStream stream, X509Certificate2 certificate)
        {
            var sslStream = new SslStream(stream);
            // TODO: einstellbar
            sslStream.AuthenticateAsServer(certificate, false, System.Security.Authentication.SslProtocols.Tls | System.Security.Authentication.SslProtocols.Tls11 | System.Security.Authentication.SslProtocols.Tls12, true);
            var welches = sslStream.SslProtocol;
            return sslStream;
        }

        Stream GetTlsNetworkStream(TcpClient tcpClient)
        {
            var stream = tcpClient.GetStream();
            if (!server.Configuration.IsTlsEnabled)
                return null;

            var sslStream = new SslStream(stream);
            sslStream.AuthenticateAsServer(server.Configuration.Certificate, false, server.Configuration.TlsProtocols, false);
            return sslStream;
        }
        
        protected Server server;
        protected Stream networkStream;        
    }
}