using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LR3_CSaN
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly SynchronizationContext context;
        private UdpClient udpReceiver;
        private bool aliveUdpTask;
        private bool aliveTcpTask;

        public string Username { get; set; }
        public string IpAddress { get; set; }
        public Dictionary<string, IPEndPoint> ChatUsers { get; set; }
        public string History { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            context = SynchronizationContext.Current;
            ChatUsers = new Dictionary<string, IPEndPoint>();
            tbMessage.IsEnabled = false;
        }

        private void GetIpAddress() // Получение локального IP-адреса пользователя
        {
            Socket tempSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.IP);
            try
            {
                tempSocket.Connect("8.8.8.8", 8100);
                IpAddress = ((IPEndPoint)tempSocket.LocalEndPoint).Address.ToString();
            }
            catch
            {
                IpAddress = ((IPEndPoint)tempSocket.LocalEndPoint).Address.ToString();
            }
            tempSocket.Shutdown(SocketShutdown.Both);
            tempSocket.Close();
        }

        private string GetShortIpAddress(string fullAddress)
        {
            string shortIpAddress = "";
            for (int i = 0; i < fullAddress.Length; i++)
            {
                if (fullAddress[i] == ':')
                    break;
                shortIpAddress += fullAddress[i];
            }
            return shortIpAddress;
        }

        private void SendFirstNotification() // Отправление UDP-пакета всем пользователям
        {
            const int LOCAL_PORT = 8501;
            const int REMOTE_PORT = 8502;

            try
            {
                IPEndPoint sourceEndPoint = new IPEndPoint(IPAddress.Parse(IpAddress), LOCAL_PORT);
                UdpClient udpSender = new UdpClient(sourceEndPoint);
                IPEndPoint destEndPoint = new IPEndPoint(IPAddress.Broadcast, REMOTE_PORT);
                udpSender.EnableBroadcast = true;

                UdpMessage udpMessage = new UdpMessage(IpAddress, Username);
                byte[] messageBytes = udpMessage.ToBytes();
                udpSender.Send(messageBytes, messageBytes.Length, destEndPoint);
                udpSender.Close();

                string messageChat = "Вы успешно подключились!\r\n";
                tbChat.AppendText(messageChat);
                string datetime = DateTime.Now.ToString();
                History += string.Format("{0} {1} присоединялся к чату\r\n", datetime, Username);
            }
            catch
            {
                throw new Exception("Не удалось отправить уведомление о новом пользователе.");
            }
        }

        private void ListenUdpMessages() // Приём UDP-пакетов от новых пользователей
        {
            const int REMOTE_UDP_PORT = 8501;
            const int LOCAL_UDP_PORT = 8502;
            const int REMOTE_TCP_PORT = 8503;

            aliveUdpTask = true;

            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(IpAddress), LOCAL_UDP_PORT);
            udpReceiver = new UdpClient(localEndPoint);
            while (aliveUdpTask)
            {
                try
                {
                    IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, REMOTE_UDP_PORT); // Любой порт
                    byte[] message = udpReceiver.Receive(ref remoteEndPoint);

                    UdpMessage receiveMessage = new UdpMessage(message);
                    context.Post(delegate (object state)
                    {
                        string datetime = DateTime.Now.ToString();
                        string messageChat = string.Format("{0} {1} присоединялся к чату\r\n", datetime, receiveMessage.Username);
                        tbChat.AppendText(messageChat);
                    }, null);

                    Socket tcpSenderSocket;
                    try // Устанавливаем подключение с новым пользователем
                    {
                        IPEndPoint connectionEndPoint = new IPEndPoint(IPAddress.Parse(receiveMessage.Ip), REMOTE_TCP_PORT);
                        tcpSenderSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        ChatUsers.Add(receiveMessage.Username, connectionEndPoint);
                        tcpSenderSocket.Connect(connectionEndPoint);
                    }
                    catch
                    {
                        throw new Exception("Не удалось установить соединение с пользователем.");
                    }
                    try // Отправляем новому пользователю своё имя
                    {
                        TcpMessage tcpMessage = new TcpMessage(IpAddress, Username);
                        tcpSenderSocket.Send(tcpMessage.ToBytes());
                        tcpSenderSocket.Shutdown(SocketShutdown.Both);
                        tcpSenderSocket.Close();
                    }
                    catch
                    {
                        throw new Exception("Не удалось отправить уведомление о вашем существовании.");
                    }
                }
                catch (SocketException)
                {
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        private void ListenTcpMessages() // Приём TCP-пакетов
        {
            const int LOCAL_PORT = 8503;
            const int REMOTE_PORT = 8503;

            aliveTcpTask = true;
            bool firstUser = true;

            try
            {
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(IpAddress), LOCAL_PORT);
                Socket tcpListenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                tcpListenerSocket.Bind(localEndPoint);
                tcpListenerSocket.Listen(10);

                while (aliveTcpTask)
                {
                    Socket listener = tcpListenerSocket.Accept();

                    StringBuilder data = new StringBuilder();
                    int size = 0;
                    byte[] buffer = new byte[256];

                    do
                    {
                        size = listener.Receive(buffer);
                        data.Append(Encoding.Unicode.GetString(buffer, 0, size));
                    }
                    while (listener.Available > 0);

                    try
                    {
                        TcpMessage tcpMessage = new TcpMessage(Encoding.Unicode.GetBytes(data.ToString()));
                        if (tcpMessage.Code == 1) // Передали имя
                        {
                            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse(tcpMessage.Ip), REMOTE_PORT);
                            ChatUsers.Add(tcpMessage.Username, remoteEndPoint);
                            if (firstUser)
                            {
                                try
                                {
                                    Socket tcpHistorySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                                    tcpHistorySocket.Connect(ChatUsers[tcpMessage.Username]);
                                    TcpMessage tcpHistoryMessage = new TcpMessage(4, "History");
                                    tcpHistorySocket.Send(tcpHistoryMessage.ToBytes());
                                    tcpHistorySocket.Shutdown(SocketShutdown.Both);
                                    tcpHistorySocket.Close();
                                }
                                catch
                                {
                                    context.Post(delegate (object state)
                                    {
                                        tbChat.AppendText("История пуста.\r\n");
                                    }, null);
                                }
                                finally
                                {
                                    firstUser = false;
                                }
                            }
                        }
                        else if (tcpMessage.Code == 2) // Передали сообщение
                        {
                            string senderName = "";
                            if (ChatUsers != null)
                            {
                                foreach (string username in ChatUsers.Keys)
                                {
                                    if (GetShortIpAddress(listener.RemoteEndPoint.ToString()) == GetShortIpAddress(ChatUsers[username].ToString()))
                                    {
                                        senderName = username;
                                        break;
                                    }
                                }
                                context.Post(delegate (object state)
                                {
                                    string datetime = DateTime.Now.ToString();
                                    string messageChat = string.Format("{0} {1}: {2}\r\n", datetime, senderName, tcpMessage.MessageText);
                                    History += messageChat;
                                    tbChat.AppendText(messageChat);
                                }, null);
                            }
                        }
                        else if (tcpMessage.Code == 3) // Передали сообщение о выходе пользователя
                        {
                            string senderName = "";
                            if (ChatUsers != null)
                            {
                                foreach (string username in ChatUsers.Keys)
                                {
                                    if (GetShortIpAddress(listener.RemoteEndPoint.ToString()) == GetShortIpAddress(ChatUsers[username].ToString()))
                                    {
                                        senderName = username;
                                        break;
                                    }
                                }
                                ChatUsers.Remove(senderName);
                                context.Post(delegate (object state)
                                {
                                    string datetime = DateTime.Now.ToString();
                                    string messageChat = string.Format("{0} {1}\r\n", datetime, tcpMessage.MessageText);
                                    History += messageChat;
                                    tbChat.AppendText(messageChat);
                                }, null);
                            }
                        }
                        else if (tcpMessage.Code == 4) // Передали запрос на историю
                        {
                            try
                            {
                                Socket tcpHistorySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                                IPEndPoint destEndPoint = new IPEndPoint(IPAddress.Parse(GetShortIpAddress(listener.RemoteEndPoint.ToString())), REMOTE_PORT);
                                tcpHistorySocket.Connect(destEndPoint);
                                TcpMessage tcpHistoryMessage = new TcpMessage(5, History);
                                tcpHistorySocket.Send(tcpHistoryMessage.ToBytes());
                                tcpHistorySocket.Shutdown(SocketShutdown.Both);
                                tcpHistorySocket.Close();
                            }
                            catch { }
                        }
                        else if (tcpMessage.Code == 5) // Передали историю
                        {
                            if (tcpMessage.MessageText != "")
                            {
                                context.Post(delegate (object state)
                                {
                                    History = tcpMessage.MessageText + History;
                                    tbChat.Text = tcpMessage.MessageText + tbChat.Text;
                                }, null);
                            }
                        }
                        listener.Shutdown(SocketShutdown.Both);
                        listener.Close();
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void SendTcpMessage()
        {
            string userMessage = tbMessage.Text;
            TcpMessage tcpMessage = new TcpMessage(2, userMessage);
            string datetime = DateTime.Now.ToString();
            string messageChat = string.Format("{0} Вы: {1}\r\n", datetime, userMessage);
            if (ChatUsers != null)
            {
                foreach (string username in ChatUsers.Keys)
                {
                    try
                    {
                        Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        tcpSocket.Connect(ChatUsers[username]);
                        tcpSocket.Send(tcpMessage.ToBytes());
                        tcpSocket.Shutdown(SocketShutdown.Both);
                        tcpSocket.Close();
                    }
                    catch
                    {
                        MessageBox.Show(string.Format("Не удалось отправить сообщение пользователю {0}.", username));
                    }
                }
            }
            tbChat.AppendText(messageChat);
            tbMessage.Clear();
            History += string.Format("{0} {1}: {2}\r\n", datetime, Username, userMessage);
        }

        private void ButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            tbUserName.Text = tbUserName.Text.Trim();
            Username = tbUserName.Text;
            if (Username.Length > 0)
            {
                GetIpAddress();
                tbUserName.IsReadOnly = true;
                try
                {
                    SendFirstNotification();
                    Task listenUdpTask = new Task(ListenUdpMessages);
                    listenUdpTask.Start();
                    Task listenTcpTask = new Task(ListenTcpMessages);
                    listenTcpTask.Start();

                    btnConnect.IsEnabled = false;
                    tbMessage.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    ExitChat();
                }
            }
        }

        private void ButtonSend_Click(object sender, RoutedEventArgs e)
        {
            SendTcpMessage();
        }

        private void ExitChat()
        {
            aliveUdpTask = false;
            aliveTcpTask = false;

            if (udpReceiver != null)
                udpReceiver.Close();

            if (ChatUsers != null)
            {
                foreach (string username in ChatUsers.Keys)
                {
                    try
                    {
                        Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        tcpSocket.Connect(ChatUsers[username]);
                        string datetime = DateTime.Now.ToString();
                        string message = string.Format("{0} покинул чат\r\n", Username);
                        string exit = string.Format("{0} {1}", datetime, message);

                        TcpMessage tcpMessage = new TcpMessage(3, message);
                        History += exit;
                        tbChat.AppendText(exit);

                        tcpSocket.Send(tcpMessage.ToBytes());
                        tcpSocket.Shutdown(SocketShutdown.Both);
                        tcpSocket.Close();
                    }
                    catch
                    {
                        MessageBox.Show("Ошибка отправки уведомления о выходе из чата.");
                    }
                }
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (aliveUdpTask && aliveTcpTask)
                ExitChat();
        }
    }
}