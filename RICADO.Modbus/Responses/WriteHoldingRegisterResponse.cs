using System;
using System.Linq;
using RICADO.Modbus.Requests;

namespace RICADO.Modbus.Responses
{
    internal class WriteHoldingRegisterResponse
    {
        #region Internal Methods

        internal static void Validate(WriteHoldingRegisterRequest request, RTUResponse response)
        {
            if (response.Data.Length < 4)
            {
                throw new RTUException("The Response Data Length of '" + response.Data.Length.ToString() + "' was too short - Expecting a Length of '4'");
            }

#if NETSTANDARD
            byte[] addressBytes = response.Data.Take(2).ToArray();

            addressBytes.Reverse();

            ushort address = BitConverter.ToUInt16(addressBytes, 0);
#else
            Memory<byte> bytes = response.Data.AsMemory();

            Span<byte> addressBytes = bytes.Slice(0, 2).Span;

            addressBytes.Reverse();

            ushort address = BitConverter.ToUInt16(addressBytes);
#endif

            if (address != request.Address)
            {
                throw new RTUException("The Response Address of '" + address.ToString() + "' did not match the Expected Address '" + request.Address.ToString() + "'");
            }

#if NETSTANDARD
            byte[] valueBytes = response.Data.Skip(2).Take(2).ToArray();

            valueBytes.Reverse();

            short value = BitConverter.ToInt16(valueBytes, 0);
#else
            Span<byte> valueBytes = bytes.Slice(2, 2).Span;

            valueBytes.Reverse();

            short value = BitConverter.ToInt16(valueBytes);
#endif

            if (value != request.Value)
            {
                throw new RTUException("The Response Register Value of '" + value.ToString() + "' did not match the Expected Register Value '" + request.Value.ToString() + "'");
            }
        }

        #endregion
    }
}
