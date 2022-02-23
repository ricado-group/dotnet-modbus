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

#if NETSTANDARD
            byte[] bytes = response.Data;

            byte[] addressBytes = bytes.Take(2).ToArray();
#else
            Memory<byte> bytes = response.Data.AsMemory();

            Span<byte> addressBytes = bytes.Slice(0, 2).Span;
#endif

            addressBytes.Reverse();

#if NETSTANDARD
            ushort address = BitConverter.ToUInt16(addressBytes, 0);
#else
            ushort address = BitConverter.ToUInt16(addressBytes);
#endif

            if(address != request.Address)
            {
                throw new RTUException("The Response Address of '" + address.ToString() + "' did not match the Expected Address '" + request.Address.ToString() + "'");
            }

#if NETSTANDARD
            byte[] valueBytes = bytes.Skip(2).Take(2).ToArray();
#else
            Span<byte> valueBytes = bytes.Slice(2, 2).Span;
#endif

            valueBytes.Reverse();

#if NETSTANDARD
            ushort value = BitConverter.ToUInt16(valueBytes, 0);
#else
            ushort value = BitConverter.ToUInt16(valueBytes);
#endif

            ushort expectedValue = (ushort)(request.Value ? 0xFF00 : 0x0000);

            if (value != expectedValue)
            {
                throw new RTUException("The Response Coil Value of '" + value.ToString() + "' did not match the Expected Coil Value '" + expectedValue.ToString() + "'");
            }
        }

        #endregion
    }
}
