using Modbus.Device;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zhaoxi.Communication;
using Zhaoxi.Communication.Modbus;

namespace Zhaoxi.CommunicationLib
{
    public class Program
    {
        static int timeout = 1000;
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            //var tt = Encoding.UTF8.GetBytes("Hi");// 2个字节  

            int flag = 13;

            #region NModbus4通信库演示
            if (flag == 1)
            {
                // 
                // RTU  
                SerialPort serialPort = new SerialPort("COM1", 9600, Parity.None, 8, StopBits.One);
                serialPort.Open();
                ModbusMaster master = ModbusSerialMaster.CreateRtu(serialPort);

                // 起始地址    “40001”      "I0.0"    "DB1.DBW100"
                ushort[] values = master.ReadHoldingRegisters(1, 0, 2);

                // 功能    效率

                // float数据   从两个ushort值转换成float
            }
            #endregion


            #region ModbusRTU 读保持型寄存器报文处理
            if (flag == 1)
            {
                List<byte> bytes = new List<byte>();
                bytes.Add(0x01);// 从站地址
                bytes.Add(0x03);// 功能码：读保持型寄存器
                ushort addr = 4;
                // BitConverter      /256    %256
                bytes.Add((byte)(addr / 256));// BitConverter.GetBytes(addr)[1];
                bytes.Add((byte)(addr % 256));// BitConverter.GetBytes(addr)[0];
                ushort len = 2;//寄存器数量 
                bytes.Add((byte)(len / 256));
                bytes.Add((byte)(len % 256));

                bytes = CRC16(bytes);// Modbus

                // 发送  串口
                SerialPort serialPort = new SerialPort("COM1", 9600, Parity.None, 8, StopBits.One);
                serialPort.Open();
                // 协议的发送间隔时间    传输时间
                // 发送
                serialPort.Write(bytes.ToArray(), 0, bytes.Count);//   事件 C#处理从站逻辑使用    延迟
                // 响应
                byte[] data = new byte[len * 2 + 5];// 正常    异常
                serialPort.Read(data, 0, data.Length);// 串口Read不卡线程    网口Scoket卡线程  事件
                // CRC校验检查
                //// 解析   2字节的数据  
                //for (int i = 3; i < data.Length - 2; i += 2)
                //{
                //    // Modbus Slave大端处理
                //    // 100
                //    // 0000 0000     0110 0100
                //    byte[] vb = new byte[2] { data[i + 1], data[i] };
                //    ushort v = BitConverter.ToUInt16(vb);// 解析出无符号短整型数据
                //    Console.WriteLine(v);
                //}
                // float   4个字节     ABCD    40 90 00 00   IEEE754标准   CDAB
                //                                                         BADC  [0]B  [1]A  [2]D  [3]C
                // 如果发现转换的值不对-》优先考虑字节序问题
                // byte[] 设备 
                // short  bitconverter  ->byte[]  100  [0x00] [0x64]    int
                // 自动匹配字节  类型
                for (int i = 3; i < data.Length - 2; i += 4)
                {
                    // 封装库的需要注意字节序  
                    byte[] vb = new byte[4]
                    {
                        data[i + 2],//D
                        data[i+3 ],//C
                        data[i ],//B
                        data[i+1]//A
                    };
                    float v = BitConverter.ToSingle(vb);
                    Console.WriteLine(v);
                }

                // short 123
                // 64+32+16+8+2+1
                // 0111 1011  0000 0000   
            }
            #endregion

            #region ModbusRTU 单写保持型寄存器报文处理   
            if (flag == 2)
            {
                List<byte> bytes = new List<byte>();
                bytes.Add(0x01);// 从站地址
                bytes.Add(0x06);// 功能码：读保持型寄存器
                ushort addr = 3;
                // BitConverter      /256    %256
                bytes.Add((byte)(addr / 256));// BitConverter.GetBytes(addr)[1];
                bytes.Add((byte)(addr % 256));// BitConverter.GetBytes(addr)[0];
                ushort value = 100;// 写入寄存器的值
                //bytes.Add((byte)(value / 256));// 高位
                //bytes.Add((byte)(value % 256));// 低位

                // 两种结果是不一样的
                //byte[] vs = BitConverter.GetBytes(value); // 2byte
                //byte[] vs = BitConverter.GetBytes(100);   // 默认是int   32位   4个字节

                bytes = CRC16(bytes);// Modbus

                SerialPort serialPort = new SerialPort("COM1", 9600, Parity.None, 8, StopBits.One);
                serialPort.Open();
                serialPort.Write(bytes.ToArray(), 0, bytes.Count);
            }
            #endregion

