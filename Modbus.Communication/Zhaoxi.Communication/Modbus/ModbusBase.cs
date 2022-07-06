using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Zhaoxi.Communication.Component;

namespace Zhaoxi.Communication.Modbus
{
    // 里面可以实现功能
    public abstract class ModbusBase : IDisposable
    {
        protected static Dictionary<int, string> Errors = new Dictionary<int, string>
        {
            { 0x01, "非法功能码"},
            { 0x02, "非法数据地址"},
            { 0x03, "非法数据值"},
            { 0x04, "从站设备故障"},
            { 0x05, "确认，从站需要一个耗时操作"},
            { 0x06, "从站忙"},
            { 0x08, "存储奇偶性差错"},
            { 0x0A, "不可用网关路径"},
            { 0x0B, "网关目标设备响应失败"},
        };

        public EndianType EndianType { get; set; } = EndianType.AB;

        // 通信单元（包含串口、以太网）
        public ICommunicationUnit communicationUnit { get; set; }
        #region 基方法
        /// <summary>
        /// 获取读取报文（设备地址+PDU）
        /// </summary>
        /// <param name="slaveNum">从站地址</param>
        /// <param name="funcCode">功能码</param>
        /// <param name="startAddr">起始地址</param>
        /// <param name="count">读取寄存器数量</param>
        /// <returns></returns>
        public List<byte> CreateReadPDU(byte slaveNum, byte funcCode, ushort startAddr, ushort count)
        {
            List<byte> datas = new List<byte>();
            datas.Add(slaveNum);
            datas.Add(funcCode);

            datas.Add((byte)(startAddr / 256));
            datas.Add((byte)(startAddr % 256));

            datas.Add((byte)(count / 256));
            datas.Add((byte)(count % 256));

            return datas;
        }
        /// <summary>
        /// 获取写入报文（设备地址+PDU）
        /// </summary>
        /// <returns></returns>
        public List<byte> CreateWritePDU(byte slaveNum, byte funcCode, ushort startAddr, ushort count, byte len)
        {
            List<byte> bytes = new List<byte>();
            bytes.Add(slaveNum);
            bytes.Add(funcCode);

            bytes.Add((byte)(startAddr / 256));
            bytes.Add((byte)(startAddr % 256));


            // 状态：一个状态就是一个寄存器
            // 保持型寄存器：ushort  一个寄存器   float   二个寄存器
            // 写了入寄存器数量
            bytes.Add((byte)(count / 256));
            bytes.Add((byte)(count % 256));

            bytes.Add(len);// 写入字节数

            return bytes;
        }

        /// <summary>
        /// 表示将一个数据字节进行指定字节序的调整
        /// </summary>
        /// <param name="bytes">接收待转换的设备中返回的字节数组</param>
        /// <returns>返回调整完成的字节数组</returns>
        public List<byte> SwitchEndianType(List<byte> bytes)
        {
            // 不管是什么字节序，这个Switch里返回的是ABCD这个顺序
            List<byte> temp = new List<byte>();
            switch (EndianType)  // alt+enter
            {
                case EndianType.AB:
                case EndianType.ABCD:
                case EndianType.ABCDEFGH:
                    temp = bytes;
                    break;
                case EndianType.BA:
                case EndianType.DCBA:
                case EndianType.HGFEDCBA:
                    for (int i = bytes.Count - 1; i >= 0; i--)
                    {
                        temp.Add(bytes[i]);
                    }
                    //temp = new List<byte> { bytes[1], bytes[0] };
                    break;
                case EndianType.CDAB:
                    temp = new List<byte> { bytes[2], bytes[3], bytes[0], bytes[1] };
                    break;
                case EndianType.BADC:
                    temp = new List<byte> { bytes[1], bytes[0], bytes[3], bytes[2] };
                    break;
                //case EndianType.DCBA:
                //    temp = new List<byte> { bytes[3], bytes[2], bytes[1], bytes[0] };
                //    break;
                case EndianType.GHEFCDAB:
                    temp = new List<byte> { bytes[6], bytes[7], bytes[4], bytes[5], bytes[2], bytes[3], bytes[0], bytes[1] };
                    break;
                case EndianType.BADCFEHG:
                    break;
            }
            // bitconverter  DCBA
            // BitConverter  跟平台有关
            // 当BitConverter是小端模式的时修改   BA
            //                 大端模式的时       AB
            // 123     00 7B
            //         7B 00    BitConverter   
            if (BitConverter.IsLittleEndian)
                temp.Reverse();

            return temp;
        }

