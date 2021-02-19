using System;
using System.Collections.Generic;
using System.Linq;

namespace RICADO.Modbus.Requests
{
    internal class WriteHoldingCoilRequest : RTURequest
    {
        #region Private Fields

        private ushort _address;
        private bool _value;

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

        internal bool Value
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

        private WriteHoldingCoilRequest(ModbusRTUDevice device) : base(device)
        {
        }

        #endregion


        #region Internal Methods

        internal static WriteHoldingCoilRequest CreateNew(ModbusRTUDevice device, ushort address, bool value)
        {
            return new WriteHoldingCoilRequest(device)
            {
                FunctionCode = (byte)enFunctionCode.WriteHoldingCoil,
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
            ushort stateValue = (ushort)(_value == true ? 0xFF00 : 0x0000);
            data.AddRange(BitConverter.GetBytes(stateValue).Reverse());

            return data;
        }

        #endregion
    }
}
