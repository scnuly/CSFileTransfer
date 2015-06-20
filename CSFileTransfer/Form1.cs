using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace CSFileTransfer
{ 
    public partial class FileTransfer : Form
    {
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        static extern void mouse_event(CSFileTransfer.FileTransfer.MouseEventFlag flags, int dx, int dy, uint data, UIntPtr extraInfo);


        [Flags]
        enum MouseEventFlag : uint
        {
            Move = 0x0001,
            LeftDown = 0x0002,
            LeftUp = 0x0004,
            RightDown = 0x0008,
            RightUp = 0x0010,
            MiddleDown = 0x0020,
            MiddleUp = 0x0040,
            XDown = 0x0080,
            XUp = 0x0100,
            Wheel = 0x0800,
            VirtualDesk = 0x4000,
            Absolute = 0x8000
        }

        TcpListener serverListener;
        private Boolean quiteServer = false;
        private List<Socket> clientSockets;

        private string fileSaveName;
        private static AutoResetEvent syncEvent;

        public const int ENDPOINT = 7879;
        public const int PACKAGE_LEN = 512;

        public const uint NETWORK_TOKEN_FILE_TRANSFER    = 0xfffe0001;
        public const uint NETWORK_TOKEN_PAINT_MODE       = 0xfffd0002;
        public const uint NETWORK_TOKEN_MOUSE_CONTROL    = 0xfffc0003;

        public const string BUTTON_FILE_SAVE = "BUTTON_FILE_SAVE";
        public const string PROGRESS_BAR_SAVE = "PROGRESS_BAR_SAVE";
        public const string PROGRESS_BAR_POS = "PROGRESS_BAR_POS";
        public const string INTEM_ENABLE = "INTEM_ENABLE";
        public const string INTEM_VISABLE = "INTEM_VISABLE";
        public const string INTEM_TEXT = "INTEM_TEXT";
        public const string FILE_SAVED_NOTICE = "FILE_SAVED_NOTICE";
        public const string PAINT_MODE_DOWN = "PAINT_MODE_DOWN";
        public const string PAINT_MODE_MOVE = "PAINT_MODE_MOVE";
        public const string PAINT_MODE_UP = "PAINT_MODE_UP";
        public const string PAINT_MODE_CLEAR = "PAINT_MODE_CLEAR";
        public const string LABLE_NAME = "LABLE_NAME";

        int countsocket = 0;
        int countdraw = 0;

        public delegate void UiInvoke(string para1, string para2, string para3, int para4, int para5);
        public UiInvoke ui;
        Bitmap originBmp;
        Point DownPoint;
        Pen p;

        public FileTransfer()
        {
            InitializeComponent();
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.form1_FormClosing);
            syncEvent = new AutoResetEvent(false);
            ui = new UiInvoke(updateUI);
            progressBarFile.Minimum = 0;
            progressBarFile.Maximum = 100;
            progressBarFile.Value = 0;
            DownPoint = new Point(0, 0);
            p = new Pen(Color.Blue, 3);
            p.StartCap = LineCap.Round;
            p.EndCap = LineCap.Round;
            p.LineJoin = LineJoin.Round;

            clientSockets = new List<Socket>();

            originBmp = new Bitmap(this.pictureBox1.Width, this.pictureBox1.Height, PixelFormat.Format32bppRgb);
            Graphics g = Graphics.FromImage(originBmp);
            g.Clear(BackColor);
            g.Dispose();

            //start the server listening thread to keep listen incoming client.
            Thread server = new Thread(new ThreadStart(serverStartListening));
            server.Start();
        }
        
        private void drawPathDown(Point e)
        {
            DownPoint.X = e.X;
            DownPoint.Y = e.Y;
            countdraw++;
        }

        private void drawPathMove(Point e)
        {
            //Point newPoint = new Point(DownPoint.X, DownPoint.Y);
            //Graphics g = Graphics.FromImage(originBmp);
            //g.DrawLine(p, DownPoint, e);
            //DownPoint.X = e.X;
            //DownPoint.Y = e.Y;
            Graphics g1 = this.pictureBox1.CreateGraphics();
            g1.DrawLine(p, DownPoint, e);
            DownPoint.X = e.X;
            DownPoint.Y = e.Y;
            //g1.DrawImage(destBmp, new Point(0, 0));
            g1.Dispose();
            //Invalidate();
            countdraw++;
        }

        public static int findStringLen(byte[] data, int index)
        {
            int i=0, flag=0;
            for (i = index; i < data.Length; i++)
            {
                if (flag == 1 && data[i] == 0)
                    return (i - index);
                if (data[i] == 0)
                    flag = 1;
                else
                    flag = 0;

            }
            return data.Length - index;
        }

        public void updateUI(string param1, string param2, string param3, int param4, int param5)
        {
            if (param1.CompareTo(PAINT_MODE_MOVE) == 0)
            {
                drawPathMove(new Point(param4, param5));
            }
            else if (param1.CompareTo(PAINT_MODE_DOWN) == 0)
            {
                drawPathDown(new Point(param4, param5));
            }
            else if (param1.CompareTo(PAINT_MODE_CLEAR) == 0)
            {
                Graphics g1 = this.pictureBox1.CreateGraphics();
                g1.Clear(BackColor);
                g1.Dispose();
            }
            else if (param1.CompareTo(BUTTON_FILE_SAVE) == 0)
            {
                if (param2.CompareTo(INTEM_TEXT) == 0)
                    buttonfilesave.Text = param3;
                else if (param2.CompareTo(INTEM_ENABLE) == 0)
                    buttonfilesave.Enabled = Convert.ToBoolean(param4);
                else if (param2.CompareTo(INTEM_VISABLE) == 0)
                    buttonfilesave.Visible = Convert.ToBoolean(param4);
            }
            else if (param1.CompareTo(PROGRESS_BAR_SAVE) == 0)
            {
                if (param2.CompareTo(PROGRESS_BAR_POS) == 0)
                    progressBarFile.Value = param4;
                else if (param2.CompareTo(INTEM_ENABLE) == 0)
                    progressBarFile.Enabled = Convert.ToBoolean(param4);
                else if (param2.CompareTo(INTEM_VISABLE) == 0)
                    progressBarFile.Visible = Convert.ToBoolean(param4);
            }
            else if (param1.CompareTo(LABLE_NAME) == 0)
            {
                label1.Text = param3;
            }
            else if (param1.CompareTo(FILE_SAVED_NOTICE) == 0)
            {
                MessageBox.Show(param2);
            }
        }

        private void serverStartListening()
        {
            serverListener = new TcpListener(IPAddress.Any, ENDPOINT);
            serverListener.Start();

            while (true)
            {
                try
                {
                    //start listen socket
                    Socket client = serverListener.AcceptSocket();
                    if (quiteServer)
                    {
                        client.Close();
                        break;
                    }

                    clientSockets.Add(client);
                    this.Invoke(ui, new Object[] { LABLE_NAME, "", "connected", 1, 0 });
                    Thread handleServer = new Thread(new ParameterizedThreadStart(serverHandle));
                    handleServer.Start(client);
                }
                catch (Exception e)
                {
                    MessageBox.Show("server listening error! " + e.Message);
                }
            }
        }

        private void serverHandle(object obj)
        {
            byte[] d = new byte[PACKAGE_LEN];
            Socket client = (Socket)obj;
            uint token = 0;

            //IPEndPoint client_ip = (IPEndPoint)client.RemoteEndPoint;
            //Stream stream = new NetworkStream(client);
            //StreamReader sr = new StreamReader(stream);
            //StreamWriter sw = new StreamWriter(stream);
            try
            {
                client.Receive(d, PACKAGE_LEN, SocketFlags.None);
            }
            catch (Exception e)
            {
                //MessageBox.Show("client token read failed" + e.Message);
                System.Console.WriteLine(e.Message);
                client.Close();
                clientSockets.Remove(client);
                return;
            }

            token = (uint)(d[0] | d[1] << 8 | d[2] << 16 | d[3] << 24);

            switch (token)
            {
                case NETWORK_TOKEN_FILE_TRANSFER:
                    int count = 0;
                    int fileLength = (int)(d[4] | d[5] << 8 | d[6] << 16 | d[7] << 24);
                    string fileName = Encoding.UTF8.GetString(d, 8, findStringLen(d, 8));
                    
                    this.Invoke(ui, new Object[] { BUTTON_FILE_SAVE, INTEM_TEXT, fileName, 0, 0 });
                    this.Invoke(ui, new Object[] { BUTTON_FILE_SAVE, INTEM_VISABLE, "", 1, 0 });
                    this.Invoke(ui, new Object[] { PROGRESS_BAR_SAVE, PROGRESS_BAR_POS, "", 0, 0 });
                    this.Invoke(ui, new Object[] { PROGRESS_BAR_SAVE, INTEM_VISABLE, "", 1, 0 });

                    //wait for file open
                    syncEvent.WaitOne();
                    
                    //file write
                    FileStream sFile = new FileStream(fileSaveName, FileMode.Create);
                    BinaryWriter bw = new BinaryWriter(sFile);
                    //tell the client start to sent data.
                    d[0] = 0x1; d[1] = 0x0; d[2] = 0x0; d[3] = 0x0;
                    client.Send(d, 4, SocketFlags.None);

                    try
                    {
                        int left = fileLength;
                        while (left > 0)
                        {
                            int rd = client.Receive(d, PACKAGE_LEN, SocketFlags.None);
                            bw.Write(d, 0, rd);
                            left -= rd;
                            count += rd;
                            this.Invoke(ui, new Object[] { PROGRESS_BAR_SAVE, PROGRESS_BAR_POS, "", (int)(count*100 / fileLength), 0 });
                        }
                    }
                    catch (Exception e)
                    {
                        //MessageBox.Show("client token read failed" + e.Message);
                        System.Console.WriteLine(e.Message);
                    }
                    bw.Flush();
                    bw.Close();
                    sFile.Close();
                    this.Invoke(ui, new Object[] { BUTTON_FILE_SAVE, INTEM_VISABLE, "", 0, 0 });
                    this.Invoke(ui, new Object[] { PROGRESS_BAR_SAVE, INTEM_VISABLE, "", 0, 0 });
                    this.Invoke(ui, new Object[] { FILE_SAVED_NOTICE, "File saved successfull!", "", 0, 0 });
                    break;

                case NETWORK_TOKEN_PAINT_MODE:
                    //MessageBox.Show("paint mode on");
                    while (!quiteServer)
                    {
                        try
                        {
                            client.Receive(d, 4, SocketFlags.None);
                            System.Console.WriteLine("got message " + countsocket);
                            if (0xff == (uint)(d[3]))
                            {
                                this.Invoke(ui, new Object[] { PAINT_MODE_CLEAR, "", "", 0, 0 });
                                break;
                            }
                            else if (0xfe == (uint)(d[3]))
                            {
                                this.Invoke(ui, new Object[] { PAINT_MODE_CLEAR, "", "", 0, 0 });
                                continue;
                            }
                            int x = (int)(d[0] | d[1] << 8);
                            int y = (int)(d[2] | (d[3] & 0x7f) << 8);
                            uint type = (uint)(d[3] & 0x80);

                            countsocket++;
                            if (type == 0x80)
                                this.Invoke(ui, new Object[] { PAINT_MODE_DOWN, "", "", x / 4, y / 4 });
                            else if (type == 0x0)
                                this.Invoke(ui, new Object[] { PAINT_MODE_MOVE, "", "", x / 4, y / 4 });                                
                        }catch(Exception e)
                        {
                            //MessageBox.Show("disconnect" + e.Message);
                            System.Console.WriteLine(e.Message);
                        }
                        
                    }
                    break;

                case NETWORK_TOKEN_MOUSE_CONTROL:
                    //MessageBox.Show("mouse mode on");
                    while (!quiteServer)
                    {
                        try
                        {
                            client.Receive(d, 4, SocketFlags.None);
                            if (0xff == (uint)(d[3]))
                                break;
                            else if (0xfe == (uint)(d[3]))
                                continue;
                            
                            int x = (int)(d[0] | d[1] << 8);
                            int y = (int)(d[2] | (d[3] & 0x7f) << 8);
                            uint type = (uint)(d[3] & 0x80);

                            countsocket++;
                            SetCursorPos(x, y);
                        }
                        catch (Exception e)
                        {
                            //MessageBox.Show("disconnect" + e.Message);
                            System.Console.WriteLine(e.Message);
                        }

                    }
                    break;

                default:
                    break;
            };
            this.Invoke(ui, new Object[] { LABLE_NAME, "", "disconnected", 1, 0 });
            client.Close();
            clientSockets.Remove(client);
        }

        private void form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult dr = MessageBox.Show("是否要退出程序", "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr == DialogResult.Yes)
            {
                e.Cancel = false;
                quiteServer = true;
                Socket closeClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint ie = new IPEndPoint(IPAddress.Parse("127.0.0.1"), ENDPOINT);
                try
                {
                    closeClient.Connect(ie);
                    closeClient.Close();
                    clientSockets.ForEach(delegate(Socket client)
                    {
                        if (client != null)
                        {
                            client.Disconnect(false);
                            client.Shutdown(SocketShutdown.Receive);
                            client.Close();
                        }
                    });
                }
                catch (Exception err)
                {
                    MessageBox.Show("close client error! " + err.Message);
                }
            }
            else
            {
                e.Cancel = true;
            }
        }

        private void buttonfilesave_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Text File(*.txt)|*.txt";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                fileSaveName = dlg.FileName;
                syncEvent.Set();
            }     
        }

        private void button1_Click(object sender, EventArgs e)
        {
            drawPathDown(new Point(0, 0));
            drawPathMove(new Point(100, 50));
            drawPathMove(new Point(0, 100));
        }
    }
}
