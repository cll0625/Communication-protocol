using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zhaoxi.Communication.Modbus
{
    public enum FuncType
    {
        ReadCoil = 0x01,
        WriteCoil = 0x05,
        WriteMultiCoil = 0x0F,
        ReadInput = 0x02,
        ReadHodingRegister = 0x03,
        WriteHodingRegister = 0x06,
        WriteMultiHodingRegister = 0x10,
        ReadInputRegister = 0x04,
    }
}
