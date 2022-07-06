using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Zhaoxi.Communication.Component;

namespace Zhaoxi.Communication.Modbus
{
    public class ModbusRtu : ModbusBase
    {
        // 必要的   串口参数
        public ModbusRtu(string portName, int baudRate, int dataBit, Parity parity, StopBits stopBits, int readTimeout = 50)
        {
            this.communicationUnit = new Serial(portName, baudRate, dataBit, parity, stopBits, readTimeout);
        }

        #region 读
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
            List<byte> dataBytes = this.CreateReadPDU(slaveNum, funcCode, startAddr, count);
            // 二、计算关拼接CRC校验码
            CRC16(dataBytes);
            // 三、打开/检查通信组件的状态
            Result<bool> connectState = this.communicationUnit.Open(100);
            if (connectState.Status)
            {
                // 四、发送请求报文
                Result<byte> resp = this.communicationUnit.SendAndReceive(
                    dataBytes, // 发送的请求报文 
                    respLen + 5,// 正常响应字节
                    5); // 异常响应报文长度
                if (!resp.Status)
                    throw new Exception(resp.Message);


                // 五、校验检查
                List<byte> crcValidation = resp.Datas.GetRange(0, resp.Datas.Count - 2);
                this.CRC16(crcValidation);
                if (!crcValidation.SequenceEqual(resp.Datas))
                {
                    throw new Exception("CRC校验检查不匹配");
                    // CRC 校验失败
                }

                // 六、检查异常报文
                if (resp.Datas[1] > 0x80)
                {
                    // 
                    byte errorCode = resp.Datas[2];
                    throw new Exception(Errors[errorCode]);
                }
                // 七、解析
                List<byte> datas = resp.Datas.GetRange(3, resp.Datas.Count - 5);
                return datas;
            }
            throw new Exception(connectState.Message);
        }
        #endregion


        #region 写 15  16
        protected override void Write(List<byte> reqBytes)
        {
            // 
            CRC16(reqBytes);
            // 三、打开/检查通信组件的状态
            Result<bool> connectState = this.communicationUnit.Open(100);
            if (connectState.Status)
            {
                // 四、发送请求报文
                Result<byte> resp = this.communicationUnit.SendAndReceive(
                    reqBytes, // 发送的请求报文 
                    8,  // 正常返回的长度
                    5); // 异常响应报文长度
                if (!resp.Status)
                    throw new Exception(resp.Message);


                // 五、校验检查
                List<byte> crcValidation = resp.Datas.GetRange(0, resp.Datas.Count - 2);
                this.CRC16(crcValidation);
                if (!crcValidation.SequenceEqual(resp.Datas))
                {
                    throw new Exception("CRC校验检查不匹配");
                }

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
        #endregion

        /// <summary>
        /// 计算CRC校验码
        /// </summary>
        /// <param name="value"></param>
        /// <param name="poly"></param>
        /// <param name="crcInit"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        void CRC16(List<byte> value, ushort poly = 0xA001, ushort crcInit = 0xFFFF)
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

            value.Add(lo);
            value.Add(hi);
        }
    }
}
