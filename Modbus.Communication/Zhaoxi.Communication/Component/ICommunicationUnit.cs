using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zhaoxi.Communication.Component
{
    /// <summary>
    /// 通信单元
    /// </summary>
    public interface ICommunicationUnit
    {
        /// <summary>
        /// 连接超时时间
        /// </summary>
        public int ConnectTimeout { get; set; }
        /// <summary>
        /// 打开动作
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public Result<bool> Open(int timeout);
        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close();
        /// <summary>
        /// 发送与接收报文
        /// </summary>
        public Result<byte> SendAndReceive(List<byte> req, int receiveLen, int errorLen);
    }
}
