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
        private readonly object _isInitializedLock = new object();

        private readonly HashSet<Guid> _registeredDevices = new HashSet<Guid>();
        private readonly object _registeredDevicesLock = new object();

        private readonly SemaphoreSlim _initializeSemaphore;
        private readonly SemaphoreSlim _requestSemaphore;

        private DateTime _lastInitializeAttempt = DateTime.MinValue;
        private DateTime _lastMessageTimestamp = DateTime.MinValue;

        #endregion


        #region Internal Properties

        internal string RemoteHost => _remoteHost;

        internal int Port => _port;

        internal bool IsInitialized
        {
            get
            {
                lock(_isInitializedLock)
                {
                    return _isInitialized;
                }
            }
        }

#if NETSTANDARD
        internal HashSet<Guid> RegisteredDevices
#else
        internal IReadOnlySet<Guid> RegisteredDevices
#endif
        {
            get
            {
                lock(_registeredDevicesLock)
                {
                    return _registeredDevices;
                }
            }
        }

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
            try
            {
                _client?.Dispose();
            }
            catch
            {
            }
            finally
            {
                _client = null;
            }

            _initializeSemaphore?.Dispose();
            _requestSemaphore?.Dispose();

            lock(_isInitializedLock)
            {
                _isInitialized = false;
            }
        }

        #endregion


        #region Internal Methods

        public async Task InitializeAsync(int timeout, CancellationToken cancellationToken)
        {
            lock (_isInitializedLock)
            {
                if (_isInitialized)
                {
                    return;
                }
            }

            if (!_initializeSemaphore.Wait(0))
            {
                await _initializeSemaphore.WaitAsync(cancellationToken);
            }

            try
            {
                if (IsInitialized)
                {
                    return;
                }

                int retrySeconds = RegisteredDevices.Count < 10 ? RegisteredDevices.Count : 10;

                if (RegisteredDevices.Count == 1 || DateTime.UtcNow.Subtract(_lastInitializeAttempt).TotalSeconds >= retrySeconds)
                {
                    _lastInitializeAttempt = DateTime.UtcNow;

                    cancellationToken.ThrowIfCancellationRequested();

                    destroyClient();

                    await initializeClient(timeout, cancellationToken);
                }
                else
                {
                    throw new ModbusException("Too Many Initialize Attempts for the Serial Over LAN Channel '" + RemoteHost + ":" + Port + "' - Retry after " + retrySeconds + " seconds");
                }
            }
            finally
            {
                _initializeSemaphore.Release();
            }

            lock (_isInitializedLock)
            {
                _isInitialized = true;
            }
        }

        public async Task<ProcessRequestResult> ProcessRequestAsync(RTURequest request, int timeout, int retries, int? delayBetweenMessages, CancellationToken cancellationToken)
        {
            int attempts = 0;
            int bytesSent = 0;
            int packetsSent = 0;
            int bytesReceived = 0;
            int packetsReceived = 0;
            DateTime startTimestamp = DateTime.UtcNow;

#if NETSTANDARD
            byte[] responseMessage = Array.Empty<byte>();
#else
            Memory<byte> responseMessage = new Memory<byte>();
#endif

            while (attempts <= retries)
            {
                if (!_requestSemaphore.Wait(0))
                {
                    await _requestSemaphore.WaitAsync(cancellationToken);
                }

                try
                {
                    if (attempts > 0)
                    {
                        await destroyAndInitializeClient(request.UnitID, timeout, cancellationToken);
                    }

                    // Build the Request into a Message we can Send
#if NETSTANDARD
                    byte[] requestMessage = request.BuildMessage();
#else
                    ReadOnlyMemory<byte> requestMessage = request.BuildMessage();
#endif

                    TimeSpan timeSinceLastMessage = DateTime.UtcNow.Subtract(_lastMessageTimestamp);

                    if (delayBetweenMessages.HasValue && delayBetweenMessages.Value > 0 && timeSinceLastMessage.TotalMilliseconds < delayBetweenMessages.Value)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(delayBetweenMessages.Value - timeSinceLastMessage.TotalMilliseconds), cancellationToken);
                    }

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
                catch (Exception)
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

        public void RegisterDevice(Guid uniqueId)
        {
            lock(_registeredDevicesLock)
            {
                if(_registeredDevices.Contains(uniqueId) == false)
                {
                    _registeredDevices.Add(uniqueId);
                }
            }
        }

        public void UnregisterDevice(Guid uniqueId)
        {
            lock(_registeredDevicesLock)
            {
                _registeredDevices.RemoveWhere(id => id == uniqueId);
            }
        }

        #endregion


        #region Private Methods

        private Task initializeClient(int timeout, CancellationToken cancellationToken)
        {
            _client = new TcpClient(RemoteHost, Port);

            return _client.ConnectAsync(timeout, cancellationToken);
        }

        private void destroyClient()
        {
            try
            {
                _client?.Dispose();
            }
            finally
            {
                _client = null;
            }
        }

        private async Task destroyAndInitializeClient(byte unitId, int timeout, CancellationToken cancellationToken)
        {
            destroyClient();

            try
            {
                await initializeClient(timeout, cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                throw new ModbusException("Failed to Re-Connect to Modbus Device ID '" + unitId + "' on '" + RemoteHost + ":" + Port + "' - The underlying Socket Connection has been Closed");
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

#if NETSTANDARD
        private async Task<SendMessageResult> sendMessageAsync(byte unitId, byte[] message, int timeout, CancellationToken cancellationToken)
#else
        private async Task<SendMessageResult> sendMessageAsync(byte unitId, ReadOnlyMemory<byte> message, int timeout, CancellationToken cancellationToken)
#endif
        {
            SendMessageResult result = new SendMessageResult
            {
                Bytes = 0,
                Packets = 0,
            };

#if NETSTANDARD
            byte[] modbusMessage = buildSerialMessage(unitId, message);
#else
            ReadOnlyMemory<byte> modbusMessage = buildSerialMessage(unitId, message);
#endif

            try
            {
                result.Bytes += await _client.SendAsync(modbusMessage, timeout, cancellationToken);
                result.Packets += 1;
            }
            catch (ObjectDisposedException)
            {
                throw new ModbusException("Failed to Send RTU Message to Modbus Device ID '" + unitId + "' on '" + RemoteHost + ":" + Port + "' - The underlying Socket Connection has been Closed");
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
#if NETSTANDARD
            ReceiveMessageResult result = new ReceiveMessageResult
            {
                Bytes = 0,
                Packets = 0,
                Message = Array.Empty<byte>(),
            };
#else
            ReceiveMessageResult result = new ReceiveMessageResult
            {
                Bytes = 0,
                Packets = 0,
                Message = new Memory<byte>(),
            };
#endif

            try
            {
                List<byte> receivedData = new List<byte>();
                DateTime startTimestamp = DateTime.UtcNow;

                while (DateTime.UtcNow.Subtract(startTimestamp).TotalMilliseconds < timeout && receivedData.Count < RTUResponse.GetMessageLengthHint(request, receivedData) + 3)
                {
#if NETSTANDARD
                    byte[] buffer = new byte[300];
#else
                    Memory<byte> buffer = new byte[300];
#endif
                    TimeSpan receiveTimeout = TimeSpan.FromMilliseconds(timeout).Subtract(DateTime.UtcNow.Subtract(startTimestamp));

                    if (receiveTimeout.TotalMilliseconds >= 50)
                    {
                        int receivedBytes = await _client.ReceiveAsync(buffer, receiveTimeout, cancellationToken);

                        if (receivedBytes > 0)
                        {
#if NETSTANDARD
                            receivedData.AddRange(buffer.Take(receivedBytes));
#else
                            receivedData.AddRange(buffer.Slice(0, receivedBytes).ToArray());
#endif

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
            catch (ObjectDisposedException)
            {
                throw new ModbusException("Failed to Receive RTU Message from Modbus Device ID '" + unitId + "' on '" + RemoteHost + ":" + Port + "' - The underlying Socket Connection has been Closed");
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

#if NETSTANDARD
        private byte[] buildSerialMessage(byte unitId, byte[] message)
#else
        private ReadOnlyMemory<byte> buildSerialMessage(byte unitId, ReadOnlyMemory<byte> message)
#endif
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
