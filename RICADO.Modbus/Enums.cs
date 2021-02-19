using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RICADO.Modbus
{
    public enum ConnectionMethod
    {
        TCP,
        SerialOverLAN,
    }
    
    internal enum enFunctionCode : byte
    {
        ReadHoldingCoils = 0x01,
        ReadInputCoils = 0x02,
        ReadHoldingRegisters = 0x03,
        ReadInputRegisters = 0x04,
        WriteHoldingCoil = 0x05,
        WriteHoldingRegister = 0x06,
        WriteHoldingCoils = 0x0F,
        WriteHoldingRegisters = 0x10,
    }
}