            #region ModbusRTU 多写保持型寄存器报文处理
            if (flag == 3)
            {
                List<byte> datas = new List<byte>();
                datas.Add(0x01);// 从站地址
                datas.Add(0x10);// 16进制0x10   十进制 16

                ushort addr = 16;
                datas.Add((byte)(addr / 256));
                datas.Add((byte)(addr % 256));

                //// 写入多个相关的类型
                //List<ushort> values = new List<ushort>();
                //values.Add(111);  // byte[2]
                //values.Add(222);  // byte[2]
                //values.Add(333);  // byte[2]
                //values.Add(444);  // byte[2]

                //// 写入寄存器数量
                //datas.Add((byte)(values.Count / 256));
                //datas.Add((byte)(values.Count % 256));

                //// 需要写入的数据字节数
                //datas.Add((byte)(values.Count * 2));  // 255

                //for (int i = 0; i < values.Count; i++)
                //{
                //    // 大端
                //    datas.Add(BitConverter.GetBytes(values[i])[1]);// ?
                //    datas.Add(BitConverter.GetBytes(values[i])[0]);// ?
                //}

                // 
                // 写入多个 float  double
                // 从10号寄存器写入2个float
                List<float> values = new List<float>();
                values.Add(1.2f);
                values.Add(2.3f);
                values.Add(4.5f);

                //// 写入寄存器数量
                //datas.Add((byte)(values.Count * 2 / 256));
                //datas.Add((byte)(values.Count * 2 % 256));
                //// 需要写入的数据字节数
                //datas.Add((byte)(values.Count * 4));  // 255

                //for (int i = 0; i < values.Count; i++)
                //{
                //    // DCBA
                //    datas.Add(BitConverter.GetBytes(values[i])[3]);// ?
                //    datas.Add(BitConverter.GetBytes(values[i])[2]);// ?
                //    datas.Add(BitConverter.GetBytes(values[i])[1]);// ?
                //    datas.Add(BitConverter.GetBytes(values[i])[0]);// ?
                //}



                // 写入不确定类型   ushort   float       写入对象封装   类型   值
                List<dynamic> values2 = new List<dynamic>();
                ushort v1 = 123;
                values2.Add(v1);// ushort
                values2.Add(36.5f); // float


                List<byte> temp = new List<byte>();// 6个字节
                for (int i = 0; i < values2.Count; i++)
                {
                    List<byte> dBytes = new List<byte>(BitConverter.GetBytes(values2[i]));
                    dBytes.Reverse();
                    temp.AddRange(dBytes);
                }
                // 写入寄存器数量   
                datas.Add((byte)(temp.Count / 2 / 256));// 取两个字节的高位
                datas.Add((byte)(temp.Count / 2 % 256));// 取两个字节中的低位
                // 需要写入的数据字节数
                datas.Add((byte)(temp.Count));  // 255
                // 拼接数据字节
                datas.AddRange(temp);

                datas = CRC16(datas);// Modbus
                SerialPort serialPort = new SerialPort("COM1", 9600, Parity.None, 8, StopBits.One);
                serialPort.Open();
                serialPort.Write(datas.ToArray(), 0, datas.Count);




            }
            #endregion


