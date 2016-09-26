using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
//using System.Drawing;//Rectangle 사용을 위해 
using System.Runtime.InteropServices; // Marshal를 쓰기위해
using DirectShowLib; // 다이렉트쇼 사용
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;


//using System.Runtime.InteropServices;


namespace Windows_Form_Observer
{
    public partial class Form1 : Form
    {
        //이벤트를 사용
        IGraphBuilder pGraphBuilder = null;
        IMediaControl pMediaControl = null;
        IMediaEvent pMediaEvent = null;
        EventCode eventCode;
        IMediaSeeking pMediaSeeking = null;
        //윈도우 선언
        IVideoWindow pVideoWindow = null;
        IMediaPosition pMediaPosition =null;

        // 영상 history 를 위해
        OpenFileDialog OpenFileDialog1 = new OpenFileDialog();
        String Filename1;
        Thread threadServer;
        Thread threadrecv;
        int port = 8000;
        Socket server;
        Socket client;
        double mapx;
        double mapy;
        double Length=0;


        //Image button_image = Resources.button;
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class DataPacket
        {
            public DataPacket()
            {
                this.header = 0;
                this.CAM = 0;
                this.MAN = 0;
                this.RET = 0;
                this.Lat = 0;
                this.Lng = 0;
            }
            public byte header; // 초기 0 cam 1  Man 2  Ret 3  GPS 4
            public byte CAM;  // CAMERA
            public byte MAN; // MANUAL
            public byte RET;  // RETURN
           //Destination GPS
            
                public double Lat = 0;
                public double Lng = 0;
            
            // 수동제어도 넣어야함
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class DataPacket2 {
            public DataPacket2()
            {
                this.ARR =0;
                this.DLat =0;
                this.DLng =0;
                this.header2 = 0;
                this.RETURN = 0;
                this.WAR = 0;
            }
            public byte header2; // 초기 0 war 1 return 2 arr 3 GPS 4 드론 종료 5
            public byte WAR;    // 베터리 부족
            public byte RETURN; // 복귀 지점 도착
            public byte ARR;    // 화재 지역 근처
            //Drone GPS
                public double DLat ;
                public double DLng ;
            // 복귀지점 도착 , 배터리 부족 , GPS 근처 도착
        }
        DataPacket Packet = new DataPacket();
        DataPacket2 Packet2 = new DataPacket2();

        public Form1()
        {
            InitializeComponent();
            
            // ****************** 전체 화면 *******************//
            if ((this.FormBorderStyle == System.Windows.Forms.FormBorderStyle.None) && (this.WindowState == FormWindowState.Maximized))
            {
                // Form 상태 변경  
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
                this.WindowState = FormWindowState.Normal;
            }

            // 현재 Full-Screen 모드가 아닐 경우 처리  
            else
            {
                // Form 상태 변경  
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
            }
            // ****************** 전체 화면 *******************//
            
            pictureBox1.Image = Bitmap.FromFile("Drone.png");
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
         
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
            try
            {
                string url = Environment.CurrentDirectory + "\\daumMapAPI.html";
                webBrowser1.Navigate(url);
                this.webBrowser1.ObjectForScripting = this;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            server.Bind(new IPEndPoint(IPAddress.Any, port));
            server.Listen(1);
            
            threadServer = new Thread(new ThreadStart(Connect));

            threadServer.Start();
            while (!threadServer.IsAlive) ;
            Thread.Sleep(1);
            Packet2.DLat = 37.30024;
            Packet2.DLng = 127.03997;
            Packet.Lat = 37.29861;
            Packet.Lng = 127.03961;
        }
        public void Connect() {
            while (true)
            {
                try
                {
                    client = server.Accept();
                    string sLog = String.Format("▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼ 드론이 연결 되었습니다 ▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼", new StackTrace(true).GetFrame(1).GetFileName());
                    SaveLogFile(sLog);
                    break;
                }
                catch(Exception ex) {
                    string sLog = String.Format("▽▽▽▽▽▽▽▽▽▽▽ 드론 연결이 실패하였습니다. ▽▽▽▽▽▽▽▽▽▽▽", new StackTrace(true).GetFrame(1).GetFileName());
                }
            }
            threadrecv = new Thread(new ThreadStart(Recv));
            threadrecv.Start(); 
            threadServer.Interrupt();
        }

        public void disconnect()
        {
            client.Close();
            threadServer = new Thread(new ThreadStart(Connect));
            threadServer.Start();
            threadrecv.Abort();
        } // 드론과 연결 해제 .. 연결 대기 상태
        public void Send() {
            try
            {
                byte[] bsPacket = StructureToByte(Packet); // 구조체 -> BYTE
                client.Send(bsPacket, 0, 20, 0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        } // 보낸다 !

        public void Recv() {
            while (true){
                int datasize = Marshal.SizeOf(Packet2);
                byte[] brPacket = new byte[datasize];
                try
                {
                    client.Receive(brPacket, 0, 20, 0); //Marshal.SizeOf(Packet2)
                }
                catch {
                    string sLog = String.Format("▽▽▽▽▽▽▽▽▽▽▽ 드론 연결이 비정상 종료 되었습니다. ▽▽▽▽▽▽▽▽▽▽▽", new StackTrace(true).GetFrame(1).GetFileName());
                    SaveLogFile(sLog);
                    disconnect();
                }
                
                Packet2 = (DataPacket2)ByteToStructure(brPacket,typeof(DataPacket2)); //문제 가능성 있음
                
                if (Packet2.header2 == 1)
                { // WAR (베터리)
                    MessageBox.Show("드론의 베터리가 " + Packet2.WAR + "% 남았습니다.");
                    string sLog = String.Format("Recv : Warning Message  Packet2.WAR :" + Packet2.WAR + " Packet2.header2 :" + Packet2.header2 + "", new StackTrace(true).GetFrame(1).GetFileName());
                    SaveLogFile(sLog);
                }
                else if (Packet2.header2 == 2)
                { // RETURN (복귀지점 도착)
                  // if (Packet2.RETURN == 1)
                  // {
                    MessageBox.Show("드론이 복귀지점에 도달했습니다.");
                    string sLog = String.Format("Recv : Return Message  Packet2.RETURN :" + Packet2.RETURN + " Packet2.header2 :" + Packet2.header2 + "", new StackTrace(true).GetFrame(1).GetFileName());
                    SaveLogFile(sLog);
                    //  }
                }
                else if (Packet2.header2 == 3)
                { // ARR (화재 지역 근처 도착)
                  //  if (Packet2.ARR == 1)
                  // {
                    MessageBox.Show("드론이 화재지역에 도달했습니다.");
                    string sLog = String.Format("Recv : Arrive Message  Packet2.ARR :" + Packet2.ARR + " Packet2.header2 :" + Packet2.header2 + "", new StackTrace(true).GetFrame(1).GetFileName());
                    SaveLogFile(sLog);
                    //  }
                }
                else if (Packet2.header2 == 4)
                {
                    sendPosition2Browser(Packet2.DLat, Packet2.DLng);
                    string sLog = String.Format("Recv : Drone GPS  Packet2.DLat :" + Packet2.DLat + " Packet2.DLng : " + Packet2.DLng + " Packet2.header2 :" + Packet2.header2 + "", new StackTrace(true).GetFrame(1).GetFileName());
                    SaveLogFile(sLog);
                }
                else if (Packet2.header2 == 5) {
                    string sLog = String.Format("▽▽▽▽▽▽▽▽▽▽▽ 드론 연결이 정상 해제되었습니다.. ▽▽▽▽▽▽▽▽▽▽▽", new StackTrace(true).GetFrame(1).GetFileName());
                    SaveLogFile(sLog);
                    disconnect();
                }
            }
        }// 받는다 !

        public static byte[] StructureToByte(object obj)
        {
            int datasize = Marshal.SizeOf(obj);             // 구조체에 할당된 메모리의 크기를 구한다.
            IntPtr buff = Marshal.AllocHGlobal(datasize);   // 비관리 메모리 영역에 구조체 크기만큼의 메모리를 할당한다.
            Marshal.StructureToPtr(obj, buff, false);       // 할당된 구조체 객체의 주소를 구한다.
            byte[] data = new byte[datasize];               // 구조체가 복사될 배열
            Marshal.Copy(buff, data, 0, datasize);          // 구조체 객체를 배열에 복사
            Marshal.FreeHGlobal(buff);                      // 비관리 메모리 영역에 할당했던 메모리를 해제함
            return data; // 배열을 리턴
        } // 구조체 -> BYTE

        public static object ByteToStructure(byte[] data, Type type)
        {
            IntPtr buff = Marshal.AllocHGlobal(data.Length);    // 배열의 크기만큼 비관리 메모리 영역에 메모리를 할당한다.
            Marshal.Copy(data, 0, buff, data.Length);           // 배열에 저장된 데이터를 위에서 할당한 메모리 영역에 복사한다.
            object obj = Marshal.PtrToStructure(buff, type);    // 복사된 데이터를 구조체 객체로 변환한다.
            Marshal.FreeHGlobal(buff);                          // 비관리 메모리 영역에 할당했던 메모리를 해제함
            if (Marshal.SizeOf(obj) != data.Length)             // (((PACKET_DATA)obj).TotalBytes != data.Length) // 구조체와 원래의 데이터의 크기 비교
            {
                return null; // 크기가 다르면 null 리턴
            }
            return obj; // 구조체 리턴
        }//BYTE -> 구조체

        public void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            {
                string msg = e.Url + " 로딩 완료!";
                MessageBox.Show(msg);
            }
        } // 지도 api 잘 불러 왔는지

       

        private void button1_Click(object sender, EventArgs e) 
        {
            OpenFileDialog1.Filter = "Video FILE | *.wmv;*.avi;*.mp4";

            if (OpenFileDialog1.ShowDialog() == DialogResult.OK)
            {
                Filename1 = OpenFileDialog1.FileName;
                test();
                string sLog = String.Format("Play: Video", new StackTrace(true).GetFrame(1).GetFileName());
                SaveLogFile(sLog);
            }
            else
            {
                MessageBox.Show("파일이 선택되지 않았습니다.");
            }
        } // 영상 파일 찾기
        int Min;
        private void SetupGraph(Control hWin, string filename) 
        {
            if (pGraphBuilder == null)
            {
                //새로운 필터그래프 대응
                pGraphBuilder = (IGraphBuilder)new FilterGraph();
                //필터그래프에 콘트롤 붙이기
                pMediaControl = (IMediaControl)pGraphBuilder;
                //윈도우 붙이기
                pVideoWindow = (IVideoWindow)pGraphBuilder;
                pMediaPosition = (IMediaPosition)pGraphBuilder; //추가
                                                                //콘트롤에 동영상 읽어오기
                pMediaControl.RenderFile(filename);
                //패넬에서 재생하기
                pVideoWindow.put_Owner(hWin.Handle);
                pVideoWindow.put_WindowStyle(WindowStyle.Child | WindowStyle.ClipSiblings);
                Rectangle rect = panel2.ClientRectangle;
                pVideoWindow.SetWindowPosition(0, 0, rect.Right, rect.Bottom);
                pMediaPosition.get_Duration(out Length);
                
                 
                if (Length > 60)
                {
                    Min = (int)Length / 60;
                }
              
                 double Sec =  Length - (Min * 60);
                 String Time;
                 Time = Min + "분" + (int)Sec +"초";
                textBox3.Text = Time;
                Min = 0;
            }
            else {
                string sLog = String.Format("실행되고있는 영상을 먼저 정지해주세요.", new StackTrace(true).GetFrame(1).GetFileName());
                SaveLogFile(sLog);
            }

        } // 영상 setup

        void test()
        { // 가지고 있는 영상

            SetupGraph(panel2, Filename1);
            //읽어온 동영상 플레이 하기
            pMediaControl.Run();
            
        }   //영상 실행 SetupGraph 과 함께

        private void panel2_Paint(object sender, PaintEventArgs e)
        {

        } // 영상 재생을 위한 판넬

        private void button2_Click(object sender, EventArgs e) 
        {
            try
            {
                //그래프와 콘트롤 해제
                if (pMediaControl != null)
                {
                    pMediaControl.StopWhenReady();
                }
                if (pVideoWindow != null)
                {
                    pVideoWindow.put_Visible(OABool.False);
                    pVideoWindow.put_Owner(IntPtr.Zero);
                }
                Marshal.ReleaseComObject(pGraphBuilder);
                pGraphBuilder = null;
                Marshal.ReleaseComObject(pMediaControl);
                pMediaControl = null;
                Marshal.ReleaseComObject(pVideoWindow);
                pVideoWindow = null;
                Marshal.ReleaseComObject(pMediaPosition); //추가
                pMediaPosition = null;
                string sLog = String.Format("Play : Video Stop", new StackTrace(true).GetFrame(1).GetFileName());
                SaveLogFile(sLog);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        } // 영상 정지 버튼

        private void button3_Click(object sender, EventArgs e) 
        {
            try
            {
                pMediaControl.Pause();
                string sLog = String.Format("Play : Video Pause", new StackTrace(true).GetFrame(1).GetFileName());
                SaveLogFile(sLog);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        } // 영상 일시정지 버튼
        
        

        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                
                pMediaControl.Run();
                string sLog = String.Format("Play : Video Run", new StackTrace(true).GetFrame(1).GetFileName());
                SaveLogFile(sLog);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }// 영상 재생

        private void button4_Click(object sender, EventArgs e)
        {
            DialogResult dialogResult = MessageBox.Show("종료하시겠습니까?", "Observer", MessageBoxButtons.YesNo);
            if (dialogResult == DialogResult.Yes)
            {
                server.Close();
                threadServer.Abort();
                
                Application.Exit();
            }
            else if (dialogResult == DialogResult.No)
            {
            }
        } // 종료 버튼
                
        private void button6_Click(object sender, EventArgs e)
        {
            Packet.header = 1;
            Packet.CAM = 1;
            Send();
            string sLog = String.Format("Send : Cam On  Packet.Cam :"+ Packet.CAM + " Packet.header :"+ Packet.header + "", new StackTrace(true).GetFrame(1).GetFileName());
            SaveLogFile(sLog);
        } // 카메라 ON 버튼

        private void button7_Click(object sender, EventArgs e)
        {
            Packet.header = 1;
            Packet.CAM = 0;
            Send();
            string sLog = String.Format("Send : Cam Off  Packet.Cam :" + Packet.CAM + " Packet.header :" + Packet.header + "", new StackTrace(true).GetFrame(1).GetFileName());
            SaveLogFile(sLog);
        }// 카메라 OFF 버튼

        private void button8_Click(object sender, EventArgs e)
        {
            Packet.header = 2;
            Packet.MAN = 1;
            Send();
            string sLog = String.Format("Send : Manual On  Packet.Man :" + Packet.MAN + " Packet.header :" + Packet.header + "", new StackTrace(true).GetFrame(1).GetFileName());
            SaveLogFile(sLog);
        }// 수동제어 ON 버튼

        private void button9_Click(object sender, EventArgs e)
        {
            Packet.header = 2;
            Packet.MAN = 0;
            Send();
            string sLog = String.Format("Send : Manual Off  Packet.Man :" + Packet.MAN + " Packet.header :" + Packet.header + "", new StackTrace(true).GetFrame(1).GetFileName());
            SaveLogFile(sLog);
        }// 수동제어 OFF 버튼

        private void button10_Click(object sender, EventArgs e)
        {
            Packet.header = 3;
            Packet.RET = 1;
            Send();
            string sLog = String.Format("Send : Return  Packet.Ret :" + Packet.RET + " Packet.header :" + Packet.header + "", new StackTrace(true).GetFrame(1).GetFileName());
            SaveLogFile(sLog);
        }// RETURN 버튼

        private void button11_Click(object sender, EventArgs e)
        {
            Packet.header = 3;
            Packet.RET = 0;
            Send();
            string sLog = String.Format("Send : Return  Packet.Ret :" + Packet.RET + " Packet.header :" + Packet.header + "", new StackTrace(true).GetFrame(1).GetFileName());
            SaveLogFile(sLog);
        }// RETURN 취소 버튼

        private void button12_Click(object sender, EventArgs e)
        {
            try
            {
                Packet.Lat = double.Parse(textBox1.Text);
                Packet.Lng = double.Parse(textBox2.Text);
                this.webBrowser1.Document.InvokeScript("DGetPoint", new object[] { Packet.Lat, Packet.Lng }); //DESTINATION 설정
                Packet.header = 4;
                Send();
                string sLog = String.Format("Send : Destination GPS  Packet.Lat :" + Packet.Lat +" Packet.Lng : "+ Packet.Lng+" Packet.header :" + Packet.header + "", new StackTrace(true).GetFrame(1).GetFileName());
                SaveLogFile(sLog);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }// 목적지 GPS 설정

        private void textBox1_TextChanged(object sender, EventArgs e) 
        {
            Regex emailregex = new Regex(@"[0-9]");
            Boolean ismatch = emailregex.IsMatch(textBox1.Text);
            if (!ismatch)
            {
                MessageBox.Show("숫자만 입력해 주세요.");
            }
        } // 목적지 x축

        private void textBox1_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            if (!(Char.IsDigit(e.KeyChar)) && e.KeyChar != 8)
            {
                e.Handled = true;
            }
        } // 숫자만

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            Regex emailregex = new Regex(@"[0-9]");
            Boolean ismatch = emailregex.IsMatch(textBox1.Text);
            if (!ismatch)
            {
                MessageBox.Show("숫자만 입력해 주세요.");
            }
        } // 목적지 y 축

        public void CallForm(double x, double y) {
            mapx = x;
            mapy = y;
        }

        private void button13_Click(object sender, EventArgs e)
        {
            this.webBrowser1.Document.InvokeScript("DGetPoint", new object[] {mapx, mapy});
            Packet.Lat = mapx;
            Packet.Lng = mapy;
            Packet.header = 4;
            Send();
            string sLog = String.Format("Play : Destination GPS  Packet.Lat :" + Packet.Lat + " Packet.Lng : " + Packet.Lng + " Packet.header :" + Packet.header + "", new StackTrace(true).GetFrame(1).GetFileName());
            SaveLogFile(sLog);
        }

        private void sendPosition2Browser(double X, double Y )
        {
            try
            {
                this.Invoke(new MethodInvoker(
                    delegate ()
                    {
                        webBrowser1.Document.InvokeScript("GetPoint", new object[] { X, Y });
                    }));
            }
            catch
            {

            }

            
        }

        public void SaveLogFile(string inLogMessage) {
            string strDate;
            string temp;
            string LogMessage = "";
            string strTime = "";
            GetSystemTime(out strTime);

            // 입력받은 문자에 날짜와 시간을 붙여서 출력
            LogMessage = string.Format(strTime.ToString() + inLogMessage.ToString());
            listBox1.Items.Add(LogMessage);

            GetSystemDate(out strDate);

            // 리스트박스는 맨 마지막줄을 항상 선택
            int index = listBox1.Items.Count;
            listBox1.SelectedIndex = index - 1;

            // 리스트박스 버퍼가 1000줄이 넘으면 가장오래된 로그를 한줄 지운다
            if (listBox1.Items.Count > 1000)
            {
                listBox1.Items.RemoveAt(0);
            }
            
            // 로그 데이터가 저장될 폴더와 파일명 설정
            string FilePath = string.Format("C:\\Observer Log" + "\\" + strDate + "_Log.txt");
            FileInfo fi = new FileInfo(FilePath);
            // 폴더가 존재하는지 확인하고 존재하지 않으면 폴더부터 생성
            DirectoryInfo dir = new DirectoryInfo("C:\\Observer Log");
           
            if (dir.Exists == false)
            {
                // 새로 생성합니다.
                dir.Create();
            }        
            // 기존 로그 데이터가 존재시 이어쓰고 아니면 새로 생성
            try
            {
                if (fi.Exists != true)
                {
                    using (StreamWriter sw = new StreamWriter(FilePath))
                    {
                        temp = string.Format("[{0}] : {1}", GetDateTime(), inLogMessage);
                        sw.WriteLine(temp);
                        sw.Close();
                    }
                }
                else
                {
                    using (StreamWriter sw = File.AppendText(FilePath))
                    {
                        temp = string.Format("[{0}] : {1}", GetDateTime(), inLogMessage);
                        sw.WriteLine(temp);
                        sw.Close();
                    }
                }
            }            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            
        }// Log 띄우고 저장

        public void GetSystemTime(out string outTime)
        {
            outTime = string.Format("[" + DateTime.Now.ToString("yyyy.MM.dd") + "_" + DateTime.Now.ToString("HH:mm:ss") + "] ");
        }// 프로그램 날짜 및 시간 얻어오기

        public void GetSystemDate(out string outTime)
        {
            outTime = string.Format(DateTime.Now.ToString("yyyy.MM.dd"));
        }// 프로그램 날자 얻어오기

        public string GetDateTime()
        {
            DateTime NowDate = DateTime.Now;
            return NowDate.ToString("yyyy-MM-dd HH:mm:ss") + ":" + NowDate.Millisecond.ToString("000");
        }//수정필요

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }//log 띄우는 listbox


        private void trackBar1_Scroll_1(object sender, EventArgs e)
        {
            double W;
            
            W = (double)(Length * ((double)trackBar1.Value / (double)10));
            
            string sLog1 = String.Format("" + W + "", new StackTrace(true).GetFrame(1).GetFileName());
            SaveLogFile(sLog1);
            try
            {
                this.pMediaPosition.put_CurrentPosition(W);
            }
            catch {
                string sLog = String.Format(" Error", new StackTrace(true).GetFrame(1).GetFileName());
                SaveLogFile(sLog);
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            
        }//picture box

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
        }
    }
}
