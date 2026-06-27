using System.IO.Ports;

namespace Porsche928Diagnostics.Protocol;

/// <summary>
/// Real FTDI FT232R serial port implementation of IKLineInterface.
/// Communicates with the Porsche 928 K-Line bus via the 19-pin diagnostic connector.
///
/// Signal polarity: K-Line uses Active-Low (idle = high). The FTDI adapter's
/// level-shifting circuit handles inversion, so software uses standard RS-232 polarity.
///
/// Hardware flow control MUST be disabled — RTS/DTR would interfere with the
/// K-Line bus voltage levels and corrupt 5-baud initialization.
/// </summary>
public sealed class KLineInterface : IKLineInterface
{
    private SerialPort? _port;
    private const int BaudRate = 10400;
    private const int InterByteDelayMs = 5;

    public bool IsOpen => _port?.IsOpen ?? false;
    public string PortName => _port?.PortName ?? string.Empty;

    public void Open(string portName)
    {
        _port = new SerialPort(portName)
        {
            BaudRate = BaudRate,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            RtsEnable = false,
            DtrEnable = false,
            ReadTimeout = 1000,
            WriteTimeout = 500
        };
        _port.Open();
        FlushReceiveBuffer();
    }

    public void Close()
    {
        _port?.Close();
        _port?.Dispose();
        _port = null;
    }

    /// <summary>
    /// Manually transmits one byte at 5 Baud using serial port BREAK state toggling.
    ///
    /// At 5 baud, each bit occupies exactly 200ms (1000ms / 5bits).
    /// Bit order: Start bit (low), D0..D7 (LSB first), Stop bit (high).
    ///
    /// BreakState = true  → TXD held LOW  (logic 0 / space)
    /// BreakState = false → TXD returns HIGH (logic 1 / mark / idle)
    ///
    /// Thread.Sleep is used deliberately — this is a hardware timing requirement,
    /// not a performance concern. Task.Delay has insufficient precision at 200ms granularity.
    /// </summary>
    public void SendByte5Baud(byte b)
    {
        EnsureOpen();
        const int BitPeriodMs = 200;

        // Start bit: low
        _port!.BreakState = true;
        Thread.Sleep(BitPeriodMs);

        // 8 data bits, LSB first
        for (int i = 0; i < 8; i++)
        {
            bool bitIsOne = (b & (1 << i)) != 0;
            _port.BreakState = !bitIsOne; // true=low=0, false=high=1
            Thread.Sleep(BitPeriodMs);
        }

        // Stop bit: high (idle)
        _port.BreakState = false;
        Thread.Sleep(BitPeriodMs);
    }

    public void SendRaw(byte[] data)
    {
        EnsureOpen();
        foreach (var b in data)
        {
            _port!.Write([b], 0, 1);
            Thread.Sleep(InterByteDelayMs);
        }
    }

    public byte[] ReadBytes(int count, int timeoutMs = 1000)
    {
        EnsureOpen();
        _port!.ReadTimeout = timeoutMs;
        var buffer = new byte[count];
        int received = 0;
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (received < count)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"K-Line timeout: expected {count} bytes, got {received}");
            if (_port.BytesToRead > 0)
            {
                int read = _port.Read(buffer, received, count - received);
                received += read;
            }
            else
            {
                Thread.Sleep(1);
            }
        }
        return buffer;
    }

    public async Task<byte[]> SendFrameAsync(byte[] frame, int timeoutMs = 2000, CancellationToken ct = default)
    {
        EnsureOpen();
        return await Task.Run(() =>
        {
            FlushReceiveBuffer();
            SendRaw(frame);

            // Wait for format byte to determine response length
            var formatByte = ReadBytes(1, timeoutMs)[0];
            int responseDataLen = (formatByte & 0x3F); // SID + data bytes
            int totalLen = 1 + 2 + responseDataLen + 1; // format + src + dst + data + cs

            var rest = ReadBytes(totalLen - 1, timeoutMs);
            var full = new byte[totalLen];
            full[0] = formatByte;
            rest.CopyTo(full, 1);
            return full;
        }, ct);
    }

    public void FlushReceiveBuffer()
    {
        _port?.DiscardInBuffer();
        _port?.DiscardOutBuffer();
    }

    private void EnsureOpen()
    {
        if (_port is null || !_port.IsOpen)
            throw new InvalidOperationException("K-Line port is not open.");
    }

    public void Dispose() => Close();
}
