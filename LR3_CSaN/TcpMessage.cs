using System;
using System.Text;

namespace LR3_CSaN
{
    public class TcpMessage
    {
        const string DELIMITER = "|";
        public int Code { get; set; }
        public string Ip { get; set; }
        public string Username { get; set; }
        public string MessageText { get; set; }

        public TcpMessage(int code, string ip, string username)
        {
            Code = code;
            Ip = ip;
            Username = username;
        }

        public TcpMessage(int code, string messageText)
        {
            Code = code;
            MessageText = messageText;
        }

        public TcpMessage(byte[] message)
        {
            ParseMessageFromBytes(message);
        }

        private string[] Explode(string message, string delimiter)
        {
            return message.Split(new string[] { delimiter }, StringSplitOptions.None);
        }

        private void ParseMessageFromBytes(byte[] message)
        {
            string stringMessage = Encoding.Unicode.GetString(message);
            string[] messageFields = Explode(stringMessage, DELIMITER);
            if (Code == 1) // CONNECT
            {
                Code = int.Parse(messageFields[0]);
                Ip = messageFields[1];
                Username = messageFields[2];
            }
            else
            {
                Code = int.Parse(messageFields[0]);
                MessageText = messageFields[1];
            }    
        }

        public byte[] ToBytes()
        {
            if (Code == 1) // CONNECT
            {
                return Encoding.Unicode.GetBytes(Code.ToString() + DELIMITER + Ip + DELIMITER + Username);
            }
            else
            {
                return Encoding.Unicode.GetBytes(Code.ToString() + DELIMITER + MessageText);
            }
        }
    }
}
