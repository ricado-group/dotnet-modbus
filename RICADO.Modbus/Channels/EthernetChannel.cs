using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RICADO.Sockets;
using RICADO.Modbus.Requests;
using RICADO.Modbus.Responses;

namespace RICADO.Modbus.Channels
{
    internal class EthernetChannel : IChannel
    {
        #region Constants

        internal const int MBAPHeaderLength = 7;

        #endregion


        #region Private Fields

        private readonly string _remoteHost;
        private readonly int _port;

        private TcpClient _client;

        private ushort _requestId = 0;

        private readonly SemaphoreSlim _semaphore;

        #endregion


        #region Internal Properties

        internal string RemoteHost => _remoteHost;

        internal int Port => _port;

        #endregion


        #region Constructors

        internal EthernetChannel(string remoteHost, int port)
        {
            _remoteHost = remoteHost;
            _port = port;

            _semaphore = new SemaphoreSlim(1, 1);
        }

        #endregion


        #region Public Methods

        public void Dispose()
        {
            if (_client == null)
            {
                return;
            }

            try
            {
                _client.Dispose();
            }
            finally
            {
                _client = null;
            }

            _semaphore?.Dispose();
        }

        #endregion


        #region Internal Methods

        public Task InitializeAsync(int timeout, CancellationToken cancellationToken)
        {
            return initializeClient(timeout, cancellationToken);
        }

        public async Task<ProcessRequestResult> ProcessRequestAsync(RTURequest request, int timeout, int retries, CancellationToken cancellationToken)
        {
            int attempts = 0;
            Memory<byte> responseMessage = new Memory<byte>();
            int bytesSent = 0;
            int packetsSent = 0;
            int bytesReceived = 0;
            int packetsReceived = 0;
            DateTime startTimestamp = DateTime.UtcNow;

            while (attempts <= retries)
            {
                try
                {
                    await _semaphore.WaitAsync(cancellationToken);

                    if (attempts > 0)
                    {
                        await destroyAndInitializeClient(timeout, cancellationToken);
                    }

                    // Build the Request into a Message we can Send
                    ReadOnlyMemory<byte> requestMessage = request.BuildMessage();

                    // Send the Message
                    SendMessageResult sendResult = await sendMessageAsync(request.UnitID, requestMessage, timeout, cancellationToken);

                    bytesSent += sendResult.Bytes;
                    packetsSent += sendResult.Packets;

                    // Receive a Response
                    ReceiveMessageResult receiveResult = await receiveMessageAsync(request.UnitID, timeout, cancellationToken);

                    bytesReceived += receiveResult.Bytes;
                    packetsReceived += receiveResult.Packets;
                    responseMessage = receiveResult.Message;

                    break;
                }
                catch (ModbusException)
                {
                    if (attempts >= retries)
                    {
                        throw;
                    }
                }
                finally
                {
                    _semaphore.Release();
                }

                // Increment the Attempts
                attempts++;
            }

            try
            {
                return new ProcessRequestResult
                {
                    BytesSent = bytesSent,
                    PacketsSent = packetsSent,
                    BytesReceived = bytesReceived,
                    PacketsReceived = packetsReceived,
                    Duration = DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds,
                    Response = RTUResponse.CreateNew(responseMessage, request),
                };
            }
            catch (RTUException e)
            {
                throw new ModbusException("Received an RTU Error Response from Modbus TCP Device  '" + _remoteHost + ":" + _port + "'", e);
            }
        }

        #endregion


        #region Private Methods

        private Task initializeClient(int timeout, CancellationToken cancellationToken)
        {
            if (_client != null)
            {
                return Task.CompletedTask;
            }

            _client = new TcpClient(RemoteHost, Port);

            return _client.ConnectAsync(timeout, cancellationToken);
        }

