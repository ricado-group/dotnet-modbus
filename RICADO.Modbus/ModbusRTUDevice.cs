using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RICADO.Modbus.Channels;
using RICADO.Modbus.Requests;
using RICADO.Modbus.Responses;

namespace RICADO.Modbus
{
    public class ModbusRTUDevice : IDisposable
    {
        #region Constants

        public const ushort MaximumAddress = 0xFFFF;

        public const ushort MaximumCoilsReadLength = 2000;
        public const ushort MaximumCoilsWriteLength = 1968;

        public const ushort MaximumRegistersReadLength = 125;
        public const ushort MaximumRegistersWriteLength = 123;

        #endregion


        #region Private Fields

        private readonly byte _unitId;
        private readonly ConnectionMethod _connectionMethod;
        private readonly string _remoteHost;
        private readonly int _port;
        private int _timeout;
        private int _retries;
        private int? _delayBetweenMessages;

        private bool _isInitialized = false;
        private readonly object _isInitializedLock = new object();

        private IChannel _channel;

        private readonly Guid _internalUniqueId;

        #endregion


        #region Internal Properties

        internal IChannel Channel => _channel;

        internal Guid InternalUniqueID => _internalUniqueId;

        #endregion


        #region Public Properties

        public byte UnitID => _unitId;

        public ConnectionMethod ConnectionMethod => _connectionMethod;

        public string RemoteHost => _remoteHost;

        public int Port => _port;

        public int Timeout
        {
            get
            {
                return _timeout;
            }
            set
            {
                _timeout = value;
            }
        }

        public int Retries
        {
            get
            {
                return _retries;
            }
            set
            {
                _retries = value;
            }
        }

        public int? DelayBetweenMessages
        {
            get
            {
                return _delayBetweenMessages;
            }
            set
            {
                _delayBetweenMessages = value;
            }
        }

        public bool IsInitialized
        {
            get
            {
                lock(_isInitializedLock)
                {
                    return _isInitialized;
                }
            }
        }

        #endregion


        #region Constructors

        public ModbusRTUDevice(byte unitId, ConnectionMethod connectionMethod, string remoteHost, int port, int timeout = 2000, int retries = 1, int? delayBetweenMessages = null)
        {
            if(connectionMethod == ConnectionMethod.SerialOverLAN && unitId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitId), "The Unit ID for a Serial Over LAN Modbus Device cannot be '0'");
            }

            if (connectionMethod == ConnectionMethod.SerialOverLAN && unitId == 255)
            {
                throw new ArgumentOutOfRangeException(nameof(unitId), "The Unit ID for a Serial Over LAN Modbus Device cannot be '255'");
            }

            _unitId = unitId;

            _connectionMethod = connectionMethod;

            if (remoteHost == null)
            {
                throw new ArgumentNullException(nameof(remoteHost), "The Remote Host cannot be Null");
            }

            if (remoteHost.Length == 0)
            {
                throw new ArgumentException("The Remote Host cannot be Empty", nameof(remoteHost));
            }

            _remoteHost = remoteHost;

