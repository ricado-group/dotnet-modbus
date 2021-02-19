using System;
using System.Linq;
using RICADO.Modbus.Requests;

namespace RICADO.Modbus.Responses
{
    internal class WriteHoldingCoilResponse
    {
        #region Internal Methods

        internal static void Validate(WriteHoldingCoilRequest request, RTUResponse response)
        {
            if (response.Data.Length < 4)
            {
                throw new RTUException("The Response Data Length of '" + response.Data.Length.ToString() + "' was too short - Expecting a Length of '4'");
            }

            Memory<byte> bytes = response.Data.AsMemory();

            Span<byte> addressBytes = bytes.Slice(0, 2).Span;

            addressBytes.Reverse();

            ushort address = BitConverter.ToUInt16(addressBytes);

            if(address != request.Address)
            {
                throw new RTUException("The Response Address of '" + address.ToString() + "' did not match the Expected Address '" + request.Address.ToString() + "'");
            }

            Span<byte> valueBytes = bytes.Slice(2, 2).Span;

            valueBytes.Reverse();

            ushort value = BitConverter.ToUInt16(valueBytes);

            ushort expectedValue = (ushort)(request.Value ? 0xFF00 : 0x0000);

            if (value != expectedValue)
            {
                throw new RTUException("The Response Coil Value of '" + value.ToString() + "' did not match the Expected Coil Value '" + expectedValue.ToString() + "'");
            }
        }

        #endregion
    }
}