            #region ModbusRTU 读线圈状态报文处理
            if (flag == 4)
            {
                List<byte> datas = new List<byte>();
                datas.Add(0x01);
                datas.Add(0x01); //读线圈状态 

                ushort addr = 0;// 起始地址
                datas.Add(0x00);
                datas.Add(0x00);
                ushort len = 10;// 读取寄存器数量 
                datas.Add(0x00);
                datas.Add(0x0A);

                datas = CRC16(datas);// Modbus
                SerialPort serialPort = new SerialPort("COM1", 9600, Parity.None, 8, StopBits.One);
                serialPort.Open();
                // 发送读取请求报文
                serialPort.Write(datas.ToArray(), 0, datas.Count);

                // 接收响应报文 
                byte[] data = new byte[(int)(Math.Ceiling(len * 1.0 / 8) + 5)];// 正常    异常
                serialPort.Read(data, 0, data.Length);
                /// 解析
                List<byte> respBytes = new List<byte>();
                for (int i = 3; i < data.Length && respBytes.Count < Math.Ceiling(len * 1.0 / 8); i++)
                {
                    respBytes.Add(data[i]);// 拿出两个数据字节
                }
                int count = 0;
                for (int i = 0; i < respBytes.Count; i++)
                {
                    // 遍历的数据字节
                    // 字节串
                    // 
                    for (int k = 0; k < 8; k++)
                    {
                        // 遍历每个字节中的每个位
                        byte temp = (byte)(1 << k);
                        Console.WriteLine((respBytes[i] & temp) != 0);
                        // 输入16个状态
                        count++;
                        if (count == len)
                            break;
                    }
                }

            }
            #endregion

            #region 写单线圈状态报文处理
            if (flag == 5)
            {
                List<byte> datas = new List<byte>();
                datas.Add(0x01);
                datas.Add(0x05);

                ushort addr = 11;// 起始地址
                datas.Add((byte)(addr / 256));
                datas.Add((byte)(addr % 256));

                // 写入的值两种类型  
                // on:  0xFF 0x00
                // off: 0x00 0x00
                datas.Add(0x00);// 表示置为off
                datas.Add(0x00);

                datas = CRC16(datas);// Modbus
                SerialPort serialPort = new SerialPort("COM1", 9600, Parity.None, 8, StopBits.One);
                serialPort.Open();
                // 发送读取请求报文
                serialPort.Write(datas.ToArray(), 0, datas.Count);
            }
            #endregion

            #region 写多线圈状态报文处理
            if (flag == 6)
            {
                List<byte> datas = new List<byte>();
                datas.Add(0x01);
                datas.Add(0x0F);// 十进制  15

                ushort addr = 10;// 起始地址
                datas.Add((byte)(addr / 256));
                datas.Add((byte)(addr % 256));
                // 写入数量：写入多个个寄存器

                // 
                List<bool> status = new List<bool>() {
                    true, false, true, true, true, false, true, false, true, true
                };
                // C#设备中已有的状态
                // 逻辑   

                // 写了入寄存器数量
                datas.Add((byte)(status.Count / 256));
                datas.Add((byte)(status.Count % 256));
                // 0101 1101    0x5D
                // 0000 0011    0x03


                // 0001 1101    0x1D

                // 0000 0000  数据初始值

                // 0000 0001   temp   <<0
                // 0000 0001  第一次或运算结果

                // 第二次没有结果   因为为false不用管
                // 0000 0100   temp   <<2
                // 0000 0101  第三次或运算结果
                // 0000 1000   temp   <<3
                // 0000 1101  第四次或运算结果
                // 0001 0000   temp   <<4
                // 0001 1101  第五次或运算结果

                List<byte> vbs = new List<byte>();
                //byte data = 0;
                int index = 0;
                for (int i = 0; i < status.Count; i++)
                {
                    //status.Count  =10   每8个一个字节
                    if (i % 8 == 0)
                        vbs.Add(0x00);// 初始值
                    index = vbs.Count - 1;

                    if (status[i])
                    {
                        // True
                        byte temp = (byte)(1 << (i % 8));
                        vbs[index] |= temp;
                    }
                    else
                    {
                        // False  取反操作

                    }
                }
                datas.Add((byte)vbs.Count);// 写入字节数
                datas.AddRange(vbs);

                datas = CRC16(datas);// Modbus
                SerialPort serialPort = new SerialPort("COM1", 9600, Parity.None, 8, StopBits.One);
                serialPort.Open();
                // 发送读取请求报文
                serialPort.Write(datas.ToArray(), 0, datas.Count);


                // 连续写
            }
            #endregion


