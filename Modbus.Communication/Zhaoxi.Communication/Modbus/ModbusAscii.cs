using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zhaoxi.Communication.Component;

namespace Zhaoxi.Communication.Modbus
{
    public class ModbusAscii : ModbusBase
    {
        public ModbusAscii(string portName, int baudRate, int dataBit, Parity parity, StopBits stopBits, int readTimeout = 50)
        {
            this.communicationUnit = new Serial(portName, baudRate, dataBit, parity, stopBits, readTimeout);
        }

        /// <summary>
        /// 读取数据请求的方法封装
        /// </summary>
        /// <param name="slaveNum"></param>
        /// <param name="funcCode"></param>
        /// <param name="startAddr"></param>
        /// <param name="count">请求寄存器数量</param>
        /// <param name="respLen">正常响应字节数</param>
        /// <returns></returns>
        protected override List<byte> Read(byte slaveNum, byte funcCode, ushort startAddr, ushort count, ushort respLen)
        {
            // 一、组建请求报文
            List<byte> reqBytes = this.CreateReadPDU(slaveNum, funcCode, startAddr, count);
            // 二、相对RTU协议来讲：修改第一步
            LRC(reqBytes);
            // 修改第二步：转换Ascii
            var bytesStrArray = reqBytes.Select(b => b.ToString("X2")).ToList();
            string bytesStr = ":" + string.Join("", bytesStrArray) + "\r\n";
            List<byte> asciiBytes = new List<byte>(Encoding.ASCII.GetBytes(bytesStr));
            // 三、打开/检查通信组件的状态
            Result<bool> connectState = this.communicationUnit.Open(100);
            if (connectState.Status)
            {
                // 四、发送请求报文
                Result<byte> resp = this.communicationUnit.SendAndReceive(
                    asciiBytes, // 发送的请求报文 
                    (respLen + 4) * 2 + 3, // 正常响应字节数
                    13); // 异常响应报文长度
                if (!resp.Status)
                    throw new Exception(resp.Message);

                // 从Ascii报文转换成数据报文
                List<byte> dataBytes = new List<byte>();
                string asciiStr = Encoding.ASCII.GetString(resp.Datas.ToArray(), 0, resp.Datas.Count);
                for (int i = 1; i < asciiStr.Length - 2; i += 2)
                {
                    string temp = asciiStr[i].ToString() + asciiStr[i + 1].ToString();// "01"  "03"   "08"
                    dataBytes.Add(Convert.ToByte(temp, 16));// byte {0x01  0x03  0x08}
                }

                // 五、校验检查
                List<byte> lrcValidation = dataBytes.GetRange(0, dataBytes.Count - 1);
                this.LRC(lrcValidation);
                if (!lrcValidation.SequenceEqual(dataBytes))
                {
                    throw new Exception("LRC校验检查不匹配");
                }

                // 六、检查异常报文
                if (dataBytes[1] > 0x80)
                {
                    byte errorCode = resp.Datas[2];
                    throw new Exception(Errors[errorCode]);
                }
                // 七、解析
                List<byte> datas = dataBytes.GetRange(3, dataBytes.Count - 4);
                return datas;
            }
            throw new Exception(connectState.Message);
        }

        protected override void Write(List<byte> bytes)
        {
            LRC(bytes);
            // 修改第二步：转换Ascii
            var bytesStrArray = bytes.Select(b => b.ToString("X2")).ToList();
            string bytesStr = ":" + string.Join("", bytesStrArray) + "\r\n";
            List<byte> asciiBytes = new List<byte>(Encoding.ASCII.GetBytes(bytesStr));


            // 三、打开/检查通信组件的状态
            Result<bool> connectState = this.communicationUnit.Open(100);
            if (connectState.Status)
            {
                // 四、发送请求报文
                Result<byte> resp = this.communicationUnit.SendAndReceive(
                    asciiBytes, // 发送的请求报文 
                    7 * 2 + 3,  // 正常返回的长度     基础报文7个字节  转成Ascii编码   *2   3个头尾字符
                    4 * 2 + 3); // 异常响应报文长度
                if (!resp.Status)
                    throw new Exception(resp.Message);

                // 从Ascii报文转换成数据报文
                List<byte> dataBytes = new List<byte>();
                string asciiStr = Encoding.ASCII.GetString(resp.Datas.ToArray(), 0, resp.Datas.Count);
                for (int i = 1; i < asciiStr.Length - 2; i += 2)
                {
                    string temp = asciiStr[i].ToString() + asciiStr[i + 1].ToString();// "01"  "03"   "08"
                    dataBytes.Add(Convert.ToByte(temp, 16));// byte {0x01  0x03  0x08}
                }

                // 五、校验检查
                List<byte> lrcValidation = dataBytes.GetRange(0, dataBytes.Count - 1);
                this.LRC(lrcValidation);
                if (!lrcValidation.SequenceEqual(dataBytes))
                {
                    throw new Exception("LRC校验检查不匹配");
                }

                // 六、检查异常报文
                if (dataBytes[1] > 0x80)
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

        private void LRC(List<byte> value)
        {
            if (value == null) return;

            int sum = 0;
            for (int i = 0; i < value.Count; i++)
            {
                sum += value[i];
            }

            sum = sum % 256;
            sum = 256 - sum;

            value.Add((byte)sum);// 16进制一个字节
        }
    }
}
