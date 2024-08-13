using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TCPClientDemo
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            //this.txtIP.Text = "127.0.0.1";
            this.txtIP.Text = GetLocalIPv4Address().ToString();
            this.txtPort.Text = "9001";
            this.txtName.Text = "Test";
        }

        Socket socketCommunication; // 用于通信的socket
        Thread threadCommunication; // 用于通信的线程

        /// <summary>
        /// 连接服务器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStartConnect_Click(object sender, EventArgs e)
        {
            socketCommunication = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPAddress address = IPAddress.Parse(this.txtIP.Text.Trim());
            IPEndPoint iPEndPoint = new IPEndPoint(address, int.Parse(this.txtPort.Text.Trim()));
            this.txtReceive.AppendText("正在与服务器连接中..." + Environment.NewLine);

            try
            {
                socketCommunication.Connect(iPEndPoint);
                this.txtReceive.AppendText("连接成功！" + Environment.NewLine);

                threadCommunication = new Thread(ReceiveMsg);
                threadCommunication.IsBackground = true;
                threadCommunication.Start();
            }
            catch (Exception ex)
            {
                this.txtReceive.AppendText("连接失败！" + Environment.NewLine);
                MessageBox.Show("连接失败！");
            }
        }

        /// <summary>
        /// 接收服务端消息
        /// </summary>
        private void ReceiveMsg()
        {
            while (true)
            {
                byte[] arrMsgReceive = new byte[1024 * 1024 * 5];
                int length = -1;
                string endPoint = socketCommunication.RemoteEndPoint.ToString();

                try
                {
                    length = socketCommunication.Receive(arrMsgReceive);
                }
                catch (SocketException e)
                {
                    BeginInvoke(new Action(() => this.txtReceive.AppendText(GetCurrentTime() + " SocketException：" + e.ToString() + Environment.NewLine)));
                    closeConnect();
                    break;
                }
                catch (Exception ee)
                {
                    BeginInvoke(new Action(() => this.txtReceive.AppendText(GetCurrentTime() + " Exception：" + ee.ToString() + Environment.NewLine)));
                    closeConnect();
                    break;
                }

                if (length > 0)
                {
                    string str = Encoding.UTF8.GetString(arrMsgReceive, 0, length);
                    string msg = GetCurrentTime() + " [接收] " + endPoint + Environment.NewLine + str;
                    BeginInvoke(new Action(() => this.txtReceive.AppendText(msg + Environment.NewLine)));
                }
                else
                {
                    BeginInvoke(new Action(() => this.txtReceive.AppendText(GetCurrentTime() + " 服务器端 " + endPoint + " 正常下线 " + Environment.NewLine)));
                    closeConnect();
                    break;
                }
            }
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        private void closeConnect()
        {
            try
            {
                if (socketCommunication != null && socketCommunication.Connected)
                {
                    // 关闭通信线程和套接字
                    socketCommunication.Shutdown(SocketShutdown.Both);
                    socketCommunication.Close(60);
                    threadCommunication.Abort();
                }
            }
            catch (Exception e)
            {
                // throw;
            }
        }

        /// <summary>
        /// 向服务端发送消息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSend_Click(object sender, EventArgs e)
        {
            try
            {
                if (!socketCommunication.Connected)
                {
                    MessageBox.Show("还未连接服务端");
                    return;
                }

                //string strMsg = "来自" + txtName.Text + ":" + txtSend.Text;
                //byte[] arrMsg = Encoding.ASCII.GetBytes(strMsg);
                byte[] arrMsg = ToBytesFromHexString(txtSend.Text);
                string endPoint = socketCommunication.RemoteEndPoint.ToString();

                socketCommunication.Send(arrMsg);
                BeginInvoke(new Action(() => txtReceive.AppendText(GetCurrentTime() + " [发送] " + endPoint + Environment.NewLine + txtSend.Text + Environment.NewLine)));
            }
            catch (Exception ex)
            {
                // throw;
            }
        }

        /// <summary>
        /// 获取当前系统时间
        /// </summary>
        /// <returns></returns>
        private string GetCurrentTime()
        {
            return DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");
        }

        /// <summary>
        /// 获取本地IPv4地址
        /// </summary>
        /// <returns></returns>
        public IPAddress GetLocalIPv4Address()
        {
            IPAddress localIPv4 = null;
            // 获取本机的所有IP地址列表
            IPAddress[] IPList = Dns.GetHostAddresses(Dns.GetHostName());
            // 循环遍历所有IP地址
            foreach (IPAddress ip in IPList)
            {
                // 判断是否是IPv4地址
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIPv4 = ip;
                }
                else
                {
                    continue;
                }
            }

            return localIPv4;
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCloseConnect_Click(object sender, EventArgs e)
        {
            try
            {
                if (socketCommunication != null && !socketCommunication.Connected)
                {
                    MessageBox.Show("还未连接服务端");
                    return;
                }

                // socketCommunication.Close();
                socketCommunication.Shutdown(SocketShutdown.Both);
                socketCommunication.Close(60);
                // .net core 已废弃abort，这样写会报错，https://learn.microsoft.com/zh-cn/dotnet/core/compatibility/core-libraries/5.0/thread-abort-obsolete
                threadCommunication.Abort();

                BeginInvoke(new Action(() => txtReceive.AppendText(GetCurrentTime() + " 连接关闭！" + Environment.NewLine)));
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() => txtReceive.AppendText(GetCurrentTime() + " 关闭出现异常！" + Environment.NewLine)));
            }
        }

        /// <summary>
        /// 16进制格式字符串转字节数组
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        public static byte[] ToBytesFromHexString(string hexString)
        {
            // 以 ' ' 分割字符串，并去掉空字符串
            string[] chars = hexString.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            byte[] returnBytes = new byte[chars.Length];
            // 逐个字符变为16字节数据
            for (int i = 0; i < chars.Length; i++)
            {
                returnBytes[i] = Convert.ToByte(chars[i], 16);
            }
            return returnBytes;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            closeConnect();
        }
    }
}
