using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ServerApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"Konusarak Ogren Sunucu Yazılımı Başlatıldı.");
            var listener = new Listener(34567);

            Console.ReadLine();
        }
    }

    public class Listener
    {
        private Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        public Listener(int port)
        {
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
        }

        private void SocketAccept()
        {
            serverSocket.BeginAccept((ar) =>
            {
                SocketAccept();
                var clientSocket = serverSocket.EndAccept(ar);

                SocketReceive(clientSocket, new StateObject(clientSocket));

            }, null);
        }

        private void SocketReceive(Socket clientSocket, StateObject stateObject)
        {
            if (!clientSocket.Connected)
                return;

            clientSocket.BeginReceive(stateObject.Buffer, 0, 256, SocketFlags.None, (ar) => {

                var _stateObject = ar.AsyncState as StateObject;
                var _clientSocket = _stateObject.ClientSocket;

                if (!clientSocket.Connected)
                    return;

                var receivedByteLength = clientSocket.EndReceive(ar);

                if (receivedByteLength == 0)
                    return;

                var receivedText = Encoding.UTF8.GetString(_stateObject.Buffer);

                Console.WriteLine($"receivedText : {receivedText}");

            }, stateObject);
        }
    }

    public class StateObject
    {
        public Socket ClientSocket { get; private set; }
        public byte[] Buffer { get; private set; }

        public StateObject(Socket clientSocket)
        {
            ClientSocket = clientSocket;
            Buffer = new byte[256];
        }
    }
}
