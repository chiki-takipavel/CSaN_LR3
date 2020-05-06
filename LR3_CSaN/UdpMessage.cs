using System;
using System.Text;

namespace LR3_CSaN
{
    class UdpMessage
    {
        const string DELIMITER = ">";
        public string Ip { get; set; }
        public string Username { get; set; }

        public UdpMessage(string ip, string username)
        {
            Ip = ip;
            Username = username;
        }

        public UdpMessage(byte[] message)
        {
            ParseMessageFromBytes(message);
        }

        public byte[] ToBytes()
        {
            return Encoding.Unicode.GetBytes(Ip + DELIMITER + Username);
        }

        private string[] Explode(string message, string delimiter)
        {
            return message.Split(new string[] { delimiter }, StringSplitOptions.None);
        }

        private void ParseMessageFromBytes(byte[] message)
        {
            string stringMessage = Encoding.Unicode.GetString(message);
            string[] messageFields = Explode(stringMessage, DELIMITER);
            Ip = messageFields[0];
            Username = messageFields[1];
        }
    }
}