        public List<bool> ReadStatusByBytes(List<byte> dataBytes, int count)
        {
            List<bool> result = new List<bool>();
            int sum = 0;
            for (int j = 0; j < dataBytes.Count; j++)
            {
                for (int k = 0; k < 8; k++)
                {
                    // 遍历每个字节中的每个位
                    byte temp = (byte)(1 << k);
                    result.Add((dataBytes[j] & temp) != 0);
                    // 
                    sum++;
                    if (sum == count)
                        break;
                }
            }
            return result;
        }

        public List<T> ReadValuesByBytes<T>(List<byte> dataBytes)
        {
            List<T> result = new List<T>();
            int len = Marshal.SizeOf(typeof(T));

            #region 利用反射，根据数据类型获取对应转换方法
            Type tBitConverter = typeof(BitConverter);
            MethodInfo[] mis = tBitConverter.GetMethods(
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo method = mis.FirstOrDefault(
                mi => mi.ReturnType == typeof(T)
                && mi.GetParameters().Length == 2) as MethodInfo;
            if (method == null)
                throw new Exception("转换数据类型出错：未找到匹配的数据类型转换方法");
            #endregion

            for (int k = 0; k < dataBytes.Count; k += len)
            {
                List<byte> dataTemp = dataBytes.GetRange(k, len);
                dataTemp = this.SwitchEndianType(dataTemp);
                result.Add((T)method.Invoke(tBitConverter, new object[] { dataTemp.ToArray(), 0 }));
            }
            return result;
        }
        #endregion
        #region 虚方法
        protected virtual List<byte> Read(byte slaveNum, byte funcCode, ushort startAddr, ushort count, ushort respLen) { return null; }
        protected virtual void Write(List<byte> bytes) { }
        #region 读
        /// <summary>
        /// 读取线圈（线圈状态 、输入线圈）
        /// </summary>
        /// <param name="slaveNum">从站地址</param>
        /// <param name="funcCode">功能码</param>
        /// <param name="startAddr">起始地址</param>
        /// <param name="count">读取数量</param>
        /// <returns></returns>
        public virtual Result<bool> ReadCoils(byte slaveNum, byte funcCode, ushort startAddr, ushort count)
        {
            Result<bool> result = new Result<bool>();
            try
            {
                for (ushort i = 0; i < count; i += 240 * 8)
                {
                    startAddr += i;
                    ushort perCount = (ushort)Math.Min(count - i, 240 * 8);

                    List<byte> datas = this.Read(slaveNum, funcCode,
                        startAddr,
                        perCount,
                        (ushort)(Math.Ceiling(perCount * 1.0 / 8)));

                    //int sum = 0;
                    //for (int j = 0; j < datas.Count; j++)
                    //{
                    //    for (int k = 0; k < 8; k++)
                    //    {
                    //        // 遍历每个字节中的每个位
                    //        byte temp = (byte)(1 << k);
                    //        result.Datas.Add((datas[j] & temp) != 0);
                    //        // 
                    //        sum++;
                    //        if (sum == perCount)
                    //            break;
                    //    }
                    //}
                    result.Datas.AddRange(this.ReadStatusByBytes(datas, count));
                }
            }
            catch (Exception ex)
            {
                result.Status = false;
                result.Message = ex.Message;
            }


            return result;
        }
        // 问题：这里是否需要区分协议类型？不需要
        public virtual Result<bool> ReadCoils(byte slaveNum, string variable, ushort count)
        {
            byte funcCode;
            ushort startAddr;

            // 做地址解析
            this.AnalysisAddress(variable, out funcCode, out startAddr);

            return ReadCoils(slaveNum, funcCode, startAddr, count);
        }
        /// <summary>
        /// 读取寄存器（保持型寄存器、输入寄存器）
        /// </summary>
        /// <returns></returns>
        public virtual Result<T> ReadRegisters<T>(byte slaveNum, byte funcCode, ushort startAddr, ushort dataCount)
        {
            Result<T> result = new Result<T>();

            int len = Marshal.SizeOf(typeof(T));

            try
            {
                int reqTotalCount = dataCount * len / 2;
                for (ushort i = 0; i < reqTotalCount; i += 120)
                {
                    startAddr += i;
                    var rCount = (ushort)Math.Min(reqTotalCount - i, 120);
                    List<byte> datas = this.Read(
                        slaveNum, funcCode,
                        startAddr,// 起始地址
                        rCount, // 寄存器数量
                        (ushort)(rCount * 2));// 正常响应报文字节数

                    //#region 利用反射，根据数据类型获取对应转换方法
                    //Type tBitConverter = typeof(BitConverter);
                    //MethodInfo[] mis = tBitConverter.GetMethods(
                    //    BindingFlags.Public | BindingFlags.Static);
                    //MethodInfo method = mis.FirstOrDefault(
                    //    mi => mi.ReturnType == typeof(T)
                    //    && mi.GetParameters().Length == 2) as MethodInfo;
                    //if (method == null)
                    //    return new Result<T> { Status = false, Message = "转换数据类型出错：未找到匹配的数据类型转换方法" };
                    //#endregion

                    //for (int k = 0; k < datas.Count; k += len)
                    //{
                    //    List<byte> dataTemp = datas.GetRange(k, len);
                    //    dataTemp = this.SwitchEndianType(dataTemp);
                    //    result.Datas.Add((T)method.Invoke(tBitConverter, new object[] { dataTemp.ToArray(), 0 }));
                    //}
                    result.Datas.AddRange(this.ReadValuesByBytes<T>(datas));
                }
            }
            catch (Exception ex)
            {
                result.Status = false;
                result.Message = ex.Message;
            }


            return result;
        }
        public virtual Result<T> ReadRegisters<T>(byte slaveNum, string variable, ushort count)
        {
            byte funcCode;
            ushort startAddr;

            // 做地址解析
            this.AnalysisAddress(variable, out funcCode, out startAddr);

            return ReadRegisters<T>(slaveNum, funcCode, startAddr, count);
        }
        #endregion
        #region 写
        /// <summary>
        /// 写线圈状态，不需要功能码指定   05 15 
        /// </summary>
        /// <returns></returns>
        public virtual Result<bool> WriteCoils(byte slaveNum, ushort startAddrss, List<bool> datas)
        {
            Result<bool> result = new Result<bool>();

            try
            {
                List<byte> reqBytes = this.CreateWritePDU(slaveNum, 15, startAddrss,
                      (ushort)datas.Count,
                      (byte)Math.Ceiling(datas.Count * 1.0 / 8));
                // 组装写入报文 
                List<byte> valueBytes = new List<byte>();
                int index = 0;
                for (int i = 0; i < datas.Count; i++)
                {
                    //status.Count  =10   每8个一个字节
                    if (i % 8 == 0)
                        valueBytes.Add(0x00);// 初始值
                    index = valueBytes.Count - 1;

                    if (datas[i])
                    {
                        byte temp = (byte)(1 << (i % 8));
                        valueBytes[index] |= temp;
                    }
                }
                reqBytes.AddRange(valueBytes);

                this.Write(reqBytes);
            }
            catch (Exception ex)
            {
                result.Status = false;
                result.Message = ex.Message;
            }
            return result;
        }
        /// <summary>
        /// 写保持型寄存器
        /// </summary>
        /// <returns></returns>
        public virtual Result<bool> WriteRegisters<T>(byte slaveNum, ushort startAddrss, List<T> datas)
        {
            Result<bool> result = new Result<bool>();

            try
            {
                int len = Marshal.SizeOf(typeof(T));// 当前写入数据中每个数据所需要的字节数

                List<byte> reqBytes = this.CreateWritePDU(slaveNum, 16, startAddrss,
                      (ushort)(len / 2 * datas.Count),
                      (byte)(len * datas.Count));
                // 组装写入报文  
                List<byte> valueBytes = new List<byte>();
                for (int i = 0; i < datas.Count; i++)
                {
                    // 获取每个数字的字节   反射？
                    dynamic v = datas[i];
                    List<byte> vBytes = new List<byte>(BitConverter.GetBytes(v));
                    // 字节序
                    vBytes = this.SwitchEndianType(vBytes);
                    reqBytes.AddRange(vBytes);
                }

                this.Write(reqBytes);
            }
            catch (Exception ex)
            {
                result.Status = false;
                result.Message = ex.Message;
            }
            return result;
        }

        #endregion

        // 关于字符串处理
        // 存储单元：2个字节    Modbus    三菱
        //           1个字节    西门子    
        // string   "Hello\0"  长度不确定   起始地址：0    3个寄存
        // 长度不确定    count:寄存器/字节数
        /// <summary>
        /// 任意字节获取：比如字符串，针对保持型寄存
        /// </summary>
        /// <param name="slaveNum"></param>
        /// <param name="funcCode"></param>
        /// <param name="startAddr"></param>
        /// <param name="count">字节数</param>
        /// <returns></returns>
        public virtual Result<byte> ReadBytes(byte slaveNum, byte funcCode, ushort startAddr, ushort bytesCount)
        {
            Result<byte> result = new Result<byte>();

            // 一个寄存器存2个字节
            ushort reqTotalCount = (ushort)Math.Ceiling(bytesCount * 1.0 / 2);

            try
            {
                for (ushort i = 0; i < reqTotalCount; i += 120)
                {
                    // 起始地址/请求长度
                    startAddr += i;
                    // 寄存器数量（单次请求）
                    var rCount = (ushort)Math.Min(reqTotalCount - i, 120);

                    List<byte> datas = this.Read(
                        slaveNum, funcCode,
                        startAddr,// 起始地址
                        rCount, // 寄存器数量
                        (ushort)(rCount * 2));

                    result.Datas.AddRange(datas);
                }
            }
            catch (Exception ex)
            {
                result.Status = false;
                result.Message = ex.Message;
            }


            return result;
        }
        public virtual Result<byte> ReadBytes(int v, byte slaveNum, string variable, ushort byteCount)
        {
            byte funcCode;
            ushort startAddr;

            // 做地址解析
            this.AnalysisAddress(variable, out funcCode, out startAddr);

            return ReadBytes(slaveNum, funcCode, startAddr, byteCount);
        }
        /// <summary>
        /// 以字节的形式写入多个寄存器，默认使用16功能码
        /// </summary>
        /// <param name="slaveNum"></param>
        /// <param name="startAddr"></param>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public virtual Result<bool> WriteBytes(byte slaveNum, ushort startAddr, List<byte> bytes)
        {
            Result<bool> result = new Result<bool>();

            try
            {
                List<byte> reqBytes = this.CreateWritePDU(slaveNum, 16, startAddr,
                      (ushort)Math.Ceiling(bytes.Count * 1.0 / 2),// 寄存器数量
                      (byte)(bytes.Count + (bytes.Count % 2)));// 当只有奇数个字节的时候，被齐一个

                reqBytes.AddRange(bytes);
                if (bytes.Count % 2 > 0)
                    reqBytes.Add(0x00);
                // 调整顺序


                this.Write(reqBytes);
            }
            catch (Exception ex)
            {
                result.Status = false;
                result.Message = ex.Message;
            }
            return result;
        }
        #endregion


        /// <summary>
        /// 地址解析，目前只是针对读。写？
        /// 40001.1     16bit    第二个位的状态     业务层面处理   多个   20个状态
        /// 123  &  1<<1    
        /// </summary>
        /// <param name="address"></param>
        /// <param name="fc"></param>
        /// <param name="sa"></param>
        private void AnalysisAddress(string address, out byte fc, out ushort sa)
        {
            fc = 0;
            switch (address[0])
            {
                case '0':
                    fc = 1;
                    break;
                case '1':
                    fc = 2;
                    break;
                case '3':
                    fc = 4;
                    break;
                case '4':
                    fc = 3;
                    break;
            }
            sa = (ushort)(ushort.Parse(address.Substring(1)) - 1);
        }

        public void Dispose()
        {
            if (this.communicationUnit != null)
                this.communicationUnit.Close();
        }
        // result.Datas  ->  List<byte> datas  -> Encoding.UTF8.GetString(datas);
        // 关键点：拿对字节

        //  内存  编程  byte
        // string  文件-》byte[]  FileStream

        // 路由事件： 应该Command绑定

        // Hello
        //1- e H
        //2- l l
        //3- \0 o

        // 如果需要考虑中文
        // Encoding.UTF8.GetString
    }
}
