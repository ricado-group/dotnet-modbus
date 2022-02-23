using System;
using System.Threading;
using System.Threading.Tasks;
using RICADO.Modbus.Requests;

namespace RICADO.Modbus.Channels
{
    internal interface IChannel : IDisposable
    {
        Task InitializeAsync(int timeout, CancellationToken cancellationToken);

        Task<ProcessRequestResult> ProcessRequestAsync(RTURequest request, int timeout, int retries, int? delayBetweenMessages, CancellationToken cancellationToken);
    }
}
