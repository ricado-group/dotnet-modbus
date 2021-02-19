using System;
using System.Linq;
using RICADO.Modbus.Requests;

namespace RICADO.Modbus.Responses
{
    internal class WriteHoldingRegistersResponse
    {
        #region Internal Methods

        internal static void Validate(WriteHoldingRegistersRequest request, RTUResponse response)
        {
            if (response.Data.Length < 4)
            {
                throw new RTUException("The Response Data Length of '" + response.Data.Length.ToString() + "' was too short - Expecting a Length of '4'");
            }

            Memory<byte> bytes = response.Data.AsMemory();

            Span<byte> addressBytes = bytes.Slice(0, 2).Span;

            addressBytes.Reverse();

            ushort address = BitConverter.ToUInt16(addressBytes);

            if (address != request.Address)
            {
                throw new RTUException("The Response Address of '" + address.ToString() + "' did not match the Expected Address '" + request.Address.ToString() + "'");
            }

            Span<byte> lengthBytes = bytes.Slice(2, 2).Span;

            lengthBytes.Reverse();

            ushort length = BitConverter.ToUInt16(lengthBytes);

            if (length != request.Values.Length)
            {
                throw new RTUException("The Response Values Length of '" + length.ToString() + "' did not match the Expected Values Length '" + request.Values.Length.ToString() + "'");
            }
        }

        #endregion
    }
}
