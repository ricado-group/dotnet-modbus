using System;

namespace RICADO.Modbus
{
    public struct ReadCoilsResult
    {
        public int BytesSent;
        public int PacketsSent;
        public int BytesReceived;
        public int PacketsReceived;
        public double Duration;
        public bool[] Values;
    }
}
