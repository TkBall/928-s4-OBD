namespace Porsche928Diagnostics.Protocol;

/// <summary>
/// Abstraction over the K-Line serial transport. After a session is initialized
/// by Iso9141Session, all ECU modules use this interface for framed communication.
/// </summary>
public interface IKLineInterface : IDisposable
{
    bool IsOpen { get; }
    string PortName { get; }

    /// <summary>Opens the serial port at 10,400 baud, 8N1, no flow control.</summary>
    void Open(string portName);

    void Close();

    /// <summary>
    /// Sends raw bytes to the K-Line with an inter-byte delay of at least 5ms
    /// to respect ISO 9141-2 P4 timing (tester inter-byte gap).
    /// </summary>
    void SendRaw(byte[] data);

    /// <summary>
    /// Reads exactly <paramref name="count"/> bytes with a timeout.
    /// Throws TimeoutException if bytes do not arrive within the timeout.
    /// </summary>
    byte[] ReadBytes(int count, int timeoutMs = 1000);

    /// <summary>
    /// Sends a built frame and reads the ECU's response frame.
    /// Returns the raw response byte array (including checksum byte).
    /// </summary>
    Task<byte[]> SendFrameAsync(byte[] frame, int timeoutMs = 2000, CancellationToken ct = default);

    /// <summary>
    /// For 5-baud initialization: manually transmits one byte at 5 Baud
    /// by toggling the serial port BREAK signal. Each bit occupies 200ms.
    /// </summary>
    void SendByte5Baud(byte b);

    /// <summary>
    /// Flushes the receive buffer. Used before and after 5-baud init.
    /// </summary>
    void FlushReceiveBuffer();
}
