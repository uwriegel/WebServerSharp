using System.Net;
using System.Net.Sockets;

namespace WebServer
{
    public static class Ipv6TcpListenerFactory
    {
        public struct Ipv6Listener
        {
            public TcpListener Listener;
            public bool Ipv6;
        }
        public static Ipv6Listener Create(int port)
        {
            Ipv6Listener result = new Ipv6Listener();
            try
            {
                result.Listener = new TcpListener(IPAddress.IPv6Any, port);
                result.Listener.Server.SetDualMode();
                result.Ipv6 = true;
            }
            catch (SocketException se)
            {
                if (se.SocketErrorCode != SocketError.AddressFamilyNotSupported)
                    throw;
                result.Listener = new TcpListener(IPAddress.Any, port);
                result.Ipv6 = false;
            }
            return result;
        }
    }
}