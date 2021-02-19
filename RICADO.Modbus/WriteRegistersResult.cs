﻿namespace RICADO.Modbus
{
    public struct WriteRegistersResult
    {
        public int BytesSent;
        public int PacketsSent;
        public int BytesReceived;
        public int PacketsReceived;
        public double Duration;
    }
}
