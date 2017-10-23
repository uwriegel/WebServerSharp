using System;
using WebServer;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new Server(new Configuration{
                Port = 20000
            });
            server.Start();
            System.Threading.Thread.Sleep(10000);
            server.Stop();
        }
    }
}
