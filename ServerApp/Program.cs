using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ServerApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"Konusarak Ogren Sunucu Yazılımı Başlatıldı.");
            var listener = new Listener(34567);
            listener.IIncomingHandler = new IncomingHandler();

            Console.ReadLine();
        }
    }

    public class Listener
    {
        private Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        public IIncomingHandler IIncomingHandler { get; set; }

        public Listener(int port)
        {
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            serverSocket.Listen(10);
            SocketAccept();
        }

        private void SocketAccept()
        {
            serverSocket.BeginAccept((ar) =>
            {
                var clientSocket = serverSocket.EndAccept(ar);
                SocketAccept();

                SocketReceive(clientSocket, new StateObject(clientSocket));

            }, null);
        }

        private void SocketReceive(Socket clientSocket, StateObject stateObject)
        {
            if (!clientSocket.Connected)
                return;

            clientSocket.BeginReceive(stateObject.Header, 0, stateObject.HeaderLength, SocketFlags.None, (ar) => {

                if (!clientSocket.Connected)
                    return;

                var receivedByteLength = clientSocket.EndReceive(ar);

                if (receivedByteLength == 0)
                    return;

                var bodyLength = BitConverter.ToInt32(stateObject.Header, 0);
                Console.WriteLine($"receivedByteLength : { receivedByteLength } / bodyLength : { bodyLength }");

                stateObject.BufferLength = bodyLength;
                clientSocket.BeginReceive(stateObject.Buffer, 0, bodyLength, SocketFlags.None, (ar2) => {

                    var receivedByteLength2 = clientSocket.EndReceive(ar2);

                    if (receivedByteLength2 == 0)
                        return;

                    var _stateObject = ar.AsyncState as StateObject;
                    //var _clientSocket = _stateObject.ClientSocket;

                    var receivedText = Encoding.UTF32.GetString(_stateObject.Buffer);
                    Console.WriteLine($"receivedText : {receivedText}");
                    IIncomingHandler.Handler(this, clientSocket, DateTime.Now, receivedText);
                    SocketReceive(clientSocket, new StateObject(clientSocket));
                }, stateObject);

            }, stateObject);
        }

        public void Send(Socket clientSocket, string text)
        {
            var textBytes = Encoding.UTF32.GetBytes(text);
            var LengthBytes = BitConverter.GetBytes(textBytes.Length);

            Console.WriteLine($"LengthBytes : {LengthBytes.Length}");

            clientSocket.BeginSend(LengthBytes, 0, LengthBytes.Length, SocketFlags.None, (ar) => {
                var sendedByte = 0;

                try
                {
                    sendedByte = clientSocket.EndSend(ar);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                

                if (sendedByte == 0)
                    return;

                clientSocket.BeginSend(textBytes, 0, textBytes.Length, SocketFlags.None, (ar2) =>
                {
                    try
                    {
                        sendedByte = clientSocket.EndSend(ar2);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }

                    if (sendedByte == 0)
                        return;
                }, null);

            }, null);
        }
    }

    public class StateObject
    {
        public int HeaderLength { get; set; } = 4;
        public int BufferLength { set { Buffer = new byte[value]; } }
        public Socket ClientSocket { get; private set; }
        public byte[] Header { get; private set; }
        public byte[] Buffer { get; private set; }

        public StateObject(Socket clientSocket)
        {
            ClientSocket = clientSocket;
            Header = new byte[HeaderLength];
        }
    }

    public interface IIncomingHandler
    {
        void Handler(Listener listener, Socket socket, DateTime receivedDateTime, string text);
    }

    public class IncomingHandler : IIncomingHandler
    {
        public IncomingHandler()
        {
        }

        /// <summary>
        /// key(string) : remoteEndPoint,
        /// value(tuple<datetime,int,string) : receivedtime,received message count at one second,received text data
        /// </summary>
        Dictionary<string, PackageInformationOfSocket> socketToData = new Dictionary<string, PackageInformationOfSocket>();
        public void Handler(Listener listener, Socket clientSocket, DateTime receivedDateTime, string text)
        {
            var remoteEndPoint = clientSocket.RemoteEndPoint.ToString();
            Console.WriteLine($"socket.RemoteEndPoint.ToString():{remoteEndPoint}");

            if (!socketToData.ContainsKey(remoteEndPoint))
                socketToData.Add(remoteEndPoint, new PackageInformationOfSocket { 
                    Text = text,
                });

            var socketData = socketToData[remoteEndPoint];

            if (!socketData.LastReceivedDateTime.HasValue)
                socketData.LastReceivedDateTime = receivedDateTime;
            else { 
                var diffReceived = receivedDateTime - socketData.LastReceivedDateTime.Value;
                socketData.LastReceivedDateTime = receivedDateTime;

                Console.WriteLine($"diffReceived.TotalSeconds:{diffReceived.TotalSeconds}");

                if (diffReceived.TotalSeconds <= 1)
                    socketData.ReceivedMessageCountAtOneSecond++;
                else
                    socketData.ReceivedMessageCountAtOneSecond = 0;

                if (socketData.CountAttemptsOfSameError == 1)
                    clientSocket.Close();
                else
                {
                    if (socketData.ReceivedMessageCountAtOneSecond > 1)
                    {
                        listener.Send(clientSocket, "Flood tekrarladığınız taktirde bağlantınız koparılacaktır.");
                        socketData.CountAttemptsOfSameError++;
                    }
                }
            }
        }
    }

    public class PackageInformationOfSocket
    {
        public DateTime? LastReceivedDateTime { get; set; }
        public int ReceivedMessageCountAtOneSecond { get; set; } = 0;
        public int CountAttemptsOfSameError { get; set; } = 0;
        public string Text { get; set; }
    }
}
