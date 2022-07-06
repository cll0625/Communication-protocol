using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Zhaoxi.Communication.Component;

namespace Zhaoxi.Communication.Modbus
{
    public class ModbusTcp : ModbusBase
    {
        public ModbusTcp(string ip, int port = 502, int readTimeout = 50)
        {
            // 通信单元是收当前对象的构造函数决定的
            this.communicationUnit = new Ethernet(ip, port, readTimeout);
        }
        int tid = 0;
        protected override List<byte> Read(byte slaveNum, byte funcCode, ushort startAddr, ushort count, ushort respLen)
        {
            // 一、组建请求报文
            List<byte> dataBytes = this.CreateReadPDU(slaveNum, funcCode, startAddr, count);
            // 二、拼接ADU头
            List<byte> headerBytes = new List<byte>();
            tid++;
            tid %= 65536;
            headerBytes.Add((byte)(tid / 256));
            headerBytes.Add((byte)(tid % 256));
            // pid
            headerBytes.Add(0x00);
            headerBytes.Add(0x00);
            // len
            headerBytes.Add((byte)(dataBytes.Count / 256));
            headerBytes.Add((byte)(dataBytes.Count % 256));

            headerBytes.AddRange(dataBytes);


            // 三、打开/检查通信组件的状态
            Result<bool> connectState = this.communicationUnit.Open(100);
            if (connectState.Status)
            {
                // 四、发送请求报文
                Result<byte> resp = this.communicationUnit.SendAndReceive(
                    headerBytes, // 发送的请求报文 
                    0, // 正常响应字节
                    0); // 异常响应报文长度
                if (!resp.Status)
                    throw new Exception(resp.Message);

                // 五、将Header部分移除
                resp.Datas.RemoveRange(0, 6);

                // 六、检查异常报文
                if (resp.Datas[1] > 0x80)
                {
                    byte errorCode = resp.Datas[2];
                    throw new Exception(Errors[errorCode]);
                }
                // 七、解析
                List<byte> datas = resp.Datas.GetRange(3, resp.Datas.Count - 3);
                return datas;
            }
            throw new Exception(connectState.Message);
        }

        protected override void Write(List<byte> bytes)
        {
            // 二、拼接ADU头
            List<byte> headerBytes = new List<byte>();
            tid++;
            tid %= 65536;
            headerBytes.Add((byte)(tid / 256));
            headerBytes.Add((byte)(tid % 256));
            // pid
            headerBytes.Add(0x00);
            headerBytes.Add(0x00);
            // len
            headerBytes.Add((byte)(bytes.Count / 256));
            headerBytes.Add((byte)(bytes.Count % 256));

            headerBytes.AddRange(bytes);

            // 三、打开/检查通信组件的状态
            Result<bool> connectState = this.communicationUnit.Open(100);
            if (connectState.Status)
            {
                // 四、发送请求报文
                Result<byte> resp = this.communicationUnit.SendAndReceive(
                    headerBytes, // 发送的请求报文 
                    0,  // 正常返回的长度
                    0); // 异常响应报文长度
                if (!resp.Status)
                    throw new Exception(resp.Message);

                // 五、将Header部分移除
                resp.Datas.RemoveRange(0, 6);

                // 六、检查异常报文
                if (resp.Datas[1] > 0x80)
                {
                    byte errorCode = resp.Datas[2];
                    throw new Exception(Errors[errorCode]);
                }
            }
            else
            {
                throw new Exception(connectState.Message);
            }
        }

        // 由业务层面
        public void ReadRegistersAsync<T>(byte slaveNum, byte funcCode, ushort startAddr, ushort count,
            Action<Result<T>> callbackt)
        {
            int len = Marshal.SizeOf(typeof(T));
            // 一、组建请求报文
            List<byte> dataBytes = this.CreateReadPDU(slaveNum, funcCode, startAddr, (ushort)(count * len / 2));
            // 二、拼接ADU头
            List<byte> headerBytes = new List<byte>();
            tid++;
            tid %= 65536;
            headerBytes.Add((byte)(tid / 256));
            headerBytes.Add((byte)(tid % 256));
            // pid
            headerBytes.Add(0x00);
            headerBytes.Add(0x00);
            // len
            headerBytes.Add((byte)(dataBytes.Count / 256));
            headerBytes.Add((byte)(dataBytes.Count % 256));

            headerBytes.AddRange(dataBytes);


            Result<bool> connectState = this.communicationUnit.Open(100);
            if (connectState.Status)
            {
                (this.communicationUnit as Ethernet).SendAsync(
                    headerBytes, resp =>
                    {
                        // 通信单元异步返回的结果
                        // 这个结果里可以拿到返回的报文字节
                        if (!resp.Status)
                            throw new Exception(resp.Message);

                        // 五、将Header部分移除
                        resp.Datas.RemoveRange(0, 6);

                        // 六、检查异常报文
                        if (resp.Datas[1] > 0x80)
                        {
                            byte errorCode = resp.Datas[2];
                            throw new Exception(Errors[errorCode]);
                        }
                        // 七、解析
                        List<byte> datas = resp.Datas.GetRange(3, resp.Datas.Count - 3);

                        Result<T> result = new Result<T>();
                        result.Datas.AddRange(this.ReadValuesByBytes<T>(datas));
                        callbackt?.Invoke(result);
                    });
            }
            else
                throw new Exception(connectState.Message);
        }
    }
}
