using System;
using System.Collections.Generic;
using System.Linq;

namespace RICADO.Modbus.Requests
{
    internal class WriteHoldingRegistersRequest : RTURequest
    {
        #region Private Fields

        private ushort _address;
        private short[] _values;

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

        internal short[] Values
        {
            get
            {
                return _values;
            }
            set
            {
                _values = value;
            }
        }

        #endregion


        #region Constructor

        private WriteHoldingRegistersRequest(ModbusRTUDevice device) : base(device)
        {
        }

        #endregion


        #region Internal Methods

        internal static WriteHoldingRegistersRequest CreateNew(ModbusRTUDevice device, ushort address, short[] values)
        {
            return new WriteHoldingRegistersRequest(device)
            {
                FunctionCode = (byte)enFunctionCode.WriteHoldingRegisters,
                Address = address,
                Values = values,
            };
        }

        #endregion


        #region Protected Methods

        protected override List<byte> BuildRequestData()
        {
            List<byte> data = new List<byte>();

            // Address
            data.AddRange(BitConverter.GetBytes(_address).Reverse());

            // Length
            data.AddRange(BitConverter.GetBytes(Convert.ToUInt16(_values.Length)).Reverse());

            // Byte Count
            data.Add((byte)(_values.Length * 2));

            // Values
            data.AddRange(_values.SelectMany(value => BitConverter.GetBytes(value).Reverse()));

            return data;
        }

        #endregion
    }
}
