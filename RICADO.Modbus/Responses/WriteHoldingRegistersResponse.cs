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
            byte[] lengthBytes = response.Data.Skip(2).Take(2).ToArray();

            lengthBytes.Reverse();

            ushort length = BitConverter.ToUInt16(lengthBytes, 0);
#else
            Span<byte> lengthBytes = bytes.Slice(2, 2).Span;

            lengthBytes.Reverse();

            ushort length = BitConverter.ToUInt16(lengthBytes);
#endif

            if (length != request.Values.Length)
            {
                throw new RTUException("The Response Values Length of '" + length.ToString() + "' did not match the Expected Values Length '" + request.Values.Length.ToString() + "'");
            }
        }

        #endregion
    }
}
