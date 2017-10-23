using System.Net.Sockets;

namespace WebServer
{
    /// <summary>
    /// Bei HTTP wird die Socket für mehrere Aufrufe wiederverwendet.
    /// Hiermit wird eine solche Session implementiert, im Gegensatz zur logischen <see cref="RequestSession"/>, die bei jedem Aufruf neu angelegt wird
    /// </summary>
    class SocketSession
    {
        public SocketSession(Server server, TcpClient client, bool useTls)
        {
        }        

        public void BeginReceive()
        {
        }        
    }
}