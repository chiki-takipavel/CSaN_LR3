using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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
        private volatile bool aliveUdpTask;
        private volatile bool aliveTcpTask;
        private const int CONNECT = 1;
        private const int MESSAGE = 2;
        private const int EXIT_USER = 3;
        private const int SEND_HISTORY = 4;
        private const int SHOW_HISTORY = 5;
        private object synlock = new object();

        public string Username { get; set; }
        public string IpAddress { get; set; }
        public List<ChatUser> ChatUsers { get; set; }
        public string History { get; set; }

        /// <summary>
        /// Инициализация окна
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            context = SynchronizationContext.Current;
            ChatUsers = new List<ChatUser>();
            tbMessage.IsEnabled = false;
        }

        #region Получение IP-адреса пользователя и Broadcast адреса
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
            const int REMOTE_UDP_PORT = 8501;
            const int LOCAL_UDP_PORT = 8502;
            const int TCP_PORT = 8503;

            aliveUdpTask = true;

            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(IpAddress), LOCAL_UDP_PORT);
            UdpClient udpReceiver = new UdpClient(localEndPoint);
            while (aliveUdpTask)
            {
                try
                {
                    IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, REMOTE_UDP_PORT);
                    byte[] message = udpReceiver.Receive(ref remoteEndPoint);
                    UdpMessage receiveMessage = new UdpMessage(message);

                    // Устанавливаем подключение с новым пользователем
                    ChatUser newUser = new ChatUser(receiveMessage.Username, receiveMessage.Ip, TCP_PORT);
                    newUser.Connect();

                    // Отправляем новому пользователю своё имя
                    TcpMessage tcpMessage = new TcpMessage(CONNECT, IpAddress, Username);
                    newUser.SendMessage(tcpMessage);
                    ChatUsers.Add(newUser);

                    Task.Factory.StartNew(() => ListenUser(newUser));

                    context.Post(delegate (object state)
                    {
                        string datetime = DateTime.Now.ToString();
                        string messageChat = string.Format("{0} {1} присоединился к чату\r\n", datetime, newUser.Username);
                        tbChat.AppendText(messageChat);
                    }, null);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            udpReceiver.Dispose();
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

            try
            {
                TcpListener tcpListener = new TcpListener(IPAddress.Parse(IpAddress), TCP_PORT);
                tcpListener.Start();
                while (aliveTcpTask)
                {
                    if (tcpListener.Pending())
                    {
                        ChatUser newUser = new ChatUser(tcpListener.AcceptTcpClient(), TCP_PORT);
                        Task.Factory.StartNew(() => ListenUser(newUser));
                    }
                }
                tcpListener.Stop();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Прослушивание пользователя чата
        /// </summary>
        /// <param name="user">Пользователь чата</param>
        private void ListenUser(ChatUser user)
        {
            bool firstUser = true;

            while (user.IsOnline)
            {
                try
                {
                    if (user.Stream.DataAvailable)
                    {
                        byte[] message = user.RecieveMessage();
                        TcpMessage tcpMessage = new TcpMessage(message);
                        int code = tcpMessage.Code;
                        switch (code)
                        {
                            case CONNECT:
                                user.Ip = tcpMessage.Ip;
                                user.Username = tcpMessage.Username;
                                ChatUsers.Add(user);
                                if (firstUser)
                                {
                                    GetHistory(SEND_HISTORY, user);
                                    firstUser = false;
                                }
                                break;
                            case MESSAGE:
                                ShowInChat(code, user.Username, tcpMessage.MessageText);
                                break;
                            case EXIT_USER:
                                user.Dispose();
                                ChatUsers.Remove(user);
                                ShowInChat(code, user.Username, tcpMessage.MessageText);
                                break;
                            case SEND_HISTORY:
                                GetHistory(SHOW_HISTORY, user);
                                break;
                            case SHOW_HISTORY:
                                ShowInChat(code, user.Username, tcpMessage.MessageText);
                                break;
                            default:
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Отправка TCP-пакетов
        /// </summary>
        private void SendTcpMessage()
        {
            string userMessage = tbMessage.Text;
            TcpMessage tcpMessage = new TcpMessage(MESSAGE, userMessage);
            foreach (ChatUser user in ChatUsers)
            {
                try
                {
                    user.SendMessage(tcpMessage);
                }
                catch
                {
                    MessageBox.Show(string.Format("Не удалось отправить сообщение пользователю {0}.", user.Username));
                }
            }

            string datetime = DateTime.Now.ToString();
            string messageChat = string.Format("{0} Вы: {1}\r\n", datetime, userMessage);
            tbChat.AppendText(messageChat);
            tbMessage.Clear();
            History += string.Format("{0} {1}: {2}\r\n", datetime, Username, userMessage);
        }
        #endregion

        #region Вывод принятой информации в чат пользователя
        /// <summary>
        /// Вывод информации в чат (сообщения пользователей, сообщения о выходе из чата,
        /// история пользователя из чата)
        /// </summary>
        /// <param name="code">Код сообщения</param>
        /// <param name="username">Имя пользователя, от кого пришло сообщение</param>
        /// <param name="message">Текст принятого сообщения</param>
        private void ShowInChat(int code, string username, string message)
        {
            string datetime = DateTime.Now.ToString();
            switch (code)
            {
                case MESSAGE:
                    context.Post(delegate (object state)
                    {
                        string messageChat = string.Format("{0} {1}: {2}\r\n", datetime, username, message);
                        History += messageChat;
                        tbChat.AppendText(messageChat);
                    }, null);
                    break;
                case EXIT_USER:
                    context.Post(delegate (object state)
                    {
                        string messageChat = string.Format("{0} {1}\r\n", datetime, message);
                        History += messageChat;
                        tbChat.AppendText(messageChat);
                    }, null);
                    break;
                case SHOW_HISTORY:
                    if (message != "")
                    {
                        context.Post(delegate (object state)
                        {
                            History = message + History;
                            tbChat.Text = message + tbChat.Text;
                        }, null);
                    }
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region Обработка событий формы
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
        /// Закрытие окна
        /// </summary>
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (aliveUdpTask && aliveTcpTask)
                ExitChat();
        }
        #endregion

        #region Дополнительные методы (выход из чата, запрос истории)
        /// <summary>
        /// Запрос истории
        /// </summary>
        /// <param name="code">Код, отвечающий за запрос истории или её отправку</param>
        /// <param name="user">Пользователь в чате, который отправляет или получает историю</param>
        private void GetHistory(int code, ChatUser user)
        {
            try
            {
                TcpMessage tcpHistoryMessage;
                if (code == SEND_HISTORY)
                {
                    tcpHistoryMessage = new TcpMessage(code, "History");
                }
                else // SHOW_HISTORY
                {
                    tcpHistoryMessage = new TcpMessage(code, History);
                }
                user.SendMessage(tcpHistoryMessage);
            }
            catch { }
        }

        /// <summary>
        /// Выход из чата
        /// </summary>
        private void ExitChat()
        {
            aliveUdpTask = false;
            aliveTcpTask = false;

            string datetime = DateTime.Now.ToString();
            string message = string.Format("{0} покинул чат", Username);
            string exit = string.Format("{0} {1}\r\n", datetime, message);
            tbChat.AppendText(exit);

            lock (synlock)
            {
                TcpMessage tcpMessage = new TcpMessage(EXIT_USER, message);
                foreach (ChatUser user in ChatUsers)
                {
                    try
                    {
                        user.SendMessage(tcpMessage);
                        user.Dispose();
                    }
                    catch
                    {
                        MessageBox.Show("Ошибка отправки уведомления о выходе из чата.");
                    }
                }
                ChatUsers.Clear();
            }
        }
        #endregion
    }
}