            #region Modbus 异常响应
            if (flag == 7)
            {
                List<byte> bytes = new List<byte>();
                bytes.Add(1);// 从站地址
                bytes.Add(0x03);// 功能码     20->14    0x31  0x34      0x32  0x30
                ushort addr = 5;// 起始地址
                bytes.Add((byte)(addr / 256));
                bytes.Add((byte)(addr % 256));
                ushort len = 2;// 读取寄存器数量 
                bytes.Add((byte)(len / 256));
                bytes.Add((byte)(len % 256));

                bytes = CRC16(bytes);

                SerialPort serialPort = new SerialPort("COM1", 9600, Parity.None, 8, StopBits.One);
                serialPort.Open();
                // 发送
                //while (true)    // 两次发送之间   有时间间隔  RTU    一直等待
                {
                    //3.5个字符时间     传输距离   设备扫描周期   
                    //9600波特率   1000ms   9600位    起始+8个位+校验+停止 =11   11*3.5   =  多个位   4.01ms
                    Thread.Sleep(50);   // 仿真  不是真实的时间限制    设备扫描周期
                    // 尝试  距离长   效率   干扰   报文
                    // 波特率低   远     波特率高   2ms
                    serialPort.DiscardOutBuffer();// 清空输出缓冲区
                    serialPort.Write(bytes.ToArray(), 0, bytes.Count);


                    // 响应
                    //byte[] data = new byte[serialPort.BytesToRead];// 2n+5     5
                    //serialPort.Read(data, 0, data.Length);

                    List<byte> respBytes = new List<byte>();
                    while (respBytes.Count < len * 2 + 5)
                    {
                        //  超时处理  
                        respBytes.Add((byte)serialPort.ReadByte());
                        if (respBytes.Count == 5 && respBytes[1] > 0x80)
                        {
                            break;
                        }
                    }
                    Console.WriteLine(respBytes.Count);
                    serialPort.DiscardInBuffer();// 清空接收缓冲区

                    // 解析
                }



            }
            #endregion


            #region Modbus Ascii 报文
            if (flag == 8)
            {
                //byte b = 0x0F;// 20
                //string str = b.ToString("X2");// ->20   十进制    x:16进制   2：2位   01
                //// C2   N2   P2  %
                //Convert.ToString(b, 16);// 一数字的  二进制     03    

                List<byte> bytes = new List<byte>();
                bytes.Add(0x01);// 从站地址
                bytes.Add(0x03);// 功能码     20->14    0x31  0x34      0x32  0x30
                ushort addr = 5;// 起始地址
                bytes.Add((byte)(addr / 256));
                bytes.Add((byte)(addr % 256));
                ushort len = 4;// 读取寄存器数量 
                bytes.Add((byte)(len / 256));
                bytes.Add((byte)(len % 256));

                //bytes = CRC16(bytes);// RTU   LRC校验
                LRC(bytes);// 7个字节

                // byte[]   01 03 00 05.....
                // 0x30  0x31
                // 转换每个Ascii
                var bytesStrArray = bytes.Select(b => b.ToString("X2")).ToList();// List<string>
                // "01" "03" "00" "05".......
                string bytesStr = ":" + string.Join("", bytesStrArray) + "\r\n";// ":010305....\r\n"
                //:010300050004f3\r\n
                byte[] asciiBytes = Encoding.ASCII.GetBytes(bytesStr);
                // 0x30 0x31 0x30 0x33 0x30 0x35 .......
                // 3A 30 31 30 33 30 30 30 35 30 30 30 34 46 33 0D 0A 

                // 不能发送
                // 头  尾




                SerialPort serialPort = new SerialPort("COM1", 9600, Parity.None, 8, StopBits.One);
                serialPort.Open();
                serialPort.Write(asciiBytes.ToArray(), 0, asciiBytes.Length);

                // 响应
                byte[] data = new byte[len * 4 + 11];// 2n+5     5
                serialPort.Read(data, 0, data.Length);

                // Ascii 500
                // 3A 30 31 30 33 30 38 30 30 30 30 30 30 30 30 30 30 30 30 30 30 30 30 46 34 0D 0A   Ascii编译
                string asciiStr = Encoding.ASCII.GetString(data, 0, data.Length);
                // ":010308....\r\n"
                List<byte> dataBytes = new List<byte>();
                for (int i = 1; i < asciiStr.Length - 2; i += 2)
                {
                    string temp = asciiStr[i].ToString() + asciiStr[i + 1].ToString();// "01"  "03"   "08"
                    dataBytes.Add(Convert.ToByte(temp, 16));// byte {0x01  0x03  0x08}
                }
                // dataBytes: 0x01 0x03 0x08 ..... 0xF4
                // LRC校验检查

                for (int i = 3; i < dataBytes.Count - 2; i += 2)
                {
                    byte[] vb = new byte[2] { dataBytes[i + 1], dataBytes[i] };
                    ushort v = BitConverter.ToUInt16(vb);// 解析出无符号短整型数据
                    Console.WriteLine(v);
                }
                //var ascii = new List<byte>(data).Select(b => (char)b).ToList();
                // []:   0  1  0  3   Ascii码
            }
            #endregion

