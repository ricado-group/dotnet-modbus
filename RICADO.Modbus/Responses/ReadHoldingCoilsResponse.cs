using System.Linq;
using System.Collections;
using RICADO.Modbus.Requests;

namespace RICADO.Modbus.Responses
{
    internal class ReadHoldingCoilsResponse
    {
        #region Internal Methods

        internal static bool[] ExtractValues(ReadHoldingCoilsRequest request, RTUResponse response)
        {
            if(response.Data.Length < 1)
            {
                throw new RTUException("The Response Data Length was too short to extract the Byte Count");
            }

            int expectedByteCount = request.Length > 0 ? request.Length / 8 : 0;

            if(request.Length % 8 != 0)
            {
                expectedByteCount += 1;
            }

            if(response.Data[0] != expectedByteCount)
            {
                throw new RTUException("The Response Byte Count '" + response.Data[0].ToString() + "' did not match the Expected Byte Count '" + expectedByteCount.ToString() + "'");
            }

            if (response.Data.Length < expectedByteCount + 1)
            {
                throw new RTUException("The Response Data Length of '" + response.Data.Length.ToString() + "' was too short - Expecting a Length of '" + (expectedByteCount + 1).ToString() + "'");
            }

            if(request.Length == 0)
            {
                return new bool[0];
            }

            BitArray bitArray = new BitArray(response.Data.Skip(1).ToArray());

            bool[] values = new bool[request.Length];

            for(int i = 0; i < values.Length; i++)
            {
                values[i] = bitArray.Get(i);
            }

            return values;
        }

        #endregion
    }
}
