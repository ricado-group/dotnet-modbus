using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RICADO.Modbus.Channels
{
    internal class SerialOverLANFactory
    {
        #region Private Fields

        private static readonly SerialOverLANFactory _instance;

        private readonly ConcurrentDictionary<string, SerialOverLANChannel> _channels = new ConcurrentDictionary<string, SerialOverLANChannel>();

        private readonly SemaphoreSlim _semaphore;

        #endregion


        #region Public Properties

        public static SerialOverLANFactory Instance => _instance;

        #endregion


        #region Constructors

        public SerialOverLANFactory()
        {
            _semaphore = new SemaphoreSlim(1, 1);
        }

        static SerialOverLANFactory()
        {
            _instance = new SerialOverLANFactory();
        }

        #endregion


        #region Public Methods

        public async Task<SerialOverLANChannel> GetOrCreate(Guid uniqueId, string remoteHost, int port, int timeout, CancellationToken cancellationToken)
        {
            if (uniqueId == Guid.Empty)
            {
                throw new ArgumentException("The Device Unique ID cannot be Empty", nameof(uniqueId));
            }

            if (remoteHost == null)
            {
                throw new ArgumentNullException(nameof(remoteHost));
            }

            if(remoteHost.Length == 0)
            {
                throw new ArgumentException("The Remote Host cannot be Empty", nameof(remoteHost));
            }

            if (port <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(port), "The Port cannot be less than 1");
            }

            string channelKey = getChannelKey(remoteHost, port);

            SerialOverLANChannel channel;

            try
            {
                if (!_semaphore.Wait(0))
                {
                    await _semaphore.WaitAsync(cancellationToken);
                }

                channel = _channels.GetOrAdd(channelKey, (key) =>
                {
                    return new SerialOverLANChannel(remoteHost, port);
                });

                channel.RegisterDevice(uniqueId);
            }
            finally
            {
                _semaphore.Release();
            }

            if (channel.IsInitialized == true)
            {
                return channel;
            }

            await channel.InitializeAsync(timeout, cancellationToken);

            return channel;
        }

        public async Task<bool> TryRemove(Guid uniqueId, string remoteHost, int port, CancellationToken cancellationToken)
        {
            if(uniqueId == Guid.Empty)
            {
                return false;
            }

            if (remoteHost == null || remoteHost.Length == 0)
            {
                return false;
            }

            if(port <= 0)
            {
                return false;
            }

            string channelKey = getChannelKey(remoteHost, port);

            try
            {
                if(!_semaphore.Wait(0))
                {
                    await _semaphore.WaitAsync(cancellationToken);
                }

                if(_channels.TryGetValue(channelKey, out SerialOverLANChannel existingChannel))
                {
                    existingChannel.UnregisterDevice(uniqueId);

                    if(existingChannel.RegisteredDevices.Count > 0)
                    {
                        return true;
                    }
                }

                if(_channels.TryRemove(channelKey, out SerialOverLANChannel removedChannel))
                {
                    try
                    {
                        removedChannel.Dispose();
                    }
                    catch
                    {
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        #endregion


        #region Private Methods

        private static string getChannelKey(string remoteHost, int port)
        {
            return remoteHost + ":" + port.ToString();
        }

        #endregion
    }
}
