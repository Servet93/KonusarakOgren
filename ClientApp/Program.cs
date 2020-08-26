using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ClientApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Konusarak Ogren Istemci Yazilimi Baslatildi.");
            var client = new Client();

            do
            {
                Console.WriteLine("Flood Atilsin Mi?.E girilirse atilacak.");
                Console.Write("Text Giriniz : ");

                var text = Console.ReadLine();
                if (text.Equals("E"))
                {
                    client.Send("Flood 1");
                    Thread.Sleep(250);
                    client.Send("Flood 2");
                    Thread.Sleep(250);
                    client.Send("Flood 3");
                    Thread.Sleep(250);
                    client.Send("Flood 4");
                }
                else
                    client.Send(text);

            } while (true);

        }
    }

    public class Client
    {
        private Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);


        public Client(string ip = "127.0.0.1", int port = 34567)
        {
            serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            serverSocket.BeginConnect(new IPEndPoint(IPAddress.Parse(ip), port), (ar) => {
                serverSocket.EndConnect(ar);

                SocketReceive();

            }, null);
        }

        private void SocketReceive()
        {
            var stateObject = new StateObject();
            serverSocket.BeginReceive(stateObject.Header, 0, stateObject.HeaderLength, SocketFlags.None, (ar) => {

                var receivedByteLength = serverSocket.EndReceive(ar);

                if (receivedByteLength == 0)
                    return;

                var bodyLength = BitConverter.ToInt32(stateObject.Header, 0);
                Console.WriteLine($"receivedByteLength : { receivedByteLength } / bodyLength : { bodyLength }");

                stateObject.BufferLength = bodyLength;
                serverSocket.BeginReceive(stateObject.Buffer, 0, bodyLength, SocketFlags.None, (ar2) => {
                    receivedByteLength = serverSocket.EndReceive(ar2);

                    if (receivedByteLength == 0)
                        return;

                    var receivedText = Encoding.UTF32.GetString(stateObject.Buffer);
                    Console.WriteLine($"receivedText from server : {receivedText}");
                }, stateObject);


                SocketReceive();

            }, stateObject);
        }

        public void Send(string text)
        {
            try
            {
                if (!serverSocket.Connected) { 
                    Console.WriteLine("Bağlantı Hatası");
                    return;
                }

                var textBytes = Encoding.UTF32.GetBytes(text);
                var LengthBytes = BitConverter.GetBytes(textBytes.Length);

                serverSocket.BeginSend(LengthBytes, 0, LengthBytes.Length, SocketFlags.None, (ar) => {
                    var sendedByte = 0;
                    try
                    {
                        sendedByte = serverSocket.EndSend(ar);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Bağlantı Hatası:{e.Message}");
                    }

                    if (sendedByte == 0)
                        return;

                    if (!serverSocket.Connected)
                    {
                        Console.WriteLine("Bağlantı Hatası");
                        return;
                    }

                    serverSocket.BeginSend(textBytes, 0, textBytes.Length, SocketFlags.None, (ar2) =>
                    {
                        try
                        {
                            sendedByte = serverSocket.EndSend(ar2);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Bağlantı Hatası:{e.Message}");
                        }

                    }, null);

                }, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send Exception : {ex.ToString()}");
            }
        }
    }

    public class StateObject
    {
        public int HeaderLength { get; set; } = 4;
        public int BufferLength { set { Buffer = new byte[value]; } }
        public byte[] Header { get; private set; }
        public byte[] Buffer { get; private set; }

        public StateObject()
        {
            Header = new byte[HeaderLength];
        }
    }
}
