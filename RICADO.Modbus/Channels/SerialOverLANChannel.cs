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
    internal class SerialOverLANChannel : IChannel
    {
        #region Private Fields

        private readonly string _remoteHost;
        private readonly int _port;

        private TcpClient _client;

        private bool _isInitialized = false;

        private readonly SemaphoreSlim _initializeSemaphore;
        private readonly SemaphoreSlim _requestSemaphore;

        #endregion


        #region Internal Properties

        internal string RemoteHost => _remoteHost;

        internal int Port => _port;

        #endregion


        #region Constructors

        internal SerialOverLANChannel(string remoteHost, int port)
        {
            _remoteHost = remoteHost;
            _port = port;

            _initializeSemaphore = new SemaphoreSlim(1, 1);
            _requestSemaphore = new SemaphoreSlim(1, 1);
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

            _initializeSemaphore?.Dispose();
            _requestSemaphore?.Dispose();
        }

        #endregion


        #region Internal Methods

        public async Task InitializeAsync(int timeout, CancellationToken cancellationToken)
        {
            try
            {
                await _initializeSemaphore.WaitAsync(cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if(_isInitialized == true)
                {
                    return;
                }
                
                await initializeClient(timeout, cancellationToken);

                _isInitialized = true;
            }
            finally
            {
                _initializeSemaphore.Release();
            }
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
                    await _requestSemaphore.WaitAsync(cancellationToken);

                    if (attempts > 0)
                    {
                        await destroyAndInitializeClient(request.UnitID, timeout, cancellationToken);
                    }

                    // Build the Request into a Message we can Send
                    ReadOnlyMemory<byte> requestMessage = request.BuildMessage();

                    // Send the Message
                    SendMessageResult sendResult = await sendMessageAsync(request.UnitID, requestMessage, timeout, cancellationToken);

                    bytesSent += sendResult.Bytes;
                    packetsSent += sendResult.Packets;

                    // Receive a Response
                    ReceiveMessageResult receiveResult = await receiveMessageAsync(request, request.UnitID, timeout, cancellationToken);

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
                    _requestSemaphore.Release();
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
                throw new ModbusException("Received an RTU Error Response from Modbus Device ID '" + request.UnitID + "' on '" + _remoteHost + ":" + _port + "'", e);
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

        private async Task destroyAndInitializeClient(byte unitId, int timeout, CancellationToken cancellationToken)
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
                throw new ModbusException("Failed to Re-Connect within the Timeout Period to Modbus Device ID '" + unitId + "' on '" + RemoteHost + ":" + Port + "'");
            }
            catch (System.Net.Sockets.SocketException e)
            {
                throw new ModbusException("Failed to Re-Connect to Modbus Device ID '" + unitId + "' on '" + RemoteHost + ":" + Port + "'", e);
            }
        }

        private async Task<SendMessageResult> sendMessageAsync(byte unitId, ReadOnlyMemory<byte> message, int timeout, CancellationToken cancellationToken)
        {
            SendMessageResult result = new SendMessageResult
            {
                Bytes = 0,
                Packets = 0,
            };

            ReadOnlyMemory<byte> modbusMessage = buildSerialMessage(unitId, message);

            try
            {
                result.Bytes += await _client.SendAsync(modbusMessage, timeout, cancellationToken);
                result.Packets += 1;
            }
            catch (TimeoutException)
            {
                throw new ModbusException("Failed to Send RTU Message within the Timeout Period to Modbus Device ID '" + unitId + "' on '" + RemoteHost + ":" + Port + "'");
            }
            catch (System.Net.Sockets.SocketException e)
            {
                throw new ModbusException("Failed to Send RTU Message to Modbus Device ID '" + unitId + "' on '" + RemoteHost + ":" + Port + "'", e);
            }

            return result;
        }

        private async Task<ReceiveMessageResult> receiveMessageAsync(RTURequest request, byte unitId, int timeout, CancellationToken cancellationToken)
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

                while (DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds < timeout && receivedData.Count < RTUResponse.GetMessageLengthHint(request, receivedData) + 3)
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
                    throw new ModbusException("Failed to Receive RTU Message from Modbus Device ID '" + unitId + "' on '" + RemoteHost + ":" + Port + "' - No Data was Received");
                }

                if (receivedData.Count < RTUResponse.GetMessageLengthHint(request, receivedData) + 3)
                {
                    throw new ModbusException("Failed to Receive RTU Message within the Timeout Period from Modbus Device ID '" + unitId + "' on '" + RemoteHost + ":" + Port + "'");
                }

                if (receivedData[0] != unitId)
                {
                    throw new ModbusException("Failed to Receive RTU Message from Modbus Device ID '" + unitId + "' on '" + RemoteHost + ":" + Port + "' - The Unit ID did not Match");
                }

                receivedData.RemoveRange(0, 1);

                receivedData.RemoveRange(receivedData.Count - 2, 2);

                result.Message = receivedData.ToArray();
            }
            catch (TimeoutException)
            {
                throw new ModbusException("Failed to Receive RTU Message within the Timeout Period from Modbus Device ID '" + unitId + "' on '" + RemoteHost + ":" + Port + "'");
            }
            catch (System.Net.Sockets.SocketException e)
            {
                throw new ModbusException("Failed to Receive RTU Message from Modbus Device ID '" + unitId + "' on '" + RemoteHost + ":" + Port + "'", e);
            }

            return result;
        }

        private ReadOnlyMemory<byte> buildSerialMessage(byte unitId, ReadOnlyMemory<byte> message)
        {
            List<byte> serialMessage = new List<byte>();

            // Unit ID
            serialMessage.Add(unitId);

            // Add Modbus PDU
            serialMessage.AddRange(message.ToArray());

            // Add CRC-16
            serialMessage.AddRange(serialMessage.CalculateCRC16());

            return serialMessage.ToArray();
        }

        #endregion
    }
}
