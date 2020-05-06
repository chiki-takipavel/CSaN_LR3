using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LR3_CSaN
{
    public class ChatUser
    {
        public bool IsOnline { get; set; } = true;
        public string Ip { get; set; }
        public string Username { get; set; }
        public int Port { get; set; }
        public TcpClient Client { get; set; }
        public NetworkStream Stream { get; set; }

        public ChatUser(TcpClient client, int port)
        {
            Client = client;
            Port = port;
            Stream = client.GetStream();
        }

        public ChatUser(string username, string ip, int port)
        {
            Ip = ip;
            Username = username;
            Port = port;
        }

        public void Connect()
        {
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(Ip), Port);
            Client = new TcpClient();
            Client.Connect(ipEndPoint);
            Stream = Client.GetStream();
        }

        public void SendMessage(TcpMessage tcpMessage)
        {
            byte[] data = tcpMessage.ToBytes();
            try
            {
                Stream.Write(data, 0, data.Length);
            }
            catch { }
        }

        public byte[] RecieveMessage()
        {
            StringBuilder data = new StringBuilder();
            byte[] buffer = new byte[256];
            do
            {
                int size = Stream.Read(buffer, 0, buffer.Length);
                data.Append(Encoding.Unicode.GetString(buffer, 0, size));
            }
            while (Stream.DataAvailable);

            return Encoding.Unicode.GetBytes(data.ToString());
        }

        public void Dispose()
        {
            IsOnline = false;
            Stream.Dispose();
            Client.Dispose();
        }
    }
}
