using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Net.Sockets;

namespace TwitchChat
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Bots created: " + Operations.createBots());
            Console.OutputEncoding = Encoding.Unicode;
            Console.ReadKey();
        }
    }
}