            #region Modbus Tcp 报文
            if (flag == 9)
            {
                List<byte> dataBytes = new List<byte>();
                dataBytes.Add(0x01);// 从站地址
                dataBytes.Add(0x03);// 功能码     20->14    0x31  0x34      0x32  0x30
                ushort addr = 5;// 起始地址
                dataBytes.Add((byte)(addr / 256));
                dataBytes.Add((byte)(addr % 256));
                ushort len = 4;// 读取寄存器数量 
                dataBytes.Add((byte)(len / 256));
                dataBytes.Add((byte)(len % 256));


                // [] [] [0] [1] [2] [3] [4] [5] [6] 
                // [0] [1]  0x00 0x01

                List<byte> headerBytes = new List<byte>();
                ushort tid = 0;
                // 如果用Insert   先插入  长度 - pid - tid
                //dataBytes.Insert(0, (byte)(tid % 256));
                //dataBytes.Insert(0, (byte)(tid / 256));
                // tid

                // Socket
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect("127.0.0.1", 502);

                while (true)
                {
                    tid++;
                    headerBytes.Add((byte)(tid / 256));
                    headerBytes.Add((byte)(tid % 256));
                    // pid
                    headerBytes.Add(0x00);
                    headerBytes.Add(0x00);
                    // len
                    headerBytes.Add((byte)(dataBytes.Count / 256));
                    headerBytes.Add((byte)(dataBytes.Count % 256));

                    headerBytes.AddRange(dataBytes);

                    socket.Send(headerBytes.ToArray());
                }

                // 接收解析
                byte[] bytes = new byte[len * 2 + 9];
                socket.Receive(bytes, 0, bytes.Length, SocketFlags.None); // 卡线程

                // 协议  字节长度  
                // 先获取 6个长度的字节    byte[6]    取出最后两个字节计算PDU部分
                // 串行不建议分组处理   一次获取所有有效字节  需要进行相关的校验查
            }
            #endregion

            #region Zhaoxi.Communication 业务层面读测试 - RTU
            if (flag == 10)
            {
                // 创建通信实例  ModbusRTU  ModbusAscii  ModbusTcp
                // ModbusRtu  master=new ModbusRtu();
                // List<bool> datas = master.ReadCoils(1,1,0,10);

                ModbusRtu modbusRtu = new ModbusRtu("COM1", 9600, 8, Parity.None, StopBits.One);
                modbusRtu.EndianType = EndianType.AB;
                //modbusRtu.ReadCoils();
                //Result<bool> result = modbusRtu.ReadCoils(1, 1, 0, 2000);
                Result<bool> result = modbusRtu.ReadCoils(1, "00001", 5);
                // 问题:count指的是寄存器数还是模拟量数？
                //Result<ushort> result = modbusRtu.ReadRegisters<ushort>(slaveNum: 1, funcCode: 3, startAddr: 0, dataCount: 200);
                //Result<ushort> result = modbusRtu.ReadRegisters<ushort>(slaveNum: 1, variable: "40001", count: 5);
                //Result<float> result = modbusRtu.ReadRegisters<float>(1, 3, 0, dataCount: 1);

                //Result<byte> result = modbusRtu.ReadBytes(1, 3, 0, 2);
                if (result.Status)
                {
                    result.Datas.ForEach(data => Console.WriteLine(data));
                    //Console.WriteLine(Encoding.UTF8.GetString(result.Datas.ToArray()));
                }
                else
                {
                    Console.WriteLine(result.Message);
                }
                //result.Status = false;
                //result.Message = "excption";


            }
            #endregion

