using System;
using System.Collections.Generic;
using System.Text;

namespace LR3_CSaN
{
    class UdpMessage
    {
        const char DELIMITER = '>';
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

        private void ParseMessageFromBytes(byte[] message)
        {
            string messageString = Encoding.Unicode.GetString(message);

            Ip = "";
            int i = 0;
            while (messageString[i] != DELIMITER)
            {
                Ip += messageString[i];
                ++i;
            }
            ++i; // Переходим на первый символ имени
            Username = messageString.Substring(i);
        }
    }
}
