﻿using System;
using System.Text;

namespace LR3_CSaN
{
    class TcpMessage
    {
        const char DELIMITER = '>';
        public int Code { get; set; } // 1 - передача имени и IP-адреса, 2 - передача сообщения пользователя, 3 - передача сообщения о выходе 
        public string Ip { get; set; } // 4 - запрос истории, 5 - отправка истории
        public string Username { get; set; }
        public string MessageText { get; set; }

        public TcpMessage(string ip, string username)
        {
            Code = 1;
            Ip = ip;
            Username = username;
        }

        public TcpMessage(int code, string username, string messageText)
        {
            Code = code;
            Username = username;
            MessageText = messageText;
        }

        public TcpMessage(byte[] message)
        {
            ParseMessageFromBytes(message);
        }

        private void ParseMessageFromBytes(byte[] message)
        {
            string messageString = Encoding.Unicode.GetString(message);

            // Получаем код из сообщения
            Code = 0;
            int i = 0;
            string messageCode = "";
            while (messageString[i] != DELIMITER)
            {
                messageCode += messageString[i];
                ++i;
            }
            Code = Int32.Parse(messageCode);
            Code -= Int32.Parse("0"); 

            ++i;
            if (Code == 1)
            {
                Ip = "";
                while (messageString[i] != DELIMITER)
                {
                    Ip += messageString[i];
                    ++i;
                }
                ++i; // Переходим на первый символ имени
                Username = messageString.Substring(i);
            }
            else if (Code > 1)
            {
                Username = "";
                while (messageString[i] != DELIMITER)
                {
                    Username += messageString[i];
                    ++i;
                }
                ++i; // Переходим на первый символ сообщения
                MessageText = messageString.Substring(i);
            }
        }

        public byte[] ToBytes()
        {
            if (Code == 1)
            {
                return Encoding.Unicode.GetBytes(Code.ToString() + DELIMITER + Ip + DELIMITER + Username);
            }
            else
            {
                return Encoding.Unicode.GetBytes(Code.ToString() + DELIMITER + Username + DELIMITER + MessageText);
            }
        }
    }
}