        private async Task destroyAndInitializeClient(int timeout, CancellationToken cancellationToken)
        {
            try
            {
                _client?.Dispose();
            }
            finally
            {
                _client = null;
            }

            try
            {
                await initializeClient(timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                throw new ModbusException("Failed to Re-Connect within the Timeout Period to Modbus TCP Device '" + RemoteHost + ":" + Port + "'");
            }
            catch (System.Net.Sockets.SocketException e)
            {
                throw new ModbusException("Failed to Re-Connect to Modbus TCP Device '" + RemoteHost + ":" + Port + "'", e);
            }
        }

        private async Task<SendMessageResult> sendMessageAsync(byte unitId, ReadOnlyMemory<byte> message, int timeout, CancellationToken cancellationToken)
        {
            SendMessageResult result = new SendMessageResult
            {
                Bytes = 0,
                Packets = 0,
            };

            ReadOnlyMemory<byte> modbusTcpMessage = buildMBAPMessage(unitId, message);

            try
            {
                result.Bytes += await _client.SendAsync(modbusTcpMessage, timeout, cancellationToken);
                result.Packets += 1;
            }
            catch (TimeoutException)
            {
                throw new ModbusException("Failed to Send RTU Message within the Timeout Period to Modbus TCP Device '" + RemoteHost + ":" + Port + "'");
            }
            catch (System.Net.Sockets.SocketException e)
            {
                throw new ModbusException("Failed to Send RTU Message to Modbus TCP Device '" + RemoteHost + ":" + Port + "'", e);
            }

            return result;
        }

        private async Task<ReceiveMessageResult> receiveMessageAsync(byte unitId, int timeout, CancellationToken cancellationToken)
        {
            ReceiveMessageResult result = new ReceiveMessageResult
            {
                Bytes = 0,
                Packets = 0,
                Message = new Memory<byte>(),
            };

            try
            {
                List<byte> receivedData = new List<byte>();
                DateTime startTimestamp = DateTime.UtcNow;

                while (DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds < timeout && receivedData.Count < MBAPHeaderLength)
                {
                    Memory<byte> buffer = new byte[300];
                    TimeSpan receiveTimeout = TimeSpan.FromMilliseconds(timeout).Subtract(DateTime.UtcNow.Subtract(startTimestamp));

                    if (receiveTimeout.TotalMilliseconds >= 50)
                    {
                        int receivedBytes = await _client.ReceiveAsync(buffer, receiveTimeout, cancellationToken);

                        if (receivedBytes > 0)
                        {
                            receivedData.AddRange(buffer.Slice(0, receivedBytes).ToArray());

                            result.Bytes += receivedBytes;
                            result.Packets += 1;
                        }
                    }
                }

                if (receivedData.Count == 0)
                {
                    throw new ModbusException("Failed to Receive RTU Message from Modbus TCP Device '" + RemoteHost + ":" + Port + "' - No Data was Received");
                }

                if (receivedData.Count < MBAPHeaderLength)
                {
                    throw new ModbusException("Failed to Receive RTU Message within the Timeout Period from Modbus TCP Device '" + RemoteHost + ":" + Port + "'");
                }

                if (receivedData[2] != 0 || receivedData[3] != 0)
                {
                    throw new ModbusException("Failed to Receive RTU Message from Modbus TCP Device  '" + RemoteHost + ":" + Port + "' - The TCP Header was Invalid");
                }

                if (BitConverter.ToUInt16(receivedData.GetRange(0, 2).ToArray(), 0) != _requestId)
                {
                    throw new ModbusException("Failed to Receive RTU Message from Modbus TCP Device  '" + RemoteHost + ":" + Port + "' - The TCP Header Transaction ID did not Match");
                }

                if (receivedData[6] != unitId)
                {
                    throw new ModbusException("Failed to Receive RTU Message from Modbus TCP Device  '" + RemoteHost + ":" + Port + "' - The TCP Header Unit ID did not Match");
                }

                byte[] mbapHeader = receivedData.GetRange(0, MBAPHeaderLength).ToArray();

                int tcpMessageDataLength = BitConverter.ToUInt16(new byte[] { receivedData[5], receivedData[4] }) - 1;

                if (tcpMessageDataLength <= 0 || tcpMessageDataLength > byte.MaxValue)
                {
                    throw new ModbusException("Failed to Receive RTU Message from Modbus TCP Device  '" + RemoteHost + ":" + Port + "' - The TCP Message Length was Invalid");
                }

                receivedData.RemoveRange(0, MBAPHeaderLength);

                if (receivedData.Count < tcpMessageDataLength)
                {
                    startTimestamp = DateTime.UtcNow;

                    while (DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds < timeout && receivedData.Count < tcpMessageDataLength)
                    {
                        Memory<byte> buffer = new byte[300];
                        TimeSpan receiveTimeout = TimeSpan.FromMilliseconds(timeout).Subtract(DateTime.UtcNow.Subtract(startTimestamp));

                        if (receiveTimeout.TotalMilliseconds >= 50)
                        {
                            int receivedBytes = await _client.ReceiveAsync(buffer, receiveTimeout, cancellationToken);

                            if (receivedBytes > 0)
                            {
                                receivedData.AddRange(buffer.Slice(0, receivedBytes).ToArray());
                            }

                            result.Bytes += receivedBytes;
                            result.Packets += 1;
                        }
                    }
                }

                if (receivedData.Count == 0)
                {
                    throw new ModbusException("Failed to Receive RTU Message from Modbus TCP Device  '" + RemoteHost + ":" + Port + "' - No Data was Received after TCP Header");
                }

                if (receivedData.Count < tcpMessageDataLength)
                {
                    throw new ModbusException("Failed to Receive RTU Message within the Timeout Period from Modbus TCP Device  '" + RemoteHost + ":" + Port + "'");
                }

                result.Message = receivedData.ToArray();
            }
            catch (TimeoutException)
            {
                throw new ModbusException("Failed to Receive RTU Message within the Timeout Period from Modbus TCP Device  '" + RemoteHost + ":" + Port + "'");
            }
            catch (System.Net.Sockets.SocketException e)
            {
                throw new ModbusException("Failed to Receive RTU Message from Modbus TCP Device  '" + RemoteHost + ":" + Port + "'", e);
            }

            return result;
        }

        private ReadOnlyMemory<byte> buildMBAPMessage(byte unitId, ReadOnlyMemory<byte> message)
        {
            List<byte> modbusAPMessage = new List<byte>();

            // Transaction Identifier
            modbusAPMessage.AddRange(BitConverter.GetBytes(getNextRequestId()));

            // Protocol Identifier
            modbusAPMessage.Add(0);
            modbusAPMessage.Add(0);

            // Length of Message
            modbusAPMessage.AddRange(BitConverter.GetBytes(Convert.ToUInt16(1 + message.Length)).Reverse()); // Unit ID + Message Data

            // Unit ID
            modbusAPMessage.Add(unitId);

            // Add Modbus PDU
            modbusAPMessage.AddRange(message.ToArray());

            return modbusAPMessage.ToArray();
        }

        private ushort getNextRequestId()
        {
            if (_requestId == ushort.MaxValue)
            {
                _requestId = ushort.MinValue;
            }
            else
            {
                _requestId++;
            }

            return _requestId;
        }

        #endregion
    }
}
