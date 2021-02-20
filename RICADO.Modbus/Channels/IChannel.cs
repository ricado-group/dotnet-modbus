using System;
using System.Threading;
using System.Threading.Tasks;
using RICADO.Modbus.Requests;

namespace RICADO.Modbus.Channels
{
    internal interface IChannel : IDisposable
    {
        public Task InitializeAsync(int timeout, CancellationToken cancellationToken);

        public Task<ProcessRequestResult> ProcessRequestAsync(RTURequest request, int timeout, int retries, int? delayBetweenMessages, CancellationToken cancellationToken);
    }
}
