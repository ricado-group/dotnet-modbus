using System;
using System.Collections.Generic;
using System.Linq;

namespace RICADO.Modbus.Requests
{
    internal class ReadHoldingCoilsRequest : RTURequest
    {
        #region Private Fields

        private ushort _address;
        private ushort _length;

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

        internal ushort Length
        {
            get
            {
                return _length;
            }
            set
            {
                _length = value;
            }
        }

        #endregion


        #region Constructor

        private ReadHoldingCoilsRequest(ModbusRTUDevice device) : base(device)
        {
        }

        #endregion


        #region Internal Methods

        internal static ReadHoldingCoilsRequest CreateNew(ModbusRTUDevice device, ushort address, ushort length)
        {
            return new ReadHoldingCoilsRequest(device)
            {
                FunctionCode = (byte)enFunctionCode.ReadHoldingCoils,
                Address = address,
                Length = length,
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
            data.AddRange(BitConverter.GetBytes(_length).Reverse());

            return data;
        }

        #endregion
    }
}
