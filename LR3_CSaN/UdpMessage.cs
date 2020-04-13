using System;
using System.Collections.Generic;
using System.Text;

namespace LR3_CSaN
{
    class UdpMessage
    {
        const char DELIMITER = '>';
        public string Ip { get; set; }
        public string UserName { get; set; }

        public UdpMessage(string ip, string userName)
        {
            Ip = ip;
            UserName = userName;
        }

        public UdpMessage(byte[] message)
        {
            ParseMessageFromBytes(message);
        }

        public byte[] ToBytes()
        {
            return Encoding.Unicode.GetBytes(Ip + DELIMITER + UserName);
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
            ++i; //Переходим на первый символ имени
            UserName = messageString.Substring(i);
        }
    }
}
