using System;
using WebServer;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new Server(new Configuration{
                Port = 20000,
                Webroot = "/home/uwe/Projekte/Node/WebServerElectron/web"
            });
            server.Start();
            Console.ReadLine();
            server.Stop();
        }
    }
}
