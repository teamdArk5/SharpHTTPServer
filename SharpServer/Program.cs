using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
            int port = 80;

            if (args.Length == 1) { port = int.Parse(args[0]); }
            Server server = new Server(port);
            
            server.Start();
            Console.WriteLine(@"
   _____ _                      _    _ _______ _______ _____   _____                          
  / ____| |                    | |  | |__   __|__   __|  __ \ / ____|                         
 | (___ | |__   __ _ _ __ _ __ | |__| |  | |     | |  | |__) | (___   ___ _ ____   _____ _ __ 
  \___ \| '_ \ / _` | '__| '_ \|  __  |  | |     | |  |  ___/ \___ \ / _ \ '__\ \ / / _ \ '__|
  ____) | | | | (_| | |  | |_) | |  | |  | |     | |  | |     ____) |  __/ |   \ V /  __/ |   
 |_____/|_| |_|\__,_|_|  | .__/|_|  |_|  |_|     |_|  |_|    |_____/ \___|_|    \_/ \___|_|   
                         | |                                                                  
                         |_|                                                                  
by @IversOn5
");
            Console.WriteLine($"Server started on {port}...");
            while (true)
            {
                IsInProgress.Reset();
                server.Accept();
                IsInProgress.WaitOne();
            }
        }
    }
}
