using System.IO;
using System.IO.Ports;

namespace TelemetryHil.Hardware
{
    /// <summary>
    /// ISerialDevice over a local serial port — EFM32 Pearl Gecko on
    /// /dev/ttyACM0 (Linux) or COMx (Windows), 115200 8N1.
    /// </summary>
    public class SerialPortDevice : SerialDeviceBase
    {
        private SerialPort? _port;

        protected override Task<Stream> OpenTransportAsync(string target, int baudRate, CancellationToken ct)
        {
            _port = new SerialPort(target, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = SerialPort.InfiniteTimeout,
                WriteTimeout = 2000,
            };
            _port.Open();
            return Task.FromResult(_port.BaseStream);
        }

        protected override void CloseTransport()
        {
            try { _port?.Close(); } catch (IOException) { }
            _port?.Dispose();
            _port = null;
        }
    }
}
