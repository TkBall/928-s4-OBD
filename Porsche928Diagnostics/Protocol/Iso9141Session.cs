namespace Porsche928Diagnostics.Protocol;

/// <summary>
/// Implements the ISO 9141-2 slow-initialization handshake for Porsche 928 ECU modules.
///
/// Handshake sequence:
///   1. Send ECU address at 5 Baud (bit-banged via BreakState)
///   2. Wait W1 (60-300ms) then switch to 10,400 baud
///   3. Read sync byte 0x55
///   4. Read KW1, KW2
///   5. Wait W4 (25-50ms), transmit ~KW2
///   6. Read ~Address confirmation byte from ECU
///
/// Timing constants are per ISO 9141-2 Section 6.3.
/// </summary>
public sealed class Iso9141Session
{
    private readonly IKLineInterface _interface;

    public bool IsConnected { get; private set; }
    public byte EcuAddress { get; private set; }
    public byte KeyWord1 { get; private set; }
    public byte KeyWord2 { get; private set; }

    // ISO 9141-2 timing parameters (milliseconds)
    private const int W1Ms = 60;    // Tester wait after 5-baud transmission before ECU responds
    private const int W4Ms = 25;    // Tester wait before sending ~KW2

    public Iso9141Session(IKLineInterface kLine)
    {
        _interface = kLine;
    }

    /// <summary>
    /// Executes the full ISO 9141-2 slow-init handshake with the specified ECU address.
    /// Must be called before any framed communication with that ECU.
    /// Throws InvalidOperationException if the handshake fails at any step.
    /// </summary>
    public async Task InitializeAsync(byte ecuAddress, CancellationToken ct = default)
    {
        IsConnected = false;
        EcuAddress = ecuAddress;

        await Task.Run(() =>
        {
            _interface.FlushReceiveBuffer();

            // Step 1: Transmit address at 5 baud (200ms/bit × 10 bits = 2000ms total)
            _interface.SendByte5Baud(ecuAddress);

            // Step 2: W1 — allow ECU to detect wakeup and prepare response
            Thread.Sleep(W1Ms);

            // Step 3: Read sync byte — ECU transmits 0x55 at 10,400 baud
            var syncBytes = _interface.ReadBytes(1, 300);
            if (syncBytes[0] != 0x55)
                throw new InvalidOperationException(
                    $"ISO 9141-2 sync failed: expected 0x55, got 0x{syncBytes[0]:X2}");

            // Step 4: Read keyword bytes KW1, KW2
            KeyWord1 = _interface.ReadBytes(1, 300)[0];
            KeyWord2 = _interface.ReadBytes(1, 300)[0];

            // Step 5: W4 delay, then transmit bitwise NOT of KW2
            Thread.Sleep(W4Ms);
            byte notKw2 = (byte)~KeyWord2;
            _interface.SendRaw([notKw2]);

            // Step 6: ECU confirms by returning bitwise NOT of its address
            var confirmBytes = _interface.ReadBytes(1, 300);
            byte expectedConfirm = (byte)~ecuAddress;
            if (confirmBytes[0] != expectedConfirm)
                throw new InvalidOperationException(
                    $"ISO 9141-2 address confirm failed: expected 0x{expectedConfirm:X2}, got 0x{confirmBytes[0]:X2}");

            IsConnected = true;

        }, ct);
    }

    public void Disconnect()
    {
        IsConnected = false;
    }
}
