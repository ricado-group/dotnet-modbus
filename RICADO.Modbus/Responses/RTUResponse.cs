using System;
using System.Collections.Generic;
using System.Linq;
using RICADO.Modbus.Requests;

namespace RICADO.Modbus.Responses
{
    internal class RTUResponse
    {
        #region Private Fields

        private byte _functionCode;

        private byte[] _data;

        #endregion


        #region Internal Properties

        internal byte FunctionCode
        {
            get
            {
                return _functionCode;
            }
            private set
            {
                _functionCode = value;
            }
        }

        internal byte[] Data
        {
            get
            {
                return _data;
            }
            private set
            {
                _data = value;
            }
        }

        #endregion


        #region Constructors

        private RTUResponse()
        {
        }

        #endregion


        #region Internal Methods

        internal static RTUResponse CreateNew(Memory<byte> message, RTURequest request)
        {
            if (message.Length < 2)
            {
                throw new RTUException("The RTU Response Message Length was too short");
            }

            RTUResponse response = new RTUResponse();

            byte[] command = message.Slice(0, 2).ToArray();

            if (ValidateFunctionCode(command[0]) == false)
            {
                throw new RTUException("Invalid Function Code '" + command[0].ToString() + "'");
            }

            response.FunctionCode = command[0];

            throwIfResponseError(command[0], command[1]);

            if (response.FunctionCode != request.FunctionCode)
            {
                throw new RTUException("Unexpected Function Code '" + Enum.GetName(typeof(FunctionCode), response.FunctionCode) + "' - Expecting '" + Enum.GetName(typeof(FunctionCode), request.FunctionCode) + "'");
            }

            response.Data = message.Length > 1 ? message.Slice(1, message.Length - 1).ToArray() : new byte[0];

            return response;
        }

        internal static bool ValidateFunctionCode(byte functionCode) => Enum.IsDefined(typeof(enFunctionCode), functionCode) || Enum.IsDefined(typeof(enFunctionCode), functionCode - 0x80);

        internal static int GetMessageLengthHint(RTURequest request, List<byte> receivedData)
        {
            if (receivedData.Count > 0 && Enum.IsDefined(typeof(enFunctionCode), receivedData[0] - 0x80))
            {
                return 2; // Function Code + Exception Code
            }

            int byteCount = 1; // Function Code

            if(request is ReadHoldingCoilsRequest readHoldingCoils)
            {
                byteCount += 1; // Byte Count

                if(readHoldingCoils.Length > 0)
                {
                    byteCount += readHoldingCoils.Length / 8; // One Byte for 8 Bits
                }

                if(readHoldingCoils.Length % 8 != 0)
                {
                    byteCount += 1; // Additional Byte for Extra Bits
                }
            }
            else if (request is ReadInputCoilsRequest readInputCoils)
            {
                byteCount += 1; // Byte Count

                if (readInputCoils.Length > 0)
                {
                    byteCount += readInputCoils.Length / 8; // One Byte for 8 Bits
                }

                if (readInputCoils.Length % 8 != 0)
                {
                    byteCount += 1; // Additional Byte for Extra Bits
                }
            }
            else if (request is ReadHoldingRegistersRequest readHoldingRegisters)
            {
                byteCount += 1; // Byte Count

                byteCount += readHoldingRegisters.Length * 2; // Value Bytes
            }
            else if (request is ReadInputRegistersRequest readInputRegisters)
            {
                byteCount += 1; // Byte Count

                byteCount += readInputRegisters.Length * 2; // Value Bytes
            }
            else if(request is WriteHoldingCoilRequest || request is WriteHoldingCoilsRequest || request is WriteHoldingRegisterRequest || request is WriteHoldingRegistersRequest)
            {
                byteCount += 4; // Address + Single Value or Start Address + Length
            }

            if(byteCount < 2)
            {
                byteCount = 2;
            }

            return byteCount;
        }

        #endregion


        #region Private Methods

        private static void throwIfResponseError(byte functionCode, byte exceptionCode)
        {
            if (Enum.IsDefined(typeof(enFunctionCode), functionCode - 0x80) == false)
            {
                return;
            }

            RTUException exception = exceptionCode switch
            {
                0x00 => null,
                0x01 => new RTUException("Slave Error - Illegal Function Code"),
                0x02 => new RTUException("Slave Error - Illegal Data Address"),
                0x03 => new RTUException("Slave Error - Illegal Data Value in Request"),
                0x04 => new RTUException("Slave Error - Encountered an Unrecoverable Error"),
                _ => new RTUException("Unknown Error - Exception Code (0x" + exceptionCode.ToString("X2") + ")"),
            };

            if (exception != null)
            {
                throw exception;
            }
        }

        #endregion
    }
}
