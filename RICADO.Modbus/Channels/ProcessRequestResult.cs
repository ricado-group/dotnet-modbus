using RICADO.Modbus.Responses;

namespace RICADO.Modbus.Channels
{
    internal struct ProcessRequestResult
    {
        internal int BytesSent;
        internal int PacketsSent;
        internal int BytesReceived;
        internal int PacketsReceived;
        internal double Duration;
        internal RTUResponse Response;
    }
}
