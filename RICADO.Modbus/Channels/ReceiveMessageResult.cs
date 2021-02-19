using System;

namespace RICADO.Modbus.Channels
{
    internal struct ReceiveMessageResult
    {
        internal Memory<byte> Message;
        internal int Bytes;
        internal int Packets;
    }
}
