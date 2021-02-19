using System;

namespace RICADO.Modbus
{
    public class ModbusException : Exception
    {
        #region Constructors

        internal ModbusException(string message) : base(message)
        {
        }

        internal ModbusException(string message, Exception innerException) : base(message, innerException)
        {
        }

        #endregion
    }
}
