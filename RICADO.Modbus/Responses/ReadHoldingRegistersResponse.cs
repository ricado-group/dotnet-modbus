using System;
using System.Collections.Generic;
using System.Linq;
using RICADO.Modbus.Requests;

namespace RICADO.Modbus.Responses
{
    internal class ReadHoldingRegistersResponse
    {
        #region Internal Methods

        internal static short[] ExtractValues(ReadHoldingRegistersRequest request, RTUResponse response)
        {
            if (response.Data.Length < 1)
            {
                throw new RTUException("The Response Data Length was too short to extract the Byte Count");
            }

            int expectedByteCount = request.Length * 2;

            if (response.Data[0] != expectedByteCount)
            {
                throw new RTUException("The Response Byte Count '" + response.Data[0].ToString() + "' did not match the Expected Byte Count '" + expectedByteCount.ToString() + "'");
            }

            if (response.Data.Length < expectedByteCount + 1)
            {
                throw new RTUException("The Response Data Length of '" + response.Data.Length.ToString() + "' was too short - Expecting a Length of '" + (expectedByteCount + 1).ToString() + "'");
            }

            if (request.Length == 0)
            {
                return new short[0];
            }

#if NETSTANDARD
            byte[] bytes = response.Data.Skip(1).Take(expectedByteCount).ToArray();
#else
            Memory<byte> bytes = response.Data.AsMemory().Slice(1, expectedByteCount);
#endif

            List<short> values = new List<short>();

            for(int i = 0; i < bytes.Length; i += 2)
            {
#if NETSTANDARD
                byte[] valueBytes = bytes.Skip(i).Take(2).ToArray();
#else
                Span<byte> valueBytes = bytes.Slice(i, 2).Span;
#endif
                valueBytes.Reverse();

#if NETSTANDARD
                values.Add(BitConverter.ToInt16(valueBytes, 0));
#else
                values.Add(BitConverter.ToInt16(valueBytes));
#endif
            }

            return values.ToArray();
        }

        #endregion
    }
}
