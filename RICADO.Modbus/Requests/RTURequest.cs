﻿using System;
using System.Collections.Generic;

namespace RICADO.Modbus.Requests
{
    internal abstract class RTURequest
    {
        #region Private Fields

        private readonly byte _unitId;

        private byte _functionCode;

        #endregion


        #region Internal Properties

        internal byte UnitID => _unitId;

        internal byte FunctionCode
        {
            get
            {
                return _functionCode;
            }
            set
            {
                _functionCode = value;
            }
        }

        #endregion


        #region Constructors

        protected RTURequest(ModbusRTUDevice device)
        {
            _unitId = device.UnitID;
        }

        #endregion


        #region Internal Methods

#if NETSTANDARD
        internal byte[] BuildMessage()
#else
        internal ReadOnlyMemory<byte> BuildMessage()
#endif
        {
            List<byte> message = new List<byte>();

            // Function Code
            message.Add(_functionCode);

            // Request Data
            message.AddRange(BuildRequestData());

#if NETSTANDARD
            return message.ToArray();
#else
            return new ReadOnlyMemory<byte>(message.ToArray());
#endif
        }

        #endregion


        #region Protected Methods

        protected abstract List<byte> BuildRequestData();

        #endregion
    }
}
