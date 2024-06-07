using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TCPServerDemo
{
    // 委托
    delegate void AddUserInfoDel(string userInfo, bool isRemove);
    delegate void AddReceiveInfoDel(string receiveInfo);
    public partial class Form1 : Form
    {
        // 工具
        public static byte[] AB(byte[] data, int beginIndex) => new byte[] { data[beginIndex + 1], data[beginIndex] };
        public static byte[] ABCD(byte[] data, int beginIndex) => new byte[] { data[beginIndex + 3], data[beginIndex + 2], data[beginIndex + 1], data[beginIndex] };
        public static byte[] ABCDEFGH(byte[] data, int beginIndex) => new byte[] { data[beginIndex + 7], data[beginIndex + 6], data[beginIndex + 5], data[beginIndex + 4], data[beginIndex + 3], data[beginIndex + 2], data[beginIndex + 1], data[beginIndex] };


        public Form1()
        {
            InitializeComponent();

            myUserInfoDel += AddUserInfoFunction;
            myReceiveInfoDel += AddReceiveInfoFunction;

            //this.txtIP.Text = "127.0.0.1";
            //this.txtIP.Text = "192.168.0.99";
            this.txtIP.Text = GetLocalIPv4Address().ToString();
            this.txtPort.Text = "9001";
        }

        AddUserInfoDel myUserInfoDel;
        AddReceiveInfoDel myReceiveInfoDel;
        Socket socketListen; // 用于监听和接收客户端请求的套接字
        Thread threadListen; // 用于监听和接收客户端请求的线程
        Dictionary<string, Socket> dictSocket = new Dictionary<string, Socket>();
        Dictionary<string, Thread> dictThread = new Dictionary<string, Thread>();

        private void Form1_Load(object sender, EventArgs e)
        {
            //执行新线程时跨线程资源访问检查会提示报错，所以这里关闭检测（关了之后就不需要委托了，但不稳定）
            // Control.CheckForIllegalCrossThreadCalls = false;    
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStartService_Click(object sender, EventArgs e)
        {
            socketListen = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Invoke(myReceiveInfoDel, "服务启动");
            // 禁止重复操作按钮
            btnStartService.Enabled = false;

            //1.绑定IP和Port
            IPAddress address = IPAddress.Parse(this.txtIP.Text.Trim());
            int port = int.Parse(this.txtPort.Text.Trim());
            IPEndPoint endPoint = new IPEndPoint(address, port);

            try
            {
                //2.使用Bind()进行绑定
                socketListen.Bind(endPoint);
                //3.开启监听（排队等待连接的最大数量为10）
                socketListen.Listen(10);

                /*
                     * tip：
                     * Accept会阻碍主线程的运行，一直在等待客户端的请求，
                     * 客户端如果不接入，它就会一直在这里等着，主线程卡死
                     * 所以开启一个新线程接收客户单请求
                     */
                threadListen = new Thread(ListenConnect); // 线程绑定Listen函数
                threadListen.IsBackground = true; //运行线程在后台执行
                threadListen.Start(); //Start里面的参数是Listen函数所需要的参数，这里传送的是用于通信的Socket对象
            }
            catch (Exception)
            {
                MessageBox.Show("启动失败");
                btnStartService.Enabled = true;
            }
        }

        /// <summary>
        /// 关闭服务
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCloseService_Click(object sender, EventArgs e)
        {
            // 关闭所有通信线程和套接字
            foreach (string item in dictSocket.Keys)
            {
                dictThread[item].Abort();
                dictSocket[item].Close();
            }

            // 关闭监听线程和套接字
            socketListen.Close();
            threadListen.Abort();

            btnStartService.Enabled = true;
            Invoke(myReceiveInfoDel, "服务关闭");
        }

        /// <summary>
        /// 监听客户端的连接
        /// </summary>
        private void ListenConnect()
        {
            while (true)
            {
                //4.阻塞到有client连接，返回新的套接字，创建用于通信的socket
                Socket socketCommunication = socketListen.Accept();

                // 获取远程终结点
                string endPoint = socketCommunication.RemoteEndPoint.ToString();
                // 将每个连接都保存到集合中
                dictSocket.Add(endPoint, socketCommunication);
                Invoke(myUserInfoDel, endPoint, false);

                // 创建用于通信的线程（为每个客户分配一个线程）
                Thread threadCommunication = new Thread(ReceiveMsg);
                threadCommunication.IsBackground = true;
                threadCommunication.Start(socketCommunication);
                // 将每个进程也都保存到集合中
                dictThread.Add(endPoint, threadCommunication);
            }
        }

        /// <summary>
        /// 接收客户端发来的消息
        /// </summary>
        /// <param name="obj"></param>
        private void ReceiveMsg(object obj)
        {
            // 上面传递的用于通信的socket
            Socket socket = obj as Socket;

            while (true)
            {
                // 5.接收数据
                // 创建一个内存缓冲区，其大小为1024*1024*5个字节，即5M
                //byte[] arrReceive = new byte[1024 * 1024 * 5]; // 5M
                byte[] receiveBytes = new byte[1024 * 8]; // 8KB
                int length = -1;
                string endPoint = socket.RemoteEndPoint.ToString();

                try
                {
                    // 将接收到信息存入到内存缓冲区，并返回其字节数组的长度
                    length = socket.Receive(receiveBytes);
                    if (length == 0)
                    {
                        // 消息附加到文本框上
                        Invoke(myReceiveInfoDel, endPoint + " 下线了");
                        Invoke(myUserInfoDel, endPoint, true);
                        // 移除连接
                        dictSocket.Remove(endPoint);

                        // 跳出循环
                        break;
                    }
                    else
                    {
                        // 6.打印数据
                        // 将字节数组转换为可以读懂的字符串
                        //string str = Encoding.UTF8.GetString(arrReceive, 0, length);
                        // 实际接收的字节
                        var recieveBytesReal = receiveBytes.Take(length).ToArray();
                        string hexString = ToHexStringFromByte(recieveBytesReal);
                        string msg = GetCurrentTime() + " [接收] " + endPoint + Environment.NewLine + ToStringFromHexString(hexString);
                        // 消息附加到文本框上
                        Invoke(myReceiveInfoDel, msg);

                        // 处理数据
                        handleReceiveData(recieveBytesReal);
                    }
                }
                catch (Exception e)
                {
                    Invoke(myReceiveInfoDel, endPoint + " 下线了");
                    Invoke(myUserInfoDel, endPoint, true);
                    dictSocket.Remove(endPoint);

                    // 跳出循环
                    break;
                }
            }
        }

        /// <summary>
        /// 附加信息到用户列表
        /// </summary>
        /// <param name="strInfo"></param>
        /// <param name="isRemove"></param>
        private void AddUserInfoFunction(string strInfo, bool isRemove)
        {
            if (isRemove == true)
            {
                this.lbxUserInfo.Items.Remove(strInfo);
            }
            else
            {
                this.lbxUserInfo.Items.Add(strInfo);
            }
        }

        /// <summary>
        /// 附加信息到文本框
        /// </summary>
        /// <param name="receiveInfo"></param>
        private void AddReceiveInfoFunction(string receiveInfo)
        {
            this.txtReceiveInfo.AppendText(receiveInfo + Environment.NewLine);
        }

        /// <summary>
        /// 单发消息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSendToSingle_Click(object sender, EventArgs e)
        {
            string textSend = this.txtSend.Text;
            // 将要发送的消息转换为字节数组
            // byte[] arrText = Encoding.UTF8.GetBytes(textSend);
            byte[] byteDatas = ToBytesFromHexString(textSend);

            if (this.lbxUserInfo.SelectedItems.Count == 0)
            {
                MessageBox.Show("请选择要发送的客户端");
                return;
            }
            else
            {
                foreach (string item in this.lbxUserInfo.SelectedItems)
                {
                    // 调用Send方法像客户端发送消息
                    dictSocket[item].Send(byteDatas);
                    string str = GetCurrentTime() + " [发送] " + item + Environment.NewLine + textSend;

                    // 附加消息到文本框上
                    Invoke(myReceiveInfoDel, str);
                }

                // 清除发送框
                this.txtSend.Clear();
            }
        }

        /// <summary>
        /// 群发消息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSendToAll_Click(object sender, EventArgs e)
        {
            string textSend = this.txtSend.Text;
            byte[] arrText = Encoding.UTF8.GetBytes(textSend);

            foreach (string item in dictSocket.Keys)
            {
                // 调用Send方法像客户端发送消息
                dictSocket[item].Send(arrText);
                string str = GetCurrentTime() + " [发送] " + item + Environment.NewLine + textSend;

                // 附加消息到文本框上
                Invoke(myReceiveInfoDel, str);
            }

            // 清除发送框
            this.txtSend.Clear();
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
        /// 字节数组转16进制字符串：空格分隔
        /// </summary>
        /// <param name="byteDatas"></param>
        /// <returns></returns>
        public static string ToHexStringFromByte(byte[] byteDatas)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < byteDatas.Length; i++)
            {
                builder.Append(string.Format("{0:X2} ", byteDatas[i]));
            }
            return builder.ToString().Trim();
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

        /// <summary>
        /// 普通字符串转16进制格式字符串
        /// </summary>
        /// <param name="plainString"></param>
        /// <returns></returns>
        public static string ToHexString(string plainString, string encode = "utf-8")
        {
            // 常见可选编码 utf-8、utf-16、unicode、ascii、big5、gb2312、gbk、shift_jis、iso - 8859 - 1，可用繁体“龘da”测试不同编码呈现效果
            byte[] byteDatas = Encoding.GetEncoding(encode).GetBytes(plainString);
            string hexString = BitConverter.ToString(byteDatas).Replace("-", " ");
            return hexString;
        }

        /// <summary>
        /// 16进制字符串转普通字符串
        /// </summary>
        /// <param name="hexString"></param>
        /// <param name="encode"></param>
        /// <returns></returns>
        public static string ToStringFromHexString(string hexString, string encode = "utf-8")
        {
            // 常见可选编码 utf-8、utf-16、unicode、ascii、big5、gb2312、gbk、shift_jis、iso - 8859 - 1，可用繁体“龘da”测试不同编码呈现效果
            byte[] _bytes = ToBytesFromHexString(hexString);
            return Encoding.GetEncoding(encode).GetString(_bytes);
        }

        /// <summary>
        /// 发送命令：获取“供温”
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCommand1_Click(object sender, EventArgs e)
        {
            // AA 设备地址 | 03 功能码 | 0A 00 起始寄存器地址 | 00 01 查询一个寄存器 | BD D3 CRC 循环冗余校验
            string hexString = "AA 03 00 0A 00 01 BD D3";
            this.txtSend.Text = hexString;

            // 触发单发消息button事件
            this.btnSendToSingle_Click(sender, e);

            // 返回示例 46 53 56 31 33 37 30 32 30 31 31 36 AA 03 02 08 DC 9B C5
        }

        private void handleReceiveData(byte[] bytes) {
            // 心跳包 heartbeat
            if(bytes.Length == 12)
            {

            }
            // 获取“供温”
            else if (bytes.Length == 19)
            {
                Dictionary<string, string> parsedDict = DataParsing(bytes);
                if (parsedDict != null)
                {
                    // 展示解析后的消息
                    string parsedMsg = DataParsingMsg(parsedDict);
                    // 消息附加到文本框上
                    Invoke(myReceiveInfoDel, parsedMsg);
                }
            }
        }

        /// <summary>
        /// 数据解析
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private Dictionary<string, string> DataParsing(byte[] bytes)
        {
            // 表阀一体返回的结构：普通文本 + ModbusRTU协议内容
            // 即 46 53 56 31 33 37 30 32 30 31 31 36 + AA 03 02 08 AF DA 20
            // 即 FSV137020116 + AA 03 02 08 AF DA 20
            // AA 设备地址 | 03 功能码 | 02 后面被读取数据的字节数 | 08 AF 表示寄存器的值 | DA 20 CRC循环冗余校验
            // 02 后面被读取数据的字节数：1个寄存器有2个字节，所以后面的字节数肯定是 2*查询的寄存器个数
            // 返回的数据：08 AF 是16进制的4个字符，共2个字节。在协议里定义的数据长度为1，即1个寄存器，2个字节，2*8=16位二进制
            // 此bytes正确的长度：12模块号 + 1设备地址 + 1功能码 + 1后面被读取数据的字节数 + 2返回的数据长度（2*查询的寄存器个数） + 2CRC校验 = 12固定+3固定+(1*2)+2固定 = 19个字节

            Dictionary<string, string> dict = new Dictionary<string, string>();

            // 4G模块号
            string moduleNumber = "";

            // 长度为12，表示是心跳
            if (bytes.Length == 12)
            {
                moduleNumber = Encoding.ASCII.GetString(bytes);
                dict.Add("moduleNumber", moduleNumber);
                return dict;
            }

            // 长度大于12，表示有应答数据

            // 获取前12位表示的4G模块号
            moduleNumber = Encoding.ASCII.GetString(bytes.Take(12).ToArray());
            dict.Add("moduleNumber", moduleNumber);

            // 获取后面的Modbus协议内容 
            byte[] contentBytes = bytes.Skip(12).Take(bytes.Length - 12).ToArray();

            // 判断长度
            // 获取供温
            if (contentBytes.Length == 3+(1*2)+2 ) // 7
            {
                // 协议对此寄存器定义和返回的数据长度是1，即两个字节（默认读取顺序从高位往低位，需要按照实际情况对照检查并更改顺序）
                // 下标从0开始，位置需要+固定开头3个字节（设备地址+功能码+后面被读取数据的字节数），所以数据初始下标：3+（协议表格里当前选择范围的第几行-1）*2，且一定是奇数
                byte[] gongwenBytes = new byte[] { contentBytes[4], contentBytes[3] };
                // 转换成无符号整数， 2个字节用ToUInt16（先各字节转换为16进制，然后倒序组合，转换为10进制，175 8 → AF 08 → 08 AF → 2223）
                // 4个字节用ToUInt32，8个字节用ToUInt64
                ushort gongwenUshort = BitConverter.ToUInt16(gongwenBytes, 0);
                // 协议定义：实际值需要除以100
                string gongwen = (gongwenUshort / 100d).ToString();
                dict.Add("gongwen", gongwen);
            } 
            // 获取前48个参数
            else if(contentBytes.Length == 3+(48*2)+2) // 101
            {
                // 下标3+(1-1)*2=3
                dict.Add("gliuliang", (BitConverter.ToUInt32(ABCD(contentBytes, 3), 0) / 1000d).ToString());
                // 下标3+(2-1)*2=7
                dict.Add("liusu", (BitConverter.ToUInt32(ABCD(contentBytes, 7), 0) / 10000d).ToString());
                // 下标3+(3-1)*2=11
                dict.Add("gleiji", (BitConverter.ToUInt64(ABCDEFGH(contentBytes, 11), 0) / 10000d).ToString());
                // 下标3+(9-1)*2=19
                dict.Add("shunshiRe", (BitConverter.ToUInt32(ABCD(contentBytes, 19), 0) / 1000d).ToString());
                // 下标3+(11-1)*2=23
                dict.Add("gongwen", (BitConverter.ToUInt16(AB(contentBytes, 23), 0) / 100d).ToString());
                // 下标3+(12-1)*2=25
                dict.Add("huiwen", (BitConverter.ToUInt16(AB(contentBytes, 25), 0) / 100d).ToString());
                // 下标3+(13-1)*2=27
                dict.Add("leiji", (BitConverter.ToUInt64(ABCDEFGH(contentBytes, 27), 0) / 1000d).ToString());
                // 下标3+(17-1)*2=35
                dict.Add("gonghan", (BitConverter.ToUInt32(ABCD(contentBytes, 35), 0) / 1d).ToString());
                // 下标3+(19-1)*2=39
                dict.Add("huihan", (BitConverter.ToUInt32(ABCD(contentBytes, 39), 0) / 1d).ToString());
                // 下标3+(21-1)*2=43
                dict.Add("gongya", (BitConverter.ToUInt32(ABCD(contentBytes, 43), 0) / 1000d).ToString());
                // 下标3+(23-1)*2=47
                dict.Add("huiya", (BitConverter.ToUInt32(ABCD(contentBytes, 47), 0) / 1000d).ToString());
                // 下标3+(25-1)*2=51
                dict.Add("uptime", (BitConverter.ToUInt32(ABCD(contentBytes, 51), 0) / 1d).ToString());
                // 下标3+(27-1)*2=55
                dict.Add("downtime", (BitConverter.ToUInt32(ABCD(contentBytes, 55), 0) / 1d).ToString());
                // 下标3+(29-1)*2=59
                dict.Add("shicha", (BitConverter.ToUInt32(ABCD(contentBytes, 59), 0) / 1d).ToString());
                // 下标3+(31-1)*2=63
                dict.Add("uppulsewidth", (BitConverter.ToUInt16(AB(contentBytes, 63), 0) / 1d).ToString());
                // 下标3+(32-1)*2=65
                dict.Add("downpulsewidth", (BitConverter.ToUInt16(AB(contentBytes, 65), 0) / 1d).ToString());
                // 下标3+(33-1)*2=67
                dict.Add("upsignal", (BitConverter.ToUInt16(AB(contentBytes, 67), 0) / 100d).ToString());
                // 下标3+(34-1)*2=69
                dict.Add("downsignal", (BitConverter.ToUInt16(AB(contentBytes, 69), 0) / 100d).ToString());
                // 下标3+(35-1)*2=71
                dict.Add("sdtime1", (BitConverter.ToUInt32(ABCD(contentBytes, 71), 0) / 1).ToString());
                // 下标3+(37-1)*2=75
                dict.Add("ddtime1", (BitConverter.ToUInt32(ABCD(contentBytes, 75), 0) / 1).ToString());
                // 下标3+(39-1)*2=79
                dict.Add("sdtime2", (BitConverter.ToUInt32(ABCD(contentBytes, 79), 0) / 1).ToString());
                // 下标3+(41-1)*2=83
                dict.Add("ddtime2", (BitConverter.ToUInt32(ABCD(contentBytes, 83), 0) / 1).ToString());
                // 下标3+(43-1)*2=87
                dict.Add("ddhour", (BitConverter.ToUInt32(ABCD(contentBytes, 87), 0) / 1).ToString());
                // 下标3+(45-1)*2=91
                dict.Add("encoder", (BitConverter.ToUInt16(AB(contentBytes, 91), 0) / 1d).ToString());
                // 下标3+(46-1)*2=93
                dict.Add("batteryV", (BitConverter.ToUInt16(AB(contentBytes, 93), 0) / 100d).ToString());
                // 下标3+(47-1)*2=95
                dict.Add("pulsePerS", (BitConverter.ToUInt16(AB(contentBytes, 95), 0) / 1d).ToString());
                // 下标3+(48-1)*2=97
                dict.Add("velueOpeningDegree", (BitConverter.ToUInt16(AB(contentBytes, 97), 0) / 10d).ToString());
            }

            return dict;
        }

        /// <summary>
        /// 组装解析后的消息（临时展示用）
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        private string DataParsingMsg(Dictionary<string, string> dict)
        {
            string msg = "";

            foreach (var item in dict.Keys)
            {
                msg += item + "：" + dict[item] + "； ";
            }

            return msg;
        }

        /// <summary>
        /// 发送命令：获取“前48个”
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCommand2_Click(object sender, EventArgs e)
        {
            // AA 设备地址 | 03 功能码 | 00 00 起始寄存器地址 | 00 30 查询48个寄存器 | 5C 05 CRC 循环冗余校验
            string hexString = "AA 03 00 00 00 30 5C 05";
            this.txtSend.Text = hexString;

            // 触发单发消息button事件
            this.btnSendToSingle_Click(sender, e);

            // 返回示例 46 53 56 31 33 37 30 32 30 31 31 36 AA 03 02 08 DC 9B C5
        }
    }
}
