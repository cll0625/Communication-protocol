using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zhaoxi.Communication.Component
{
    public class Serial : ICommunicationUnit
    {
        // Modbus rtu  3.5   1.5
        public int ReadTimeout { get; set; } = 50;
        SerialPort serialPort = new SerialPort();

        public Serial(string portName, int baudRate, int dataBit, Parity parity, StopBits stopBits, int readTimeout)
        {
            // 
            serialPort.PortName = portName;
            serialPort.BaudRate = baudRate;
            serialPort.DataBits = dataBit;
            serialPort.Parity = parity;
            serialPort.StopBits = stopBits;
            serialPort.ReadTimeout = readTimeout;
        }
        public int ConnectTimeout { get; set; }

        public void Close()
        {
            if (serialPort != null)
                serialPort.Close();
        }

        public Result<bool> Open(int timeout)
        {
            Result<bool> result = new Result<bool>();
            try
            {
                if (serialPort == null)
                    throw new Exception("串口对象未初始化");

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                while (stopwatch.ElapsedMilliseconds < timeout)
                {
                    if (serialPort.IsOpen)
                        break;

                    try
                    {
                        serialPort.Open();
                        break;
                    }
                    catch (System.IO.IOException)
                    {
                        Task.Delay(1).GetAwaiter().GetResult();
                    }
                }
                stopwatch.Stop();
                if (serialPort == null || !serialPort.IsOpen)
                    throw new Exception("串口打开失败");
            }
            catch (Exception e)
            {
                result.Status = false;
                result.Message = e.Message;
            }
            // 上层代码不太好处理
            return result;
        }

        // 
        public Result<byte> SendAndReceive(List<byte> req, int receiveLen, int errorLen)
        {
            Result<byte> result = new Result<byte>();
            // 发送
            serialPort.Write(req.ToArray(), 0, req.Count);
            // 接收
            // 问题：
            // ReadByte：ReadTimeout
            // Read不卡线程
            //byte[] data = new byte[len * 2 + 5];// 正常    异常
            //serialPort.Read(data, 0, data.Length);

            List<byte> respBytes = new List<byte>();
            try
            {
                // 长度：正常数据报文长度/异常报文
                // 正常：获取到指定长度的数据
                // 异常：
                while (respBytes.Count < receiveLen)
                {
                    byte data = (byte)serialPort.ReadByte();
                    respBytes.Add(data);
                }

                // RTU    5     Ascii    13
                //while (respBytes.Count < len * 2 + 5)
                //{
                //    //  超时处理  
                //    respBytes.Add((byte)serialPort.ReadByte());
                //    if (respBytes.Count == 5 && respBytes[1] > 0x80)
                //    {
                //        break;
                //    }
                //    Console.WriteLine(respBytes.Count);
                //}
            }
            catch (TimeoutException e)
            {
                if (respBytes.Count != errorLen)
                {
                    result.Status = false;
                    result.Message = "接收报文超时";
                }
            }
            catch (Exception e)
            {
                // 异常：一定时间内没有拿到字节数据
                result.Status = false;
                result.Message = e.Message;
            }
            finally
            {
                result.Datas = respBytes;
            }
            return result;
        }
    }
}