            if (port <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(port), "The Port cannot be less than 1");
            }

            _port = port;

            if (timeout <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "The Timeout Value cannot be less than 1");
            }

            _timeout = timeout;

            if (retries < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(retries), "The Retries Value cannot be Negative");
            }

            _retries = retries;

            _delayBetweenMessages = delayBetweenMessages;

            _internalUniqueId = Guid.NewGuid();
        }

        #endregion


        #region Public Methods

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            lock (_isInitializedLock)
            {
                if (_isInitialized == true)
                {
                    return;
                }
            }

            // Initialize the Channel
            if (_connectionMethod == ConnectionMethod.TCP)
            {
                try
                {
                    _channel = new EthernetChannel(_remoteHost, _port);

                    await _channel.InitializeAsync(_timeout, cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    throw new ModbusException("Failed to Create the Ethernet TCP Communication Channel for Modbus TCP Device'" + _remoteHost + ":" + _port + "' - The underlying Socket Connection has been Closed");
                }
                catch (TimeoutException)
                {
                    throw new ModbusException("Failed to Create the Ethernet TCP Communication Channel within the Timeout Period for Modbus TCP Device'" + _remoteHost + ":" + _port + "'");
                }
                catch (System.Net.Sockets.SocketException e)
                {
                    throw new ModbusException("Failed to Create the Ethernet TCP Communication Channel for Modbus TCP Device'" + _remoteHost + ":" + _port + "'", e);
                }
            }
            else if (_connectionMethod == ConnectionMethod.SerialOverLAN)
            {
                try
                {
                    _channel = await SerialOverLANFactory.Instance.GetOrCreate(_internalUniqueId, _remoteHost, _port, _timeout, cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    throw new ModbusException("Failed to Create the Serial Over LAN Communication Channel for Modbus Device ID '" + _unitId + "' on '" + _remoteHost + ":" + _port + "' - The underlying Socket Connection has been Closed");
                }
                catch (TimeoutException)
                {
                    throw new ModbusException("Failed to Create the Serial Over LAN Communication Channel within the Timeout Period for Modbus Device ID '" + _unitId + "' on '" + _remoteHost + ":" + _port + "'");
                }
                catch (System.Net.Sockets.SocketException e)
                {
                    throw new ModbusException("Failed to Create the Serial Over LAN Communication Channel for Modbus Device ID '" + _unitId + "' on '" + _remoteHost + ":" + _port + "'", e);
                }
            }

            lock(_isInitializedLock)
            {
                _isInitialized = true;
            }
        }

        public void Dispose()
        {
            if (_channel is EthernetChannel)
            {
                _channel.Dispose();

                _channel = null;
            }
            else if(_channel is SerialOverLANChannel)
            {
                _ = Task.Run(async () => { await SerialOverLANFactory.Instance.TryRemove(_internalUniqueId, _remoteHost, _port, CancellationToken.None); });
            }

            lock (_isInitializedLock)
            {
                _isInitialized = false;
            }
        }

        public Task<ReadCoilsResult> ReadHoldingCoilAsync(ushort address, CancellationToken cancellationToken)
        {
            return ReadHoldingCoilsAsync(address, 1, cancellationToken);
        }

        public async Task<ReadCoilsResult> ReadHoldingCoilsAsync(ushort startAddress, ushort length, CancellationToken cancellationToken)
        {
            lock(_isInitializedLock)
            {
                if(_isInitialized == false)
                {
                    throw new ModbusException("This Modbus RTU Device must be Initialized first before any Requests can be Processed");
                }
            }
            
            if(startAddress > MaximumAddress)
            {
                throw new ArgumentOutOfRangeException(nameof(startAddress), "The Start Address is greater than the Maximum Allowed Value of '" + MaximumAddress + "'");
            }

            if (length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "The Length cannot be Zero");
            }

            if (length > MaximumCoilsReadLength)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "The Length is greater than the Maximum Allowed Value of '" + MaximumCoilsReadLength + "'");
            }

            ReadHoldingCoilsRequest request = ReadHoldingCoilsRequest.CreateNew(this, startAddress, length);

            ProcessRequestResult requestResult = await _channel.ProcessRequestAsync(request, _timeout, _retries, _delayBetweenMessages, cancellationToken);

            return new ReadCoilsResult
            {
                BytesSent = requestResult.BytesSent,
                PacketsSent = requestResult.PacketsSent,
                BytesReceived = requestResult.BytesReceived,
                PacketsReceived = requestResult.PacketsReceived,
                Duration = requestResult.Duration,
                Values = ReadHoldingCoilsResponse.ExtractValues(request, requestResult.Response),
            };
        }

        public Task<ReadCoilsResult> ReadInputCoilAsync(ushort address, CancellationToken cancellationToken)
        {
            return ReadInputCoilsAsync(address, 1, cancellationToken);
        }

        public async Task<ReadCoilsResult> ReadInputCoilsAsync(ushort startAddress, ushort length, CancellationToken cancellationToken)
        {
            lock (_isInitializedLock)
            {
                if (_isInitialized == false)
                {
                    throw new ModbusException("This Modbus RTU Device must be Initialized first before any Requests can be Processed");
                }
            }

            if (startAddress > MaximumAddress)
            {
                throw new ArgumentOutOfRangeException(nameof(startAddress), "The Start Address is greater than the Maximum Allowed Value of '" + MaximumAddress + "'");
            }

            if (length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "The Length cannot be Zero");
            }

            if (length > MaximumCoilsReadLength)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "The Length is greater than the Maximum Allowed Value of '" + MaximumCoilsReadLength + "'");
            }

            ReadInputCoilsRequest request = ReadInputCoilsRequest.CreateNew(this, startAddress, length);

            ProcessRequestResult requestResult = await _channel.ProcessRequestAsync(request, _timeout, _retries, _delayBetweenMessages, cancellationToken);

            return new ReadCoilsResult
            {
                BytesSent = requestResult.BytesSent,
                PacketsSent = requestResult.PacketsSent,
                BytesReceived = requestResult.BytesReceived,
                PacketsReceived = requestResult.PacketsReceived,
                Duration = requestResult.Duration,
                Values = ReadInputCoilsResponse.ExtractValues(request, requestResult.Response),
            };
        }

        public Task<ReadRegistersResult> ReadHoldingRegisterAsync(ushort address, CancellationToken cancellationToken)
        {
            return ReadHoldingRegistersAsync(address, 1, cancellationToken);
        }

        public async Task<ReadRegistersResult> ReadHoldingRegistersAsync(ushort startAddress, ushort length, CancellationToken cancellationToken)
        {
            lock (_isInitializedLock)
            {
                if (_isInitialized == false)
                {
                    throw new ModbusException("This Modbus RTU Device must be Initialized first before any Requests can be Processed");
                }
            }

            if (startAddress > MaximumAddress)
            {
                throw new ArgumentOutOfRangeException(nameof(startAddress), "The Start Address is greater than the Maximum Allowed Value of '" + MaximumAddress + "'");
            }

            if (length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "The Length cannot be Zero");
            }

            if (length > MaximumRegistersReadLength)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "The Length is greater than the Maximum Allowed Value of '" + MaximumRegistersReadLength + "'");
            }

            ReadHoldingRegistersRequest request = ReadHoldingRegistersRequest.CreateNew(this, startAddress, length);

            ProcessRequestResult requestResult = await _channel.ProcessRequestAsync(request, _timeout, _retries, _delayBetweenMessages, cancellationToken);

            return new ReadRegistersResult
            {
                BytesSent = requestResult.BytesSent,
                PacketsSent = requestResult.PacketsSent,
                BytesReceived = requestResult.BytesReceived,
                PacketsReceived = requestResult.PacketsReceived,
                Duration = requestResult.Duration,
                Values = ReadHoldingRegistersResponse.ExtractValues(request, requestResult.Response),
            };
        }

        public Task<ReadRegistersResult> ReadInputRegisterAsync(ushort address, CancellationToken cancellationToken)
        {
            return ReadInputRegistersAsync(address, 1, cancellationToken);
        }

        public async Task<ReadRegistersResult> ReadInputRegistersAsync(ushort startAddress, ushort length, CancellationToken cancellationToken)
        {
            lock (_isInitializedLock)
            {
                if (_isInitialized == false)
                {
                    throw new ModbusException("This Modbus RTU Device must be Initialized first before any Requests can be Processed");
                }
            }

            if (startAddress > MaximumAddress)
            {
                throw new ArgumentOutOfRangeException(nameof(startAddress), "The Start Address is greater than the Maximum Allowed Value of '" + MaximumAddress + "'");
            }

            if (length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "The Length cannot be Zero");
            }

            if (length > MaximumRegistersReadLength)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "The Length is greater than the Maximum Allowed Value of '" + MaximumRegistersReadLength + "'");
            }

            ReadInputRegistersRequest request = ReadInputRegistersRequest.CreateNew(this, startAddress, length);

            ProcessRequestResult requestResult = await _channel.ProcessRequestAsync(request, _timeout, _retries, _delayBetweenMessages, cancellationToken);

            return new ReadRegistersResult
            {
                BytesSent = requestResult.BytesSent,
                PacketsSent = requestResult.PacketsSent,
                BytesReceived = requestResult.BytesReceived,
                PacketsReceived = requestResult.PacketsReceived,
                Duration = requestResult.Duration,
                Values = ReadInputRegistersResponse.ExtractValues(request, requestResult.Response),
            };
        }

        public async Task<WriteCoilsResult> WriteHoldingCoilAsync(bool value, ushort address, CancellationToken cancellationToken)
        {
            lock (_isInitializedLock)
            {
                if (_isInitialized == false)
                {
                    throw new ModbusException("This Modbus RTU Device must be Initialized first before any Requests can be Processed");
                }
            }

            if (address > MaximumAddress)
            {
                throw new ArgumentOutOfRangeException(nameof(address), "The Address is greater than the Maximum Allowed Value of '" + MaximumAddress + "'");
            }

            WriteHoldingCoilRequest request = WriteHoldingCoilRequest.CreateNew(this, address, value);

            ProcessRequestResult requestResult = await _channel.ProcessRequestAsync(request, _timeout, _retries, _delayBetweenMessages, cancellationToken);

            WriteHoldingCoilResponse.Validate(request, requestResult.Response);

            return new WriteCoilsResult
            {
                BytesSent = requestResult.BytesSent,
                PacketsSent = requestResult.PacketsSent,
                BytesReceived = requestResult.BytesReceived,
                PacketsReceived = requestResult.PacketsReceived,
                Duration = requestResult.Duration,
            };
        }

        public async Task<WriteCoilsResult> WriteHoldingCoilsAsync(bool[] values, ushort startAddress, CancellationToken cancellationToken)
        {
            lock (_isInitializedLock)
            {
                if (_isInitialized == false)
                {
                    throw new ModbusException("This Modbus RTU Device must be Initialized first before any Requests can be Processed");
                }
            }

            if (startAddress > MaximumAddress)
            {
                throw new ArgumentOutOfRangeException(nameof(startAddress), "The Start Address is greater than the Maximum Allowed Value of '" + MaximumAddress + "'");
            }

            if (values.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(values), "The Values Array cannot be Empty");
            }

            if (values.Length > MaximumCoilsWriteLength)
            {
                throw new ArgumentOutOfRangeException(nameof(values), "The Values Array Length was greater than the Maximum Allowed of '" + MaximumCoilsWriteLength + "'");
            }

            WriteHoldingCoilsRequest request = WriteHoldingCoilsRequest.CreateNew(this, startAddress, values);

            ProcessRequestResult requestResult = await _channel.ProcessRequestAsync(request, _timeout, _retries, _delayBetweenMessages, cancellationToken);

            WriteHoldingCoilsResponse.Validate(request, requestResult.Response);

            return new WriteCoilsResult
            {
                BytesSent = requestResult.BytesSent,
                PacketsSent = requestResult.PacketsSent,
                BytesReceived = requestResult.BytesReceived,
                PacketsReceived = requestResult.PacketsReceived,
                Duration = requestResult.Duration,
            };
        }

        public async Task<WriteRegistersResult> WriteHoldingRegisterAsync(short value, ushort address, CancellationToken cancellationToken)
        {
            lock (_isInitializedLock)
            {
                if (_isInitialized == false)
                {
                    throw new ModbusException("This Modbus RTU Device must be Initialized first before any Requests can be Processed");
                }
            }

            if (address > MaximumAddress)
            {
                throw new ArgumentOutOfRangeException(nameof(address), "The Address is greater than the Maximum Allowed Value of '" + MaximumAddress + "'");
            }

            WriteHoldingRegisterRequest request = WriteHoldingRegisterRequest.CreateNew(this, address, value);

            ProcessRequestResult requestResult = await _channel.ProcessRequestAsync(request, _timeout, _retries, _delayBetweenMessages, cancellationToken);

            WriteHoldingRegisterResponse.Validate(request, requestResult.Response);

            return new WriteRegistersResult
            {
                BytesSent = requestResult.BytesSent,
                PacketsSent = requestResult.PacketsSent,
                BytesReceived = requestResult.BytesReceived,
                PacketsReceived = requestResult.PacketsReceived,
                Duration = requestResult.Duration,
            };
        }

        public async Task<WriteRegistersResult> WriteHoldingRegistersAsync(short[] values, ushort startAddress, CancellationToken cancellationToken)
        {
            lock (_isInitializedLock)
            {
                if (_isInitialized == false)
                {
                    throw new ModbusException("This Modbus RTU Device must be Initialized first before any Requests can be Processed");
                }
            }

            if (startAddress > MaximumAddress)
            {
                throw new ArgumentOutOfRangeException(nameof(startAddress), "The Start Address is greater than the Maximum Allowed Value of '" + MaximumAddress + "'");
            }

            if (values.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(values), "The Values Array cannot be Empty");
            }

            if (values.Length > MaximumRegistersWriteLength)
            {
                throw new ArgumentOutOfRangeException(nameof(values), "The Values Array Length was greater than the Maximum Allowed of '" + MaximumRegistersWriteLength + "'");
            }

            WriteHoldingRegistersRequest request = WriteHoldingRegistersRequest.CreateNew(this, startAddress, values);

            ProcessRequestResult requestResult = await _channel.ProcessRequestAsync(request, _timeout, _retries, _delayBetweenMessages, cancellationToken);

            WriteHoldingRegistersResponse.Validate(request, requestResult.Response);

            return new WriteRegistersResult
            {
                BytesSent = requestResult.BytesSent,
                PacketsSent = requestResult.PacketsSent,
                BytesReceived = requestResult.BytesReceived,
                PacketsReceived = requestResult.PacketsReceived,
                Duration = requestResult.Duration,
            };
        }

        #endregion
    }
}