            #region Zhaoxi.Communication 业务层面写测试 - RTU
            if (flag == 11)
            {
                ModbusRtu modbusRtu = new ModbusRtu("COM1", 9600, 8, Parity.None, StopBits.One);
                //Result<bool> result = modbusRtu.WriteCoils(1, 1, new List<bool> { true, false, true, true, false, false, true });
                //Result<bool> result = modbusRtu.WriteRegisters<ushort>(1, 1,
                //    new List<ushort> { 11, 22, 33, 44, 55, 66, 77 });
                //    

                byte[] v = Encoding.UTF8.GetBytes("123456");
                Result<bool> result = modbusRtu.WriteBytes(1, 0, new List<byte>(v));
                if (result.Status)
                {
                    Console.WriteLine("写入成功");

                    Result<byte> r1 = modbusRtu.ReadBytes(1, 3, 0, (ushort)v.Length);
                    if (r1.Status)
                    {
                        //result.Datas.ForEach(data => Console.WriteLine(data));
                        Console.WriteLine(Encoding.UTF8.GetString(r1.Datas.ToArray()));
                    }
                    else
                    {
                        Console.WriteLine(r1.Message);
                    }
                }
                else
                {
                    Console.WriteLine(result.Message);
                }
            }
            #endregion

            #region Zhaoxi.Communication 业务层面测试 - Ascii
            if (flag == 12)
            {
                ModbusBase master = new ModbusAscii("COM1", 9600, 8, Parity.None, StopBits.One);
                //Result<bool> result = master.ReadCoils(1, "00001", 5);
                //Result<ushort> result = master.ReadRegisters<ushort>(1, "40001", 5);
                //if (result.Status)
                //{
                //    result.Datas.ForEach(data => Console.WriteLine(data));
                //}
                //else
                //{
                //    Console.WriteLine(result.Message);
                //}
                //master.WriteCoils(1, 0, new List<bool> { true, true, false, false, true });
                master.WriteRegisters<float>(1, 0, new List<float> { 1.2f, 2.3f, 3.4f, 4.5f });
            }
            #endregion

            #region Zhaoxi.Communication 业务层面测试 - TCP
            if (flag == 13)
            {

                ModbusTcp master = new ModbusTcp("127.0.0.1");
                //Result<bool> result = master.ReadCoils(1, "00001", 5);
                //while (true)
                //{
                //    // 打开通信链路
                //    Result<bool> result = master.ReadCoils(1, 1, 0, 5);
                //    //Result<float> result = master.ReadRegisters<float>(1, "40001", 4);
                //    if (result.Status)
                //    {
                //        result.Datas.ForEach(data => Console.WriteLine(data));
                //    }
                //    else
                //    {
                //        Console.WriteLine(result.Message);
                //    }
                //}
                //master.WriteCoils(1, 0, new List<bool> { false, true, false, true, true });
                //master.WriteRegisters<float>(1, 0, new List<float> { 1.1f, 2.2f, 3.3f, 4.4f });

                // 关闭通信链路
                //master.Dispose();
                // A  ---   B
                // 
                // 0001 0002   40001 40001    通信异构平台功能

                for (int i = 0; i < 10; i++)
                {
                    master.ReadRegistersAsync<float>(1, 3, 0, 4, result =>
                    {
                        if (result.Status)
                        {
                            result.Datas.ForEach(data => Console.Write(data));
                        }
                        else
                        {
                            Console.WriteLine(result.Message);
                        }

                    });
                }
            }
            #endregion
            Console.ReadLine();
        }

        static List<byte> CRC16(List<byte> value, ushort poly = 0xA001, ushort crcInit = 0xFFFF)
        {
            if (value == null || !value.Any())
                throw new ArgumentException("");

            //运算
            ushort crc = crcInit;
            for (int i = 0; i < value.Count; i++)
            {
                crc = (ushort)(crc ^ (value[i]));
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ poly) : (ushort)(crc >> 1);
                }
            }
            byte hi = (byte)((crc & 0xFF00) >> 8);  //高位置
            byte lo = (byte)(crc & 0x00FF);         //低位置

            List<byte> buffer = new List<byte>();
            buffer.AddRange(value);
            buffer.Add(lo);
            buffer.Add(hi);
            return buffer;
        }

        static List<byte> LRC(List<byte> value)
        {
            if (value == null) return null;

            int sum = 0;
            for (int i = 0; i < value.Count; i++)
            {
                sum += value[i];
            }

            sum = sum % 256;
            sum = 256 - sum;

            value.Add((byte)sum);// 16进制一个字节
            return value;
        }
    }
}
