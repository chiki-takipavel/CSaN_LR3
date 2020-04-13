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
        private SynchronizationContext context;
        private bool alive;

        public string UserName { get; set; }
        public string Ip { get; set; }
        public Dictionary<string, Socket> connections { get; set; }
        public Socket UdpSenderSocket { get; set; }
        public Socket UdpRecieverSocket { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            context = SynchronizationContext.Current;
            btnDisconnect.IsEnabled = false;
            tbMessage.IsEnabled = false;
        }

        private string GetIpAddress()
        {
            string host = Dns.GetHostName();
            IPAddress[] hostAdresses = Dns.GetHostAddresses(host);

            if (hostAdresses[hostAdresses.Length - 1].AddressFamily == AddressFamily.InterNetwork)
                return hostAdresses[hostAdresses.Length - 1].ToString();

            throw new Exception("Не удалось определить IP Address");
        }

        private void SendUdpNotification()
        {
            const int LOCAL_PORT = 8001;
            const int REMOTE_PORT = 8002;
            try
            {
                IPEndPoint sourcePoint = new IPEndPoint(IPAddress.Parse(Ip), LOCAL_PORT);
                UdpSenderSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                UdpSenderSocket.Bind(sourcePoint);
                UdpSenderSocket.EnableBroadcast = true;

                EndPoint destPoint = new IPEndPoint(IPAddress.Broadcast, REMOTE_PORT);
                UdpMessage message = new UdpMessage(UserName, Ip);
                UdpSenderSocket.SendTo(message.ToBytes(), destPoint);
                UdpSenderSocket.Close();

                btnConnect.IsEnabled = false;
                btnDisconnect.IsEnabled = true;
                tbMessage.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ReceiveUdpMessages()
        {
            const int LOCAL_PORT = 5358;
            const int TCP_REMOTE_PORT = 5359;

            IPEndPoint UdpEndPoint = new IPEndPoint(IPAddress.Parse(Ip), LOCAL_PORT);
            Socket UdpReceiverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            UdpReceiverSocket.Bind(UdpEndPoint);

            while (true)
            {
                EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = new byte[256];

                int size = UdpReceiverSocket.ReceiveFrom(data, ref remoteEndPoint);
                var messageBytes = new byte[size];

                for (int i = 0; i < size; i++)
                {
                    messageBytes[i] = data[i];
                }

                UdpMessage recieveMessage = new UdpMessage(messageBytes);

                context.Post(delegate (object state) {
                    string time = DateTime.Now.ToShortTimeString();
                    string message = time + $"{recieveMessage.UserName} добавился к чату\r\n";
                    tbChat.AppendText(message);
                }, null);

                IPEndPoint connectionIpEndPoint = new IPEndPoint(IPAddress.Parse(recieveMessage.Ip), TCP_REMOTE_PORT);
                connections.Add(recieveMessage.UserName, new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
                connections[recieveMessage.UserName].Connect(connectionIpEndPoint);
                connections[recieveMessage.UserName].Send(Encoding.Unicode.GetBytes(UserName));
            }
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            Ip = GetIpAddress();
            UserName = tbUserName.Text;
            tbUserName.IsReadOnly = true;
            SendUdpNotification();
            Task receiveTask = new Task(ReceiveUdpMessages);
            receiveTask.Start();
        }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            //ExitChat();
        }

        private void mainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (alive)
            {
                //ExitChat();
            }
        }

        /*private void ExitChat()
        {
            string message = UserName + " покидает чат";
            byte[] data = Encoding.Unicode.GetBytes(message);
            UdpClient.Send(data, data.Length, HOST, REMOTEPORT);
            UdpClient.DropMulticastGroup(groupAddress);

            alive = false;
            UdpClient.Close();

            btnConnect.IsEnabled = true;
            btnDisconnect.IsEnabled = false;
            tbMessage.IsEnabled = false;
        }*/
    }
}
