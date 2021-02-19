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

        private readonly ConcurrentDictionary<string, List<byte>> _channelDevices = new ConcurrentDictionary<string, List<byte>>();

        #endregion


        #region Public Properties

        public static SerialOverLANFactory Instance => _instance;

        public ConcurrentDictionary<string, SerialOverLANChannel> Channels => _channels;

        #endregion


        #region Constructors

        public SerialOverLANFactory()
        {
        }

        static SerialOverLANFactory()
        {
            _instance = new SerialOverLANFactory();
        }

        #endregion


        #region Public Methods

        public async Task<SerialOverLANChannel> GetOrCreate(byte unitId, string remoteHost, int port, int timeout, CancellationToken cancellationToken)
        {
            if(remoteHost == null)
            {
                throw new ArgumentNullException(nameof(remoteHost));
            }

            // TODO: Consider other checks for Unit ID, Remote Host, Port and Timeout

            string channelKey = getChannelKey(remoteHost, port);

            registerChannelDevice(unitId, channelKey);

            SerialOverLANChannel channel = _channels.GetOrAdd(channelKey, (key) =>
            {
                return new SerialOverLANChannel(remoteHost, port);
            });

            await channel.InitializeAsync(timeout, cancellationToken);

            return channel;
        }

        public bool TryRemove(byte unitId, string remoteHost, int port)
        {
            if(remoteHost == null)
            {
                return false;
            }

            // TODO: Consider other checks for Unit ID, Remote Host and Port

            string channelKey = getChannelKey(remoteHost, port);

            unregisterChannelDevice(unitId, channelKey);

            if(_channelDevices.TryGetValue(channelKey, out List<byte> devices) && devices.Count > 0)
            {
                return true;
            }

            _channelDevices.TryRemove(channelKey, out _);

            if(_channels.TryRemove(channelKey, out SerialOverLANChannel channel))
            {
                try
                {
                    channel.Dispose();
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        #endregion


        #region Private Methods

        private void registerChannelDevice(byte unitId, string channelKey)
        {
            List<byte> devices = _channelDevices.GetOrAdd(channelKey, (key) =>
            {
                return new List<byte>();
            });

            if(devices.Contains(unitId) == false)
            {
                devices.Add(unitId);
            }
        }

        private void unregisterChannelDevice(byte unitId, string channelKey)
        {
            if(_channelDevices.TryGetValue(channelKey, out List<byte> devices))
            {
                devices.RemoveAll(device => device == unitId);
            }
        }

        private static string getChannelKey(string remoteHost, int port)
        {
            return remoteHost + ":" + port.ToString();
        }

        #endregion
    }
}
