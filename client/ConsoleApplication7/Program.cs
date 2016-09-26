using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;

namespace ConsoleApplication7
{
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
        public byte CAM ; // CAMERA
        public byte MAN ; // MANUAL
        public byte RET ; // RETURN
                             //Destination GPS

        public double Lat ;
        public double Lng ;

        // 수동제어도 넣어야함
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DataPacket2
    {
        public DataPacket2() {
            this.header2= 0;
            this.WAR = 0;
            this.ARR = 0;
            this.DLat = 0;
            this.DLng = 0;
            this.RETURN = 0;
        }
        public byte header2; // 초기 0 war 1 return 2 arr 3 GPS 4 드론 종료 5
        public byte WAR;    // 베터리 부족
        public byte RETURN; // 복귀 지점 도착
        public byte ARR;    // 화재 지역 근처
                            //Drone GPS
        public double DLat;
        public double DLng;

        
        // 복귀지점 도착 , 배터리 부족 , GPS 근처 도착
    }
   

    class Program
    {
        
        static int datasize;
        static Socket client;
        static DataPacket Packet = new DataPacket();
        static DataPacket2 Packet2 = new DataPacket2();
        static void Main(string[] args)
        {
            Thread clientrecv;
            Connect();
            Console.WriteLine("Socket connect");
            Worker workerObject = new Worker();
            clientrecv = new Thread(new ThreadStart(workerObject.Recv));
            clientrecv.Start();
            Packet2.DLat = 37.30024;
            Packet2.DLng = 127.03997; // 2공학관 위치 = 드론 위치
            
            while (true) //Send
            {
                int a =Int32.Parse(Console.ReadLine());
                if (a == 0)  ////////////////////이제 여기다가 드론 조건
                {
                    Console.WriteLine("????");
                }
                else if (a == 1)
                {
                    Packet2.header2 = 1;
                    Packet2.WAR = 32;
                    Console.WriteLine("베터리 부족 신호 보냄");
                }
                else if (a == 2)
                {
                    Packet2.header2 = 2;
                    Console.WriteLine("복귀지점 근처도착 신호 보냄");
                }
                else if (a == 3)
                {
                    Packet2.header2 = 3;
                    Console.WriteLine("화재지역 근처 도달 신호 보냄");
                }
                else if (a == 4)
                {
                    Packet2.header2 = 4;
                    Packet2.DLat -= 0.0001;
                    Packet2.DLng -= 0.0001;
                }
                else if (a == 5) {
                    Packet2.header2 = 5;
                    try
                    {
                        Send();
                        client.Close();
                        
                        Thread.Sleep(1);
                        workerObject.ThreadStop();
                        clientrecv.Join();
                        return;
                    }
                    catch (Exception ex){
                        Console.WriteLine(ex.Message);
                    }
                }
                Send();
            }
        }
        public class Worker
        {
            private volatile bool isStop = false;
            
            public void Recv()
            {
                while (!this.isStop) ///////////////////////이제 여기다가 신호 받았을때 드론 알고리즘
                {
                    int datasize = Marshal.SizeOf(Packet);
                    byte[] brPacket = new byte[datasize];
                    try
                    {
                        client.Receive(brPacket, 0, 20, 0); //Marshal.SizeOf(Packet2) 수정필요
                    }
                    catch (Exception ex){ Console.WriteLine(ex.Message); }
                    Packet = (DataPacket)ByteToStructure(brPacket, typeof(DataPacket)); //문제 가능성 있음
                    if (Packet.header == 1)
                    {
                        if (Packet.CAM == 1)
                        {
                            Console.WriteLine("CAM ON");
                        }
                        else
                        {
                            Console.WriteLine("CAM OFF");
                        }
                    }//카메라
                    else if (Packet.header == 2)
                    {
                        if (Packet.MAN == 1)
                        {
                            Console.WriteLine("MANUAL ON");
                        }
                        else
                        {
                            Console.WriteLine("MANUAL OFF");
                        }
                    } //수동제어
                    else if (Packet.header == 3)
                    {
                        if (Packet.RET == 1)
                        {
                            Console.WriteLine("RETURN");
                        }
                        else
                        {
                            Console.WriteLine("RETURN CANCEL");
                        }

                    }// 리턴명령
                    else if (Packet.header == 4)
                    {

                        Console.WriteLine("DESTINATION GPS 신호 받음");
                        Console.WriteLine(Packet.Lat + " " + Packet.Lng);

                    }//gps
                    
                }
            }// 받는다
            public void ThreadStop() {
                this.isStop = true;
            }
        }
        
        public static void Send() {
            byte[] bsPacket = StructureToByte(Packet2); // 구조체 -> BYTE
            client.Send(bsPacket, 0, 20, 0);
        }// 보낸다
        public static byte[] StructureToByte(object obj)
        {
            datasize = Marshal.SizeOf(obj);             // 구조체에 할당된 메모리의 크기를 구한다.
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
        public static void Connect() {
            while (true)
            {
                try
                {
                    IPEndPoint ipep = new IPEndPoint(IPAddress.Parse("10.10.27.43"), 8000);
                    client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    client.Connect(ipep);
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine("다시 연결 하시려면 아무키나 입력하시오.");
                    Console.ReadLine();
                    client.Close();
                }
            }
        }
    }//class program
}
