using System;

namespace RICADO.Modbus
{
    public class RTUException : Exception
    {
        #region Constructors

        internal RTUException(string message) : base(message)
        {
        }

        internal RTUException(string message, Exception innerException) : base(message, innerException)
        {
        }

        #endregion
    }
}
