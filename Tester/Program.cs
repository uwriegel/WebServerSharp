using System;
using WebServer;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            var configuration = new Configuration();
            configuration.Webroot = ".";
            Console.WriteLine("Hello World!");
        }
    }
}
