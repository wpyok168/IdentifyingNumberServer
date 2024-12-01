using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using HPSocket;

namespace IdentifyingNumberServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            HpsocketServer server = new HpsocketServer();
            server.Start("0.0.0.0", 8899);
            Console.ReadLine();
        }
    }
}
