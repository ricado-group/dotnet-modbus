using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace RICADO.Modbus.Requests
{
    internal class WriteHoldingCoilsRequest : RTURequest
    {
        #region Private Fields

        private ushort _address;
        private bool[] _values;

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

        internal bool[] Values
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

        private WriteHoldingCoilsRequest(ModbusRTUDevice device) : base(device)
        {
        }

        #endregion


        #region Internal Methods

        internal static WriteHoldingCoilsRequest CreateNew(ModbusRTUDevice device, ushort address, bool[] values)
        {
            return new WriteHoldingCoilsRequest(device)
            {
                FunctionCode = (byte)enFunctionCode.WriteHoldingCoils,
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
            byte byteCount = (byte)(((_values.Length % 8) == 0) ? (_values.Length / 8) : ((_values.Length / 8) + 1));
            data.Add(byteCount);

            // Values
            byte[] valuesArray = new byte[byteCount];

            new BitArray(_values).CopyTo(valuesArray, 0);

            data.AddRange(valuesArray);

            return data;
        }

        #endregion
    }
}
