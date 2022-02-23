# RICADO.Modbus
A Modbus Communication Library for .NET 6 and .NET Standard 2.0 Applications


## Sample usage

```csharp
using RICADO.Modbus;

using (ModbusRTUDevice device = new ModbusRTUDevice(1, ConnectionMethod.TCP, "10.1.4.205", 8000, 5000, 3))
{
    await device.InitializeAsync(CancellationToken.None);
    ReadRegistersResult data = await device.ReadHoldingRegistersAsync(2000, 7, CancellationToken.None);
    foreach (var value in data.Values)
    {
        Console.Write(value);
    }
}
```
