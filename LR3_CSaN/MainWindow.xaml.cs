using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;

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
        private const int CONNECT = 1;
        private const int MESSAGE = 2;
        private const int EXIT = 3;
        private const int SEND_HISTORY = 4;
        private const int SHOW_HISTORY = 5;

        public string Username { get; set; }
        public string IpAddress { get; set; }
        public Dictionary<string, IPEndPoint> ChatUsers { get; set; }
        public string History { get; set; }

        /// <summary>
        /// Инициализация окна
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            context = SynchronizationContext.Current;
            ChatUsers = new Dictionary<string, IPEndPoint>();
            tbMessage.IsEnabled = false;
        }

        #region Получение IP-адреса пользователя и Broadcast
        /// <summary>
        /// Получение локального IP-адреса пользователя
        /// </summary>
        private void GetIpAddress()
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

        private string GetBroadcastAddress(string localIP)
        {
            string temp = localIP.Substring(0, localIP.LastIndexOf(".") + 1);
            return temp + "255";
        }
        #endregion

        #region Отправка и получение UDP-пакетов
        /// <summary>
        /// Отправление UDP-пакета всем пользователям
        /// </summary>
        private void SendFirstNotification()
        {
            const int LOCAL_PORT = 8501;
            const int REMOTE_PORT = 8502;

            try
            {
                IPEndPoint sourceEndPoint = new IPEndPoint(IPAddress.Parse(IpAddress), LOCAL_PORT);
                UdpClient udpSender = new UdpClient(sourceEndPoint);
                IPEndPoint destEndPoint = new IPEndPoint(IPAddress.Parse(GetBroadcastAddress(IpAddress)), REMOTE_PORT);
                udpSender.EnableBroadcast = true;

                UdpMessage udpMessage = new UdpMessage(IpAddress, Username);
                byte[] messageBytes = udpMessage.ToBytes();
                udpSender.Send(messageBytes, messageBytes.Length, destEndPoint);
                udpSender.Close();
                udpSender.Dispose();

                string messageChat = "Вы успешно подключились!\r\n";
                tbChat.AppendText(messageChat);
                string datetime = DateTime.Now.ToString();
                History += string.Format("{0} {1} присоединился к чату\r\n", datetime, Username);
            }
            catch
            {
                throw new Exception("Не удалось отправить уведомление о новом пользователе.");
            }
        }

        /// <summary>
        /// Приём UDP-пакетов от новых пользователей
        /// </summary>
        private void ListenUdpMessages()
        {
            const int LOCAL_UDP_PORT = 8502;
            const int REMOTE_TCP_PORT = 8503;

            aliveUdpTask = true;

            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(IpAddress), LOCAL_UDP_PORT);
            udpReceiver = new UdpClient(localEndPoint);
            while (aliveUdpTask)
            {
                try
                {
                    IPEndPoint remoteEndPoint = null;
                    byte[] message = udpReceiver.Receive(ref remoteEndPoint);

                    UdpMessage receiveMessage = new UdpMessage(message);
                    context.Post(delegate (object state)
                    {
                        string datetime = DateTime.Now.ToString();
                        string messageChat = string.Format("{0} {1} присоединился к чату\r\n", datetime, receiveMessage.Username);
                        tbChat.AppendText(messageChat);
                    }, null);

                    Socket tcpSenderSocket;
                    // Устанавливаем подключение с новым пользователем
                    IPEndPoint connectionEndPoint = new IPEndPoint(IPAddress.Parse(receiveMessage.Ip), REMOTE_TCP_PORT);
                    tcpSenderSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    ChatUsers.Add(receiveMessage.Username, connectionEndPoint);
                    tcpSenderSocket.Connect(connectionEndPoint);

                    // Отправляем новому пользователю своё имя
                    TcpMessage tcpMessage = new TcpMessage(IpAddress, Username);
                    tcpSenderSocket.Send(tcpMessage.ToBytes());
                    tcpSenderSocket.Shutdown(SocketShutdown.Both);
                    tcpSenderSocket.Close();
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }
        #endregion

        #region Отправка и получение TCP-пакетов
        /// <summary>
        /// Прослушивание TCP-пакетов
        /// </summary>
        private void ListenTcpMessages()
        {
            const int TCP_PORT = 8503;
            aliveTcpTask = true;
            bool firstUser = true;

            try
            {
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(IpAddress), TCP_PORT);
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
                        switch (tcpMessage.Code)
                        {
                            case CONNECT:
                                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse(tcpMessage.Ip), TCP_PORT);
                                ChatUsers.Add(tcpMessage.Username, remoteEndPoint);
                                if (firstUser)
                                {
                                    try
                                    {
                                        TcpMessage tcpHistoryMessage = new TcpMessage(4, Username, "History");
                                        listener.Send(tcpHistoryMessage.ToBytes());
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
                                break;
                            case MESSAGE:
                                ShowMessage(tcpMessage);
                                break;
                            case EXIT:
                                ShowUserExit(tcpMessage);
                                break;
                            case SEND_HISTORY:
                                try
                                {
                                    Socket tcpHistorySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                                    tcpHistorySocket.Connect(ChatUsers[tcpMessage.Username]);
                                    TcpMessage tcpHistoryMessage = new TcpMessage(5, Username, History);
                                    tcpHistorySocket.Send(tcpHistoryMessage.ToBytes());
                                    tcpHistorySocket.Shutdown(SocketShutdown.Both);
                                    tcpHistorySocket.Close();
                                }
                                catch { }
                                break;
                            case SHOW_HISTORY:
                                ShowHistory(tcpMessage);
                                break;
                            default:
                                break;
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

        private void ListenUser()
        {
            const int TCP_PORT = 8503;
        }

        /// <summary>
        /// Отправка TCP-пакетов
        /// </summary>
        private void SendTcpMessage()
        {
            string userMessage = tbMessage.Text;
            TcpMessage tcpMessage = new TcpMessage(2, Username, userMessage);
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
        #endregion

        #region Методы вывода различных сообщений
        /// <summary>
        /// Вывод принятого сообщения
        /// </summary>
        /// <param name="message">TCP-сообщение</param>
        private void ShowMessage(TcpMessage message)
        {
            if (ChatUsers != null)
            {
                context.Post(delegate (object state)
                {
                    string datetime = DateTime.Now.ToString();
                    string messageChat = string.Format("{0} {1}: {2}\r\n", datetime, message.Username, message.MessageText);
                    History += messageChat;
                    tbChat.AppendText(messageChat);
                }, null);
            }
        }

        /// <summary>
        /// Вывод сообщения о выходе пользователя из чата
        /// </summary>
        /// <param name="message">TCP-сообщение</param>
        private void ShowUserExit(TcpMessage message)
        {
            if (ChatUsers != null)
            {
                ChatUsers.Remove(message.Username);
                context.Post(delegate (object state)
                {
                    string datetime = DateTime.Now.ToString();
                    string messageChat = string.Format("{0} {1}\r\n", datetime, message.MessageText);
                    History += messageChat;
                    tbChat.AppendText(messageChat);
                }, null);
            }
        }

        /// <summary>
        /// Вывод истории
        /// </summary>
        /// <param name="message">TCP-сообщение</param>
        private void ShowHistory(TcpMessage message)
        {
            if (message.MessageText != "")
            {
                context.Post(delegate (object state)
                {
                    History = message.MessageText + History;
                    tbChat.Text = message.MessageText + tbChat.Text;
                }, null);
            }
        }
        #endregion

        /// <summary>
        /// Нажтие на кнопку "Подключиться"
        /// </summary>
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

        /// <summary>
        /// Нажтие на кнопку "Отправить"
        /// </summary>
        private void ButtonSend_Click(object sender, RoutedEventArgs e)
        {
            SendTcpMessage();
        }

        /// <summary>
        /// Выход из чата
        /// </summary>
        private void ExitChat()
        {
            aliveUdpTask = false;
            aliveTcpTask = false;

            if (udpReceiver != null)
            {
                udpReceiver.Close();
                udpReceiver.Dispose();
            }

            if (ChatUsers != null)
            {
                foreach (string username in ChatUsers.Keys)
                {
                    try
                    {
                        Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        tcpSocket.Connect(ChatUsers[username]);
                        string datetime = DateTime.Now.ToString();
                        string message = string.Format("{0} покинул чат", Username);
                        string exit = string.Format("{0} {1}\r\n", datetime, message);

                        TcpMessage tcpMessage = new TcpMessage(3, Username, message);
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

        /// <summary>
        /// Закрытие окна
        /// </summary>
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (aliveUdpTask && aliveTcpTask)
                ExitChat();
        }
    }
}