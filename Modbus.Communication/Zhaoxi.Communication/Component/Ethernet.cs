using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Zhaoxi.Communication.Component
{
    public class Ethernet : ICommunicationUnit
    {
        Socket socket;//   TCP
                      //   UDP    = new Socket();
        public Ethernet(string ip, int port, int readTimeout)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.ReceiveTimeout = readTimeout;
            ReadTimeout = readTimeout;
            IP = ip;
            Port = port;
        }
        private string IP { get; set; }
        private int Port { get; set; }
        private int ReadTimeout { get; set; } = 50;
        public int ConnectTimeout { get; set; }

        ManualResetEvent TimeoutObject = new ManualResetEvent(false);

        public void Close()
        {
            if (socket != null && socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Dispose();
            }
        }

        bool connectState = false;
        public Result<bool> Open(int timeout)
        {
            Result<bool> result = new Result<bool>()
            {
                Status = false
            };
            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                while (stopwatch.ElapsedMilliseconds < timeout)
                {
                    if (!(!socket.Connected || (socket.Poll(200, SelectMode.SelectRead) && (socket.Available == 0))))
                    {
                        result.Status = true;
                        return result;
                    }
                    try
                    {
                        connectState = false;
                        socket?.Close();
                        socket.Dispose();
                        socket = null;

                        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        socket.ReceiveTimeout = ReadTimeout;
                        socket.BeginConnect(IP, Port, callback =>
                        {
                            var cbSocket = callback.AsyncState as Socket;
                            if (cbSocket != null)
                            {
                                connectState = cbSocket.Connected;

                                if (cbSocket.Connected)
                                    cbSocket.EndConnect(callback);

                            }
                            TimeoutObject.Set();
                        }, socket);
                        TimeoutObject.WaitOne(timeout, false);
                        if (connectState)
                        {
                            result.Status = true;
                            break;
                        }
                    }
                    catch (SocketException ex)
                    {
                        if (ex.ErrorCode == 10060)
                            throw new Exception(ex.Message);
                    }
                    catch (Exception) { }
                }
                if (socket == null || !socket.Connected || ((socket.Poll(200, SelectMode.SelectRead) && (socket.Available == 0))))
                {
                    throw new Exception("网络连接失败");
                }
            }
            catch (Exception ex)
            {
                result.Status = false;
                result.Message = ex.Message;
            }
            return result;
        }

        public Result<byte> SendAndReceive(List<byte> req, int receiveLen, int errorLen)
        {
            Result<byte> result = new Result<byte>();
            try
            {
                // 同步
                socket.Send(req.ToArray(), 0, req.Count, SocketFlags.None);

                // 获取指定长度：MBAP  头部
                byte[] headerBytes = new byte[6];
                int count = socket.Receive(headerBytes, 0, 6, SocketFlags.None);
                if (count == 0)
                    throw new Exception("未接收到响应数据");
                // 数据PDU部分数据
                int pduLen = BitConverter.ToInt16(new byte[] { headerBytes[5], headerBytes[4] });
                byte[] pduBytes = new byte[pduLen];
                count = socket.Receive(pduBytes, 0, pduLen, SocketFlags.None);
                if (count == 0)
                    throw new Exception("未接收到响应数据");

                result.Datas.AddRange(headerBytes);
                result.Datas.AddRange(pduBytes);
            }
            catch (Exception ex)
            {
                result.Status = false;
                result.Message = ex.Message;
            }
            return result;
        }

        // 异步：从发送到接收的这个过程
        object lockObj = new object();
        public void SendAsync(List<byte> reqBytes, Action<Result<byte>> callback)
        {
            /// 有三方式可以做为异步解决方案
            /// 1、按同步思路调整为异步，使用Task
            /// 2、使用Socket的BeginReceive方法
            /// 3、发送与接收分离
            socket.Send(reqBytes.ToArray(), 0, reqBytes.Count, SocketFlags.None);

            Task.Run(() =>
            {
                lock (lockObj)
                {
                    Result<byte> result = new Result<byte>();
                    // 获取指定长度：MBAP  头部
                    byte[] headerBytes = new byte[6];
                    int count = socket.Receive(headerBytes, 0, 6, SocketFlags.None);
                    if (count == 0)
                        throw new Exception("未接收到响应数据");
                    // 数据PDU部分数据
                    int pduLen = BitConverter.ToInt16(new byte[] { headerBytes[5], headerBytes[4] });
                    byte[] pduBytes = new byte[pduLen];
                    count = socket.Receive(pduBytes, 0, pduLen, SocketFlags.None);
                    if (count == 0)
                        throw new Exception("未接收到响应数据");

                    result.Datas.AddRange(headerBytes);
                    result.Datas.AddRange(pduBytes);

                    callback?.Invoke(result);
                    Console.WriteLine("接收完成");
                }
            });
            Console.WriteLine("发送完成");
        }
    }
}
