using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpServer
{
    internal class Program
    {
        public static ManualResetEvent IsInProgress = new ManualResetEvent(false);
        static void Main(string[] args)
        {
            Server server = new Server();
            server.Start();
            Console.WriteLine("Server started...");
            while (true)
            {
                IsInProgress.Reset();
                server.Accept();
                IsInProgress.WaitOne();
            }
        }
    }
}
