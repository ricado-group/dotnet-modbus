using System;
using System.Collections.Generic;
using System.Linq;

namespace RICADO.Modbus.Requests
{
    internal class WriteHoldingRegisterRequest : RTURequest
    {
        #region Private Fields

        private ushort _address;
        private short _value;

        #endregion


        #region Internal Properties

        internal ushort Address
        {
            get
            {
                return _address;
            }
            set
            {
                _address = value;
            }
        }

        internal short Value
        {
            get
            {
                return _value;
            }
            set
            {
                _value = value;
            }
        }

        #endregion


        #region Constructor

        private WriteHoldingRegisterRequest(ModbusRTUDevice device) : base(device)
        {
        }

        #endregion


        #region Internal Methods

        internal static WriteHoldingRegisterRequest CreateNew(ModbusRTUDevice device, ushort address, short value)
        {
            return new WriteHoldingRegisterRequest(device)
            {
                FunctionCode = (byte)enFunctionCode.WriteHoldingRegister,
                Address = address,
                Value = value,
            };
        }

        #endregion


        #region Protected Methods

        protected override List<byte> BuildRequestData()
        {
            List<byte> data = new List<byte>();

            // Address
            data.AddRange(BitConverter.GetBytes(_address).Reverse());

            // Value
            data.AddRange(BitConverter.GetBytes(_value).Reverse());

            return data;
        }

        #endregion
    }
}
