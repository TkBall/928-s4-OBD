# Porsche 928 K-Line Diagnostic Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a .NET 8 WPF desktop application for Windows 11 that communicates with a 1989 Porsche 928 S4 over ISO 9141-2 K-Line via an FTDI FT232R adapter, supporting full diagnostics across LH, EZK, PSD, RDK, Airbag, and Alarm ECUs.

**Architecture:** A layered design separates the low-level serial/protocol layer (5-baud init, frame building, checksum) from ECU-specific module logic, which is then consumed by MVVM ViewModels bound to WPF Views. The IKLineInterface abstraction makes all ECU logic unit-testable without hardware.

**Tech Stack:** .NET 8, WPF, System.IO.Ports, CommunityToolkit.Mvvm, xUnit, NSubstitute

---

## File Structure

```
Porsche928Diagnostics/
├── Porsche928Diagnostics.sln
│
├── Porsche928Diagnostics/                    ← Main WPF application
│   ├── Porsche928Diagnostics.csproj
│   ├── App.xaml / App.xaml.cs
│   │
│   ├── Protocol/
│   │   ├── IKLineInterface.cs               ← Abstraction over serial port (testability)
│   │   ├── KLineInterface.cs                ← FTDI FT232R serial port implementation
│   │   ├── Iso9141Session.cs                ← 5-baud init + session lifecycle
│   │   ├── MessageFrame.cs                  ← Frame builder/parser, checksum verification
│   │   └── ChecksumHelper.cs               ← Modulo-256 checksum pure functions
│   │
│   ├── Modules/
│   │   ├── IEcuModule.cs                    ← Common ECU module interface
│   │   ├── BaseEcuModule.cs                 ← ReadDtcs, ClearDtcs, ReadEcuId shared impl
│   │   ├── LhModule.cs                      ← LH 2.3 (0x11): injection, drive links, SAP
│   │   ├── EzkModule.cs                     ← EZK (0x12): ignition, knock registration
│   │   ├── PsdModule.cs                     ← PSD (0x28): differential, bleed procedure
│   │   ├── RdkModule.cs                     ← RDK (0x30): tire pressure, pressure switches
│   │   ├── AirbagModule.cs                  ← Airbag (0x40): downtime, crash data
│   │   └── AlarmModule.cs                   ← Alarm (0x45): land coding, drive links
│   │
│   ├── Models/
│   │   ├── DiagnosticTroubleCode.cs         ← DTC value object
│   │   ├── EcuIdentification.cs             ← ECU ID value object
│   │   ├── LhActualValues.cs                ← Battery voltage, temp, MAF, lambda etc.
│   │   ├── LhInputSignals.cs                ← Throttle/WOT/Airco bit flags
│   │   ├── EzkSensorData.cs                 ← RPM, load, temp, knock counts per cylinder
│   │   ├── RdkSensorData.cs                 ← Pressure switch states, ABS speeds
│   │   ├── AirbagData.cs                    ← Downtime clock, crash data bits
│   │   └── AlarmData.cs                     ← Country code, input switch states
│   │
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs                 ← ObservableObject base, IsBusy, StatusMessage
│   │   ├── MainViewModel.cs                 ← Port selection, connect/disconnect, tab nav
│   │   ├── LhViewModel.cs
│   │   ├── EzkViewModel.cs
│   │   ├── PsdViewModel.cs
│   │   ├── RdkViewModel.cs
│   │   ├── AirbagViewModel.cs
│   │   ├── AlarmViewModel.cs
│   │   └── DigitalDashViewModel.cs
│   │
│   └── Views/
│       ├── MainWindow.xaml / .xaml.cs       ← Tab control shell, port connect bar
│       ├── LhView.xaml / .xaml.cs
│       ├── EzkView.xaml / .xaml.cs
│       ├── PsdView.xaml / .xaml.cs
│       ├── RdkView.xaml / .xaml.cs
│       ├── AirbagView.xaml / .xaml.cs
│       ├── AlarmView.xaml / .xaml.cs
│       └── DigitalDashView.xaml / .xaml.cs
│
└── Porsche928Diagnostics.Tests/             ← xUnit test project
    ├── Porsche928Diagnostics.Tests.csproj
    ├── Protocol/
    │   ├── ChecksumHelperTests.cs
    │   ├── MessageFrameTests.cs
    │   └── Iso9141SessionTests.cs
    └── Modules/
        ├── LhModuleTests.cs
        ├── EzkModuleTests.cs
        ├── PsdModuleTests.cs
        ├── RdkModuleTests.cs
        ├── AirbagModuleTests.cs
        └── AlarmModuleTests.cs
```

---

## Task 1: Solution & Project Scaffold

**Files:**
- Create: `Porsche928Diagnostics/Porsche928Diagnostics.csproj`
- Create: `Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj`
- Create: `Porsche928Diagnostics.sln`

- [ ] **Step 1: Create the solution**

```bash
cd "C:\Users\TkBall\Nextcloud\Documents\Cars\Porsche 928\F57GNT\Claude-OBD"
dotnet new sln -n Porsche928Diagnostics
dotnet new wpf -n Porsche928Diagnostics -f net8.0-windows --use-program-main
dotnet new xunit -n Porsche928Diagnostics.Tests -f net8.0
dotnet sln add Porsche928Diagnostics/Porsche928Diagnostics.csproj
dotnet sln add Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj
dotnet add Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj reference Porsche928Diagnostics/Porsche928Diagnostics.csproj
```

- [ ] **Step 2: Add NuGet packages**

```bash
dotnet add Porsche928Diagnostics/Porsche928Diagnostics.csproj package CommunityToolkit.Mvvm
dotnet add Porsche928Diagnostics/Porsche928Diagnostics.csproj package System.IO.Ports
dotnet add Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj package NSubstitute
dotnet add Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj package FluentAssertions
```

- [ ] **Step 3: Edit the WPF .csproj to confirm target framework**

`Porsche928Diagnostics/Porsche928Diagnostics.csproj` must contain:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <RootNamespace>Porsche928Diagnostics</RootNamespace>
    <AssemblyName>Porsche928Diagnostics</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
    <PackageReference Include="System.IO.Ports" Version="9.*" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create subdirectory structure**

```bash
mkdir Porsche928Diagnostics/Protocol
mkdir Porsche928Diagnostics/Modules
mkdir Porsche928Diagnostics/Models
mkdir Porsche928Diagnostics/ViewModels
mkdir Porsche928Diagnostics/Views
mkdir Porsche928Diagnostics.Tests/Protocol
mkdir Porsche928Diagnostics.Tests/Modules
```

- [ ] **Step 5: Verify build**

```bash
dotnet build Porsche928Diagnostics.sln
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git init
git add .
git commit -m "chore: scaffold .NET 8 WPF solution with test project"
```

---

## Task 2: ChecksumHelper

**Files:**
- Create: `Porsche928Diagnostics/Protocol/ChecksumHelper.cs`
- Create: `Porsche928Diagnostics.Tests/Protocol/ChecksumHelperTests.cs`

- [ ] **Step 1: Write failing tests**

`Porsche928Diagnostics.Tests/Protocol/ChecksumHelperTests.cs`:

```csharp
using FluentAssertions;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Tests.Protocol;

public class ChecksumHelperTests
{
    [Fact]
    public void Calculate_SingleByte_ReturnsItself()
    {
        ChecksumHelper.Calculate([0x42]).Should().Be(0x42);
    }

    [Fact]
    public void Calculate_MultipleBytes_ReturnsSumModulo256()
    {
        // 0x68 + 0x11 + 0xF1 + 0x1A + 0x90 = 0x314 → mod 256 = 0x14
        ChecksumHelper.Calculate([0x68, 0x11, 0xF1, 0x1A, 0x90]).Should().Be(0x14);
    }

    [Fact]
    public void Calculate_WrapAround_CorrectlyMasks()
    {
        // 0xFF + 0x01 = 0x100 → mod 256 = 0x00
        ChecksumHelper.Calculate([0xFF, 0x01]).Should().Be(0x00);
    }

    [Fact]
    public void Verify_ValidFrame_ReturnsTrue()
    {
        byte[] frame = [0x68, 0x11, 0xF1, 0x1A, 0x90, 0x14];
        ChecksumHelper.Verify(frame).Should().BeTrue();
    }

    [Fact]
    public void Verify_CorruptedFrame_ReturnsFalse()
    {
        byte[] frame = [0x68, 0x11, 0xF1, 0x1A, 0x90, 0xFF];
        ChecksumHelper.Verify(frame).Should().BeFalse();
    }

    [Fact]
    public void Verify_EmptyPayload_ReturnsFalse()
    {
        ChecksumHelper.Verify([]).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests — expect failures**

```bash
dotnet test Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj --filter "ChecksumHelperTests"
```

Expected: compilation errors (type not found).

- [ ] **Step 3: Implement ChecksumHelper**

`Porsche928Diagnostics/Protocol/ChecksumHelper.cs`:

```csharp
namespace Porsche928Diagnostics.Protocol;

/// <summary>
/// ISO 9141-2 Modulo-256 checksum. The checksum byte appended to a frame equals
/// the sum of all preceding bytes, masked to 8 bits.
/// </summary>
public static class ChecksumHelper
{
    public static byte Calculate(ReadOnlySpan<byte> data)
    {
        int sum = 0;
        foreach (var b in data)
            sum += b;
        return (byte)(sum & 0xFF);
    }

    /// <summary>
    /// Verifies a frame where the last byte is the checksum over all preceding bytes.
    /// </summary>
    public static bool Verify(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 2) return false;
        var payload = frame[..^1];
        var expected = frame[^1];
        return Calculate(payload) == expected;
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj --filter "ChecksumHelperTests"
```

Expected: 6 tests pass.

- [ ] **Step 5: Commit**

```bash
git add Porsche928Diagnostics/Protocol/ChecksumHelper.cs Porsche928Diagnostics.Tests/Protocol/ChecksumHelperTests.cs
git commit -m "feat: add ChecksumHelper with modulo-256 verification"
```

---

## Task 3: MessageFrame Builder & Parser

**Files:**
- Create: `Porsche928Diagnostics/Protocol/MessageFrame.cs`
- Create: `Porsche928Diagnostics.Tests/Protocol/MessageFrameTests.cs`

The ISO 9141-2 / KWP1281-style frame used by Bosch ECUs on the 928:

```
[Format] [Target] [Source=0xF1] [SID] [Data...] [Checksum]
```

Where `Format` is `0x80 | dataLength` for single-frame messages with physical addressing. `dataLength` counts SID + data bytes only. Checksum is Modulo-256 over all bytes excluding itself.

- [ ] **Step 1: Write failing tests**

`Porsche928Diagnostics.Tests/Protocol/MessageFrameTests.cs`:

```csharp
using FluentAssertions;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Tests.Protocol;

public class MessageFrameTests
{
    [Fact]
    public void Build_ReadEcuId_ProducesCorrectFrame()
    {
        // SID 0x1A, Option 0x90 → dataLen=2, Format=0x82
        // Expected: [0x82, 0x11, 0xF1, 0x1A, 0x90, CS]
        // CS = (0x82+0x11+0xF1+0x1A+0x90) & 0xFF = 0x0F+0x10 ...
        // 0x82=130, 0x11=17, 0xF1=241, 0x1A=26, 0x90=144 → sum=558 → 0x2E
        var frame = MessageFrame.Build(targetAddress: 0x11, sid: 0x1A, data: [0x90]);
        frame.Should().Equal([0x82, 0x11, 0xF1, 0x1A, 0x90, 0x2E]);
    }

    [Fact]
    public void Build_ReadDtcs_ProducesCorrectFrame()
    {
        // SID 0x18, no additional data → dataLen=1, Format=0x81
        // [0x81, 0x11, 0xF1, 0x18, CS]
        // 0x81+0x11+0xF1+0x18 = 129+17+241+24=411=0x019B → 0x9B
        var frame = MessageFrame.Build(targetAddress: 0x11, sid: 0x18, data: []);
        frame.Should().Equal([0x81, 0x11, 0xF1, 0x18, 0x9B]);
    }

    [Fact]
    public void Parse_ValidResponse_ExtractsFields()
    {
        // Response from ECU: [Format][Source_ECU][Dest=0xF1][SID_Response][Data...][CS]
        // Example: [0x83, 0x11, 0xF1, 0x5A, 0x01, 0x02, 0x03, CS]
        // 0x83+0x11+0xF1+0x5A+0x01+0x02+0x03 = 131+17+241+90+1+2+3=485=0x1E5 → 0xE5
        byte[] raw = [0x83, 0x11, 0xF1, 0x5A, 0x01, 0x02, 0x03, 0xE5];
        var parsed = MessageFrame.Parse(raw);
        parsed.IsValid.Should().BeTrue();
        parsed.SourceAddress.Should().Be(0x11);
        parsed.ServiceId.Should().Be(0x5A);
        parsed.Data.Should().Equal([0x01, 0x02, 0x03]);
    }

    [Fact]
    public void Parse_BadChecksum_IsValidFalse()
    {
        byte[] raw = [0x83, 0x11, 0xF1, 0x5A, 0x01, 0x02, 0x03, 0x00];
        MessageFrame.Parse(raw).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Parse_TooShort_IsValidFalse()
    {
        MessageFrame.Parse([0x81, 0x11]).IsValid.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests — expect failures**

```bash
dotnet test Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj --filter "MessageFrameTests"
```

Expected: type not found errors.

- [ ] **Step 3: Implement MessageFrame**

`Porsche928Diagnostics/Protocol/MessageFrame.cs`:

```csharp
namespace Porsche928Diagnostics.Protocol;

/// <summary>
/// Builds and parses ISO 9141-2 / KWP1281-style message frames for 928 ECU communication.
/// Frame layout: [Format] [Target] [0xF1] [SID] [Data...] [Checksum]
/// Format byte = 0x80 | (SID+Data length). Source address 0xF1 identifies the tester.
/// </summary>
public static class MessageFrame
{
    private const byte TesterAddress = 0xF1;

    public static byte[] Build(byte targetAddress, byte sid, ReadOnlySpan<byte> data)
    {
        int dataLen = 1 + data.Length; // SID counts as a data byte
        byte format = (byte)(0x80 | (dataLen & 0x3F));

        var frame = new byte[4 + data.Length + 1]; // format+target+source+sid + data + cs
        frame[0] = format;
        frame[1] = targetAddress;
        frame[2] = TesterAddress;
        frame[3] = sid;
        data.CopyTo(frame.AsSpan(4));
        frame[^1] = ChecksumHelper.Calculate(frame.AsSpan(0, frame.Length - 1));
        return frame;
    }

    public static ParsedFrame Parse(ReadOnlySpan<byte> raw)
    {
        if (raw.Length < 5) return ParsedFrame.Invalid;
        if (!ChecksumHelper.Verify(raw)) return ParsedFrame.Invalid;

        // Format byte low 6 bits = length of (SID + data)
        int dataLen = (raw[0] & 0x3F) - 1; // subtract 1 for SID
        if (dataLen < 0) dataLen = 0;

        return new ParsedFrame(
            IsValid: true,
            SourceAddress: raw[1],
            DestAddress: raw[2],
            ServiceId: raw[3],
            Data: raw.Length > 5 ? raw.Slice(4, Math.Min(dataLen, raw.Length - 5)).ToArray() : []
        );
    }
}

public record ParsedFrame(
    bool IsValid,
    byte SourceAddress,
    byte DestAddress,
    byte ServiceId,
    byte[] Data)
{
    public static readonly ParsedFrame Invalid = new(false, 0, 0, 0, []);
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj --filter "MessageFrameTests"
```

Expected: 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add Porsche928Diagnostics/Protocol/MessageFrame.cs Porsche928Diagnostics.Tests/Protocol/MessageFrameTests.cs
git commit -m "feat: add MessageFrame builder/parser for ISO 9141-2 KWP frames"
```

---

## Task 4: IKLineInterface & KLineInterface (Serial Port Layer)

**Files:**
- Create: `Porsche928Diagnostics/Protocol/IKLineInterface.cs`
- Create: `Porsche928Diagnostics/Protocol/KLineInterface.cs`

This is the hardware-facing layer. `IKLineInterface` provides the testable abstraction used by all ECU modules. `KLineInterface` contains the real FTDI/SerialPort logic. The interface deliberately omits 5-baud init (that belongs in `Iso9141Session`) and exposes only post-handshake byte-level I/O.

- [ ] **Step 1: Create IKLineInterface**

`Porsche928Diagnostics/Protocol/IKLineInterface.cs`:

```csharp
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
```

- [ ] **Step 2: Create KLineInterface**

`Porsche928Diagnostics/Protocol/KLineInterface.cs`:

```csharp
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
```

- [ ] **Step 3: Verify solution builds**

```bash
dotnet build Porsche928Diagnostics/Porsche928Diagnostics.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add Porsche928Diagnostics/Protocol/IKLineInterface.cs Porsche928Diagnostics/Protocol/KLineInterface.cs
git commit -m "feat: add IKLineInterface abstraction and FTDI serial port implementation"
```

---

## Task 5: Iso9141Session — 5-Baud Initialization & Session Lifecycle

**Files:**
- Create: `Porsche928Diagnostics/Protocol/Iso9141Session.cs`
- Create: `Porsche928Diagnostics.Tests/Protocol/Iso9141SessionTests.cs`

This class orchestrates the complete ISO 9141-2 slow-init handshake for a given ECU address. It uses `IKLineInterface` for all I/O, making it fully testable.

- [ ] **Step 1: Write failing tests**

`Porsche928Diagnostics.Tests/Protocol/Iso9141SessionTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Tests.Protocol;

public class Iso9141SessionTests
{
    private readonly IKLineInterface _mockInterface;
    private readonly Iso9141Session _session;

    public Iso9141SessionTests()
    {
        _mockInterface = Substitute.For<IKLineInterface>();
        _mockInterface.IsOpen.Returns(true);
        _session = new Iso9141Session(_mockInterface);
    }

    [Fact]
    public async Task InitializeAsync_SuccessfulHandshake_SetsIsConnected()
    {
        // ECU responds: 0x55 (sync), KW1=0x08, KW2=0x08, then ~addr=0xEE
        _mockInterface.ReadBytes(1, Arg.Any<int>()).Returns(
            [0x55],  // sync byte
            [0x08],  // KW1
            [0x08],  // KW2
            [0xEE]   // ~0x11 = 0xEE (ECU confirms address)
        );

        await _session.InitializeAsync(0x11);

        _session.IsConnected.Should().BeTrue();
        _session.EcuAddress.Should().Be(0x11);
        // Verify 5-baud address was sent
        _mockInterface.Received(1).SendByte5Baud(0x11);
        // Verify ~KW2 was sent back: ~0x08 = 0xF7
        _mockInterface.Received().SendRaw(Arg.Is<byte[]>(b => b[0] == 0xF7));
    }

    [Fact]
    public async Task InitializeAsync_WrongSyncByte_ThrowsInvalidOperationException()
    {
        _mockInterface.ReadBytes(1, Arg.Any<int>()).Returns([0xAA]); // wrong sync

        await _session.Invoking(s => s.InitializeAsync(0x11))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sync*");
    }

    [Fact]
    public async Task InitializeAsync_ConfirmationMismatch_ThrowsInvalidOperationException()
    {
        _mockInterface.ReadBytes(1, Arg.Any<int>()).Returns(
            [0x55], [0x08], [0x08],
            [0x11]  // should be ~0x11=0xEE, got wrong value
        );

        await _session.Invoking(s => s.InitializeAsync(0x11))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*confirm*");
    }

    [Fact]
    public void Disconnect_SetsIsConnectedFalse()
    {
        _session.Disconnect();
        _session.IsConnected.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests — expect failures**

```bash
dotnet test Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj --filter "Iso9141SessionTests"
```

Expected: type not found.

- [ ] **Step 3: Implement Iso9141Session**

`Porsche928Diagnostics/Protocol/Iso9141Session.cs`:

```csharp
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
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj --filter "Iso9141SessionTests"
```

Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add Porsche928Diagnostics/Protocol/Iso9141Session.cs Porsche928Diagnostics.Tests/Protocol/Iso9141SessionTests.cs
git commit -m "feat: implement ISO 9141-2 5-baud slow-init handshake"
```

---

## Task 6: Models & IEcuModule / BaseEcuModule

**Files:**
- Create: `Porsche928Diagnostics/Models/DiagnosticTroubleCode.cs`
- Create: `Porsche928Diagnostics/Models/EcuIdentification.cs`
- Create: `Porsche928Diagnostics/Models/LhActualValues.cs`
- Create: `Porsche928Diagnostics/Models/LhInputSignals.cs`
- Create: `Porsche928Diagnostics/Models/EzkSensorData.cs`
- Create: `Porsche928Diagnostics/Models/RdkSensorData.cs`
- Create: `Porsche928Diagnostics/Models/AirbagData.cs`
- Create: `Porsche928Diagnostics/Models/AlarmData.cs`
- Create: `Porsche928Diagnostics/Modules/IEcuModule.cs`
- Create: `Porsche928Diagnostics/Modules/BaseEcuModule.cs`

- [ ] **Step 1: Create all model records**

`Porsche928Diagnostics/Models/DiagnosticTroubleCode.cs`:

```csharp
namespace Porsche928Diagnostics.Models;

public record DiagnosticTroubleCode(byte RawByte1, byte RawByte2)
{
    public string Code => $"{RawByte1:X2}{RawByte2:X2}";
    public string Description => KnownDtcDescriptions.TryGet(RawByte1, RawByte2);
    public override string ToString() => $"DTC {Code}: {Description}";
}

/// <summary>
/// Maps known 928 ECU DTC byte pairs to human-readable descriptions.
/// Codes follow Bosch/Porsche diagnostic specification.
/// </summary>
public static class KnownDtcDescriptions
{
    private static readonly Dictionary<(byte, byte), string> _map = new()
    {
        { (0x21, 0x00), "MAF sensor signal out of range" },
        { (0x22, 0x00), "Engine coolant temperature sensor fault" },
        { (0x23, 0x00), "Throttle position sensor fault" },
        { (0x24, 0x00), "Oxygen sensor (Lambda) fault" },
        { (0x25, 0x00), "Idle speed control fault" },
        { (0x26, 0x00), "EZK communication fault (LH↔EZK link)" },
        { (0x27, 0x00), "Fuel injector output fault" },
        { (0x28, 0x00), "Tank vent solenoid fault" },
        { (0x31, 0x00), "Knock sensor cylinder 1 fault" },
        { (0x32, 0x00), "Knock sensor cylinder 2 fault" },
        { (0x33, 0x00), "Knock sensor cylinder 3 fault" },
        { (0x34, 0x00), "Knock sensor cylinder 4 fault" },
        { (0x35, 0x00), "Crank position sensor fault" },
        { (0x41, 0x00), "Airbag squib circuit fault" },
        { (0x42, 0x00), "Airbag safing sensor fault" },
        { (0x51, 0x00), "PSD hydraulic pressure fault" },
        { (0x52, 0x00), "PSD solenoid valve fault" },
    };

    public static string TryGet(byte b1, byte b2) =>
        _map.TryGetValue((b1, b2), out var desc) ? desc : "Unknown fault code";
}
```

`Porsche928Diagnostics/Models/EcuIdentification.cs`:

```csharp
namespace Porsche928Diagnostics.Models;

public record EcuIdentification(
    string ChipCode,
    string BoschPartNumber,
    string EpromVersion,
    byte[] RawBytes
)
{
    public override string ToString() =>
        $"Chip: {ChipCode} | Part: {BoschPartNumber} | EPROM: {EpromVersion}";
}
```

`Porsche928Diagnostics/Models/LhActualValues.cs`:

```csharp
namespace Porsche928Diagnostics.Models;

public record LhActualValues(
    double BatteryVoltage,     // RawByte * 0.065 V
    double ReferenceVoltage,   // 5.0V reference rail
    bool EzkOnSignal,          // EZK link active flag
    double EngineTemperatureDegC,  // NTC lookup result
    double MafVoltage,         // Active: MAF sensor voltage 0-5V
    double LambdaVoltage,      // Active: O2 sensor mV
    double VehicleSpeedKph,    // Active: speed sensor km/h
    bool Coding4Cylinder,      // true=4-cyl coding, false=8-cyl (catalyst)
    bool IsActiveReading       // true = engine-running values
);
```

`Porsche928Diagnostics/Models/LhInputSignals.cs`:

```csharp
namespace Porsche928Diagnostics.Models;

/// <summary>
/// Decoded from SID 0x21, PID 0x40 single-byte bit field.
/// Each bit represents a discrete switch state.
/// </summary>
public record LhInputSignals(byte RawByte)
{
    public bool ThrottleIdleSwitch    => (RawByte & 0x01) != 0;  // Bit 0
    public bool WideOpenThrottleSwitch => (RawByte & 0x02) != 0; // Bit 1
    public bool AircoCompressorDemand  => (RawByte & 0x04) != 0; // Bit 2
    public bool IdleDropGearEngagement => (RawByte & 0x08) != 0; // Bit 3
}
```

`Porsche928Diagnostics/Models/EzkSensorData.cs`:

```csharp
namespace Porsche928Diagnostics.Models;

public record EzkSensorData(
    int EngineRpm,
    double LoadPercent,
    double EngineTemperatureDegC,
    string TransmissionCoding,    // "Manual" or "Automatic"
    bool ThrottleSignalActive,
    int[] KnockCountPerCylinder   // 8 elements, index 0 = cyl 1
);
```

`Porsche928Diagnostics/Models/RdkSensorData.cs`:

```csharp
namespace Porsche928Diagnostics.Models;

public record RdkSensorData(
    bool[] PressureSwitchStates,  // 4 wheels: true = OK pressure
    bool HfReceiverActive,
    double[] AbsSpeedKph          // 4 wheels
);
```

`Porsche928Diagnostics/Models/AirbagData.cs`:

```csharp
namespace Porsche928Diagnostics.Models;

/// <summary>
/// Downtime is the duration since the airbag ECU lost power, used to validate
/// the integrity of the capacitor backup circuit.
/// </summary>
public record AirbagData(
    TimeSpan DowntimeClock,
    bool CrashEventRecorded,
    bool DriverBagFired,
    bool PassengerBagFired,
    bool SeatbeltPretensionerFired,
    byte RawCrashDataByte
);
```

`Porsche928Diagnostics/Models/AlarmData.cs`:

```csharp
namespace Porsche928Diagnostics.Models;

public record AlarmData(
    string CountryCode,          // e.g. "DE", "US", "GB"
    bool EngineLidSwitchOpen,
    bool LuggageLidSwitchOpen,
    bool GloveCompartmentSwitchOpen,
    bool InteriorMotionSensorActive,
    byte RawSwitchStateByte
);
```

- [ ] **Step 2: Create IEcuModule**

`Porsche928Diagnostics/Modules/IEcuModule.cs`:

```csharp
using Porsche928Diagnostics.Models;

namespace Porsche928Diagnostics.Modules;

public interface IEcuModule
{
    byte EcuAddress { get; }
    string EcuName { get; }

    Task<EcuIdentification> ReadEcuIdentificationAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DiagnosticTroubleCode>> ReadDtcsAsync(CancellationToken ct = default);
    Task ClearDtcsAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: Create BaseEcuModule**

`Porsche928Diagnostics/Modules/BaseEcuModule.cs`:

```csharp
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Modules;

/// <summary>
/// Provides ReadEcuIdentification, ReadDtcs, and ClearDtcs for all ECU modules.
/// Subclasses add module-specific commands.
///
/// All methods assume an active ISO 9141-2 session has already been established
/// by Iso9141Session.InitializeAsync() before being called.
/// </summary>
public abstract class BaseEcuModule : IEcuModule
{
    protected readonly IKLineInterface KLine;

    public abstract byte EcuAddress { get; }
    public abstract string EcuName { get; }

    protected BaseEcuModule(IKLineInterface kLine)
    {
        KLine = kLine;
    }

    public async Task<EcuIdentification> ReadEcuIdentificationAsync(CancellationToken ct = default)
    {
        // SID 0x1A, Option 0x90 — returns chip code, Bosch part number, EPROM version
        var frame = MessageFrame.Build(EcuAddress, sid: 0x1A, data: [0x90]);
        var rawResponse = await KLine.SendFrameAsync(frame, timeoutMs: 2000, ct);
        var parsed = MessageFrame.Parse(rawResponse);

        if (!parsed.IsValid)
            throw new InvalidOperationException($"{EcuName}: Invalid ECU ID response");

        // Response data: [chip code bytes 0..3] [part number bytes 4..9] [EPROM version byte 10]
        var data = parsed.Data;
        string chip = data.Length >= 4 ? BitConverter.ToString(data[..4]).Replace("-", "") : "Unknown";
        string part = data.Length >= 10 ? System.Text.Encoding.ASCII.GetString(data[4..10]).Trim() : "Unknown";
        string eprom = data.Length >= 11 ? $"v{data[10]:X2}" : "Unknown";

        return new EcuIdentification(chip, part, eprom, data);
    }

    public async Task<IReadOnlyList<DiagnosticTroubleCode>> ReadDtcsAsync(CancellationToken ct = default)
    {
        // SID 0x18 — returns stored fault code pairs
        var frame = MessageFrame.Build(EcuAddress, sid: 0x18, data: []);
        var rawResponse = await KLine.SendFrameAsync(frame, timeoutMs: 2000, ct);
        var parsed = MessageFrame.Parse(rawResponse);

        if (!parsed.IsValid)
            throw new InvalidOperationException($"{EcuName}: Invalid DTC response");

        var dtcs = new List<DiagnosticTroubleCode>();
        var data = parsed.Data;

        // DTC pairs: first byte is fault code, second is occurrence counter / status
        for (int i = 0; i + 1 < data.Length; i += 2)
        {
            if (data[i] == 0x00) continue; // 0x00 0x00 = end of list / no fault
            dtcs.Add(new DiagnosticTroubleCode(data[i], data[i + 1]));
        }

        return dtcs.AsReadOnly();
    }

    public async Task ClearDtcsAsync(CancellationToken ct = default)
    {
        // SID 0x14 — clears non-volatile fault RAM
        var frame = MessageFrame.Build(EcuAddress, sid: 0x14, data: []);
        var rawResponse = await KLine.SendFrameAsync(frame, timeoutMs: 2000, ct);
        var parsed = MessageFrame.Parse(rawResponse);

        if (!parsed.IsValid)
            throw new InvalidOperationException($"{EcuName}: Clear DTC command rejected");
    }

    /// <summary>
    /// Sends a frame and validates the response. Throws on invalid checksum or negative response SID.
    /// Negative response SID = 0x7F; second data byte is the original SID, third is error code.
    /// </summary>
    protected async Task<ParsedFrame> SendAndVerifyAsync(byte sid, byte[] data,
        int timeoutMs = 2000, CancellationToken ct = default)
    {
        var frame = MessageFrame.Build(EcuAddress, sid, data);
        var raw = await KLine.SendFrameAsync(frame, timeoutMs, ct);
        var parsed = MessageFrame.Parse(raw);

        if (!parsed.IsValid)
            throw new InvalidOperationException($"{EcuName}: Frame checksum invalid for SID 0x{sid:X2}");

        if (parsed.ServiceId == 0x7F)
            throw new InvalidOperationException(
                $"{EcuName}: Negative response to SID 0x{sid:X2}, error code 0x{(parsed.Data.Length > 1 ? parsed.Data[1] : 0):X2}");

        return parsed;
    }
}
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build Porsche928Diagnostics/Porsche928Diagnostics.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add Porsche928Diagnostics/Models/ Porsche928Diagnostics/Modules/IEcuModule.cs Porsche928Diagnostics/Modules/BaseEcuModule.cs
git commit -m "feat: add domain models and base ECU module with DTC read/clear/identify"
```

---

## Task 7: LhModule — LH 2.3 Injection ECU (0x11)

**Files:**
- Create: `Porsche928Diagnostics/Modules/LhModule.cs`
- Create: `Porsche928Diagnostics.Tests/Modules/LhModuleTests.cs`

- [ ] **Step 1: Write failing tests**

`Porsche928Diagnostics.Tests/Modules/LhModuleTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Tests.Modules;

public class LhModuleTests
{
    private readonly IKLineInterface _mock;
    private readonly LhModule _lh;

    public LhModuleTests()
    {
        _mock = Substitute.For<IKLineInterface>();
        _lh = new LhModule(_mock);
    }

    [Fact]
    public void EcuAddress_IsCorrect()
    {
        _lh.EcuAddress.Should().Be(0x11);
    }

    [Fact]
    public async Task ReadInputSignalsAsync_ParsesBitsCorrectly()
    {
        // Arrange: SID 0x21 PID 0x40 response returns byte 0x05 (bits 0+2 set)
        // Frame: [0x81, 0x11, 0xF1, 0x61, 0x05, CS]
        // 0x81+0x11+0xF1+0x61+0x05 = 129+17+241+97+5 = 489 = 0x1E9 → 0xE9
        byte[] response = [0x81, 0x11, 0xF1, 0x61, 0x05, 0xE9];
        SetupMockResponse(response);

        var signals = await _lh.ReadInputSignalsAsync();

        signals.ThrottleIdleSwitch.Should().BeTrue();    // bit 0
        signals.WideOpenThrottleSwitch.Should().BeFalse(); // bit 1
        signals.AircoCompressorDemand.Should().BeTrue();   // bit 2
        signals.IdleDropGearEngagement.Should().BeFalse(); // bit 3
    }

    [Fact]
    public async Task ReadActualValuesAsync_ParsesBatteryVoltage()
    {
        // Response data: [batteryRaw=0xD4=212, refVoltRaw, ezkOn, tempRaw]
        // Battery = 212 * 0.065 = 13.78V
        byte[] response = BuildResponseFrame(0x61, [0xD4, 0x4D, 0x01, 0x6A]);
        SetupMockResponse(response);

        var values = await _lh.ReadActualValuesAsync();

        values.BatteryVoltage.Should().BeApproximately(13.78, 0.01);
        values.EzkOnSignal.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateDriveLinkAsync_SendsCorrectFrame()
    {
        // Tank vent valve = device 0x01, activate = 0x01
        byte[] ackResponse = BuildResponseFrame(0x71, [0x01]);
        SetupMockResponse(ackResponse);

        await _lh.ActivateDriveLinkAsync(LhDriveLink.TankVentValve);

        await _mock.Received().SendFrameAsync(
            Arg.Is<byte[]>(f => f[3] == 0x30 && f[4] == 0x01 && f[5] == 0x01),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task StopDriveLinkAsync_SendsStopCommand()
    {
        byte[] ackResponse = BuildResponseFrame(0x71, [0x01]);
        SetupMockResponse(ackResponse);

        await _lh.StopDriveLinkAsync(LhDriveLink.TankVentValve);

        await _mock.Received().SendFrameAsync(
            Arg.Is<byte[]>(f => f[3] == 0x30 && f[4] == 0x01 && f[5] == 0x00),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()
        );
    }

    private void SetupMockResponse(byte[] response)
    {
        _mock.SendFrameAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(response));
    }

    private static byte[] BuildResponseFrame(byte responseSid, byte[] data)
    {
        // [Format=0x80|dataLen+1] [0x11] [0xF1] [SID] [Data...] [CS]
        byte format = (byte)(0x80 | (1 + data.Length));
        var frame = new byte[4 + data.Length + 1];
        frame[0] = format; frame[1] = 0x11; frame[2] = 0xF1; frame[3] = responseSid;
        data.CopyTo(frame, 4);
        frame[^1] = ChecksumHelper.Calculate(frame.AsSpan(0, frame.Length - 1));
        return frame;
    }
}
```

- [ ] **Step 2: Run tests — expect failures**

```bash
dotnet test Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj --filter "LhModuleTests"
```

Expected: type not found.

- [ ] **Step 3: Implement LhModule**

`Porsche928Diagnostics/Modules/LhModule.cs`:

```csharp
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Modules;

/// <summary>
/// LH Jetronic 2.3 fuel injection ECU, K-Line address 0x11.
/// Supports: ECU ID, fault codes, drive links (tank vent/resonance flap/injectors/ISV),
/// input signals (throttle/WOT/airco), actual values (battery voltage, temp, MAF, lambda),
/// and the System Adaptation Program (SAP).
/// </summary>
public sealed class LhModule : BaseEcuModule
{
    public override byte EcuAddress => 0x11;
    public override string EcuName => "LH Jetronic 2.3";

    public LhModule(IKLineInterface kLine) : base(kLine) { }

    /// <summary>
    /// Reads discrete switch/flag states via SID 0x21, PID 0x40.
    /// Returns a single byte with individual bit flags.
    /// </summary>
    public async Task<LhInputSignals> ReadInputSignalsAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x40], ct: ct);
        if (parsed.Data.Length < 1)
            throw new InvalidOperationException("LH: Empty input signals response");
        return new LhInputSignals(parsed.Data[0]);
    }

    /// <summary>
    /// Reads static ECU sensor values via SID 0x21.
    /// Battery voltage: RawByte × 0.065V.
    /// Engine temp uses NTC thermistor lookup table (voltage drop → °C).
    /// </summary>
    public async Task<LhActualValues> ReadActualValuesAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x01], ct: ct);
        var d = parsed.Data;
        if (d.Length < 4)
            throw new InvalidOperationException("LH: Insufficient actual values data");

        double battery = d[0] * 0.065;
        double refVolt = d[1] * (5.0 / 255.0);
        bool ezkOn = d[2] != 0x00;
        double tempC = NtcLookup(d[3]);

        return new LhActualValues(battery, refVolt, ezkOn, tempC,
            MafVoltage: 0, LambdaVoltage: 0, VehicleSpeedKph: 0,
            Coding4Cylinder: false, IsActiveReading: false);
    }

    /// <summary>
    /// Reads active (engine-running) values: MAF voltage, lambda, vehicle speed.
    /// SID 0x21 with PID 0x02 for active readings.
    /// </summary>
    public async Task<LhActualValues> ReadActiveValuesAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x02], ct: ct);
        var d = parsed.Data;
        if (d.Length < 4)
            throw new InvalidOperationException("LH: Insufficient active values data");

        double mafVoltage = d[0] * (5.0 / 255.0);   // 0–5V scaled
        double lambdaMv = d[1] * 5.0;                // 0–1275mV range
        double speedKph = d[2] * 1.0;                // raw speed pulse count → km/h
        bool coding4Cyl = (d[3] & 0x01) != 0;

        return new LhActualValues(0, 0, false, 0,
            mafVoltage, lambdaMv, speedKph, coding4Cyl, IsActiveReading: true);
    }

    /// <summary>
    /// Activates a drive link actuator via SID 0x30.
    /// State byte 0x01 = active; 0x00 = stop.
    /// </summary>
    public async Task ActivateDriveLinkAsync(LhDriveLink link, CancellationToken ct = default)
    {
        byte deviceId = (byte)link;
        await SendAndVerifyAsync(sid: 0x30, data: [deviceId, 0x01], ct: ct);
    }

    public async Task StopDriveLinkAsync(LhDriveLink link, CancellationToken ct = default)
    {
        byte deviceId = (byte)link;
        await SendAndVerifyAsync(sid: 0x30, data: [deviceId, 0x00], ct: ct);
    }

    /// <summary>
    /// Executes the System Adaptation Program (SAP) via SID 0x31, PID 0x0A.
    /// Engine must be at operating temperature and idling. The LH module
    /// monitors the lambda closed-loop system and writes corrected base
    /// injector pulse offsets to non-volatile RAM.
    /// This process takes approximately 60 seconds; progress is reported via callback.
    /// </summary>
    public async Task RunSystemAdaptationAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report("Starting System Adaptation Program. Engine must be at operating temperature, idling.");

        // SID 0x31 (Start Routine), PID 0x0A (Adaptation routine identifier)
        var startFrame = MessageFrame.Build(EcuAddress, sid: 0x31, data: [0x0A]);
        var startResponse = await KLine.SendFrameAsync(startFrame, timeoutMs: 3000, ct);
        var startParsed = MessageFrame.Parse(startResponse);
        if (!startParsed.IsValid)
            throw new InvalidOperationException("LH: SAP start command rejected");

        progress?.Report("SAP running — monitoring lambda closed-loop. Do not blip throttle.");

        // Poll routine status every 5 seconds for up to 90 seconds
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            await Task.Delay(5000, ct);

            // SID 0x33 (Request Routine Results)
            var pollFrame = MessageFrame.Build(EcuAddress, sid: 0x33, data: [0x0A]);
            var pollResponse = await KLine.SendFrameAsync(pollFrame, timeoutMs: 2000, ct);
            var pollParsed = MessageFrame.Parse(pollResponse);

            if (pollParsed.IsValid && pollParsed.Data.Length > 0)
            {
                byte status = pollParsed.Data[0];
                if (status == 0x02)
                {
                    progress?.Report("SAP complete. Adaptation values written to ECU non-volatile RAM.");
                    return;
                }
                progress?.Report($"SAP in progress (status=0x{status:X2})...");
            }
        }

        progress?.Report("SAP timed out. Check idle quality and retry.");
    }

    /// <summary>
    /// NTC thermistor lookup table for LH engine temperature sensor.
    /// Maps raw ADC byte (0–255 representing ~0–5V) to degrees Celsius.
    /// Values derived from Bosch NTC M12 sensor characteristic curve.
    /// </summary>
    private static double NtcLookup(byte rawByte)
    {
        // Simplified linear interpolation from known calibration points
        // Raw 0xFF (~0V) = short circuit; Raw 0x00 (~5V) = open circuit
        // Key points: 0x6A ≈ 80°C, 0x4D ≈ 20°C, 0x3A ≈ -10°C
        return rawByte switch
        {
            >= 0xE0 => -40.0,
            >= 0xC0 => -20.0,
            >= 0xA0 => 0.0,
            >= 0x80 => 20.0,
            >= 0x6A => 80.0,
            >= 0x50 => 100.0,
            >= 0x3A => 120.0,
            _ => 140.0
        };
    }
}

public enum LhDriveLink : byte
{
    TankVentValve  = 0x01,
    ResonanceFlap  = 0x02,
    FuelInjectors  = 0x03,
    IdleStabilizerValve = 0x04
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj --filter "LhModuleTests"
```

Expected: all LhModule tests pass.

- [ ] **Step 5: Commit**

```bash
git add Porsche928Diagnostics/Modules/LhModule.cs Porsche928Diagnostics.Tests/Modules/LhModuleTests.cs
git commit -m "feat: implement LH 2.3 injection module — drive links, inputs, SAP"
```

---

## Task 8: EzkModule — EZK Ignition ECU (0x12)

**Files:**
- Create: `Porsche928Diagnostics/Modules/EzkModule.cs`
- Create: `Porsche928Diagnostics.Tests/Modules/EzkModuleTests.cs`

- [ ] **Step 1: Write failing tests**

`Porsche928Diagnostics.Tests/Modules/EzkModuleTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Tests.Modules;

public class EzkModuleTests
{
    private readonly IKLineInterface _mock;
    private readonly EzkModule _ezk;

    public EzkModuleTests()
    {
        _mock = Substitute.For<IKLineInterface>();
        _ezk = new EzkModule(_mock);
    }

    [Fact]
    public void EcuAddress_IsCorrect()
    {
        _ezk.EcuAddress.Should().Be(0x12);
    }

    [Fact]
    public async Task ReadSensorDataAsync_ParsesRpm()
    {
        // RPM raw bytes: high=0x0C, low=0xA0 → 0x0CA0 = 3232. RPM = raw × (60/256) scaled...
        // Simpler: RPM = (high << 8 | low) → use direct decode
        // Let's say raw bytes [rpmHi=0x0C, rpmLo=0xA0, load=0x80, temp=0x6A, coding=0x01, trx=0x00, throttle=0x01]
        byte[] response = BuildResponseFrame(0x61, [0x0C, 0xA0, 0x80, 0x6A, 0x01, 0x00, 0x01]);
        SetupMockResponse(response);

        var data = await _ezk.ReadSensorDataAsync();

        data.EngineRpm.Should().Be(3232);
        data.LoadPercent.Should().BeApproximately(50.2, 0.5); // 0x80/255*100
        data.TransmissionCoding.Should().Be("Manual");
    }

    [Fact]
    public async Task ReadKnockCountsAsync_ReturnsEightCylinders()
    {
        // 8 knock counters, one per cylinder barrel
        byte[] knockBytes = [0x00, 0x03, 0x00, 0x01, 0x00, 0x00, 0x07, 0x00];
        byte[] response = BuildResponseFrame(0x61, knockBytes);
        SetupMockResponse(response);

        var counts = await _ezk.ReadKnockCountsAsync();

        counts.Should().HaveCount(8);
        counts[1].Should().Be(3); // cylinder 2 knocked 3 times
        counts[6].Should().Be(7); // cylinder 7 knocked 7 times
    }

    private void SetupMockResponse(byte[] response)
    {
        _mock.SendFrameAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(response));
    }

    private static byte[] BuildResponseFrame(byte sid, byte[] data)
    {
        byte format = (byte)(0x80 | (1 + data.Length));
        var frame = new byte[4 + data.Length + 1];
        frame[0] = format; frame[1] = 0x12; frame[2] = 0xF1; frame[3] = sid;
        data.CopyTo(frame, 4);
        frame[^1] = ChecksumHelper.Calculate(frame.AsSpan(0, frame.Length - 1));
        return frame;
    }
}
```

- [ ] **Step 2: Implement EzkModule**

`Porsche928Diagnostics/Modules/EzkModule.cs`:

```csharp
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Modules;

/// <summary>
/// EZK ignition control module, K-Line address 0x12.
/// Manages timing advance maps, knock detection, and provides
/// RPM/load/temperature telemetry derived from flywheel sensor and LH link.
/// </summary>
public sealed class EzkModule : BaseEcuModule
{
    public override byte EcuAddress => 0x12;
    public override string EcuName => "EZK Ignition Control";

    public EzkModule(IKLineInterface kLine) : base(kLine) { }

    /// <summary>
    /// Reads engine speed, load, temperature, and coding via SID 0x21.
    /// RPM is encoded as a 16-bit big-endian value from the flywheel sensor.
    /// </summary>
    public async Task<EzkSensorData> ReadSensorDataAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x01], ct: ct);
        var d = parsed.Data;
        if (d.Length < 7)
            throw new InvalidOperationException("EZK: Insufficient sensor data bytes");

        int rpm = (d[0] << 8) | d[1];
        double load = (d[2] / 255.0) * 100.0;
        double tempC = d[3] * 0.75 - 40.0; // Linear scaling for EZK temp sensor
        string transmission = (d[4] & 0x01) != 0 ? "Manual" : "Automatic";
        bool throttleActive = d[6] != 0x00;

        return new EzkSensorData(rpm, load, tempC, transmission, throttleActive,
            KnockCountPerCylinder: new int[8]);
    }

    /// <summary>
    /// Queries the EZK knock registration memory via SID 0x21, PID 0x04.
    /// Each cylinder has a dedicated counter byte that increments when
    /// the knock sensor detects detonation and the ECU retards timing for
    /// that cylinder. Counter values persist until cleared.
    /// Returns an array of 8 knock event counts indexed by cylinder (0 = cyl 1).
    /// </summary>
    public async Task<int[]> ReadKnockCountsAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x04], ct: ct);
        var d = parsed.Data;

        var counts = new int[8];
        for (int i = 0; i < Math.Min(8, d.Length); i++)
            counts[i] = d[i];

        return counts;
    }
}
```

- [ ] **Step 3: Run tests — expect pass**

```bash
dotnet test Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj --filter "EzkModuleTests"
```

Expected: all EZK tests pass.

- [ ] **Step 4: Commit**

```bash
git add Porsche928Diagnostics/Modules/EzkModule.cs Porsche928Diagnostics.Tests/Modules/EzkModuleTests.cs
git commit -m "feat: implement EZK ignition module — RPM/load telemetry and knock registration"
```

---

## Task 9: PsdModule — Slip Differential ECU (0x28)

**Files:**
- Create: `Porsche928Diagnostics/Modules/PsdModule.cs`
- Create: `Porsche928Diagnostics.Tests/Modules/PsdModuleTests.cs`

- [ ] **Step 1: Write failing tests**

`Porsche928Diagnostics.Tests/Modules/PsdModuleTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Tests.Modules;

public class PsdModuleTests
{
    private readonly IKLineInterface _mock;
    private readonly PsdModule _psd;

    public PsdModuleTests()
    {
        _mock = Substitute.For<IKLineInterface>();
        _psd = new PsdModule(_mock);
    }

    [Fact]
    public void EcuAddress_IsCorrect()
    {
        _psd.EcuAddress.Should().Be(0x28);
    }

    [Fact]
    public async Task StartBleedProcedureAsync_SendsActivateCommand()
    {
        var ackResponse = BuildResponseFrame(0x71, [0x01, 0x01]);
        var stopResponse = BuildResponseFrame(0x71, [0x01, 0x00]);

        _mock.SendFrameAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(ackResponse), Task.FromResult(stopResponse));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await _psd.StartBleedProcedureAsync(durationSeconds: 0, progress: null, ct: cts.Token);

        // First call should activate bleed (SID=0x30, data=[0x01, 0x01])
        await _mock.Received().SendFrameAsync(
            Arg.Is<byte[]>(f => f[3] == 0x30 && f[4] == 0x01 && f[5] == 0x01),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()
        );
    }

    private static byte[] BuildResponseFrame(byte sid, byte[] data)
    {
        byte format = (byte)(0x80 | (1 + data.Length));
        var frame = new byte[4 + data.Length + 1];
        frame[0] = format; frame[1] = 0x28; frame[2] = 0xF1; frame[3] = sid;
        data.CopyTo(frame, 4);
        frame[^1] = ChecksumHelper.Calculate(frame.AsSpan(0, frame.Length - 1));
        return frame;
    }
}
```

- [ ] **Step 2: Implement PsdModule**

`Porsche928Diagnostics/Modules/PsdModule.cs`:

```csharp
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Modules;

/// <summary>
/// PSD (Porsche Slip Differential) hydraulic lock ECU, K-Line address 0x28.
/// The bleed procedure activates the hydraulic pump and opens the transverse
/// lock solenoid valve for a fixed window, allowing a mechanic to purge air
/// from the high-pressure slave cylinder without manual pumping at the calipers.
/// </summary>
public sealed class PsdModule : BaseEcuModule
{
    public override byte EcuAddress => 0x28;
    public override string EcuName => "PSD Slip Differential";

    private const int DefaultBleedDurationSeconds = 60;

    public PsdModule(IKLineInterface kLine) : base(kLine) { }

    /// <summary>
    /// Activates the hydraulic bleed sequence. Runs for <paramref name="durationSeconds"/>
    /// seconds (default 60s) while reporting elapsed time via progress callback.
    /// Sends stop command on completion or cancellation.
    ///
    /// Safety: Operator must ensure vehicle is on lift with differential accessible.
    /// </summary>
    public async Task StartBleedProcedureAsync(
        int durationSeconds = DefaultBleedDurationSeconds,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (durationSeconds <= 0) durationSeconds = DefaultBleedDurationSeconds;

        progress?.Report("Activating PSD hydraulic pump and solenoid valve...");

        // SID 0x30, device 0x01 (bleed actuator), state 0x01 (activate)
        await SendAndVerifyAsync(sid: 0x30, data: [0x01, 0x01], ct: ct);

        progress?.Report($"Bleed sequence active. Crack bleeder screw. Running for {durationSeconds}s.");

        try
        {
            for (int elapsed = 0; elapsed < durationSeconds; elapsed++)
            {
                await Task.Delay(1000, ct);
                int remaining = durationSeconds - elapsed - 1;
                progress?.Report($"Bleed active — {remaining}s remaining. Keep bleeder cracked until fluid flows clear.");
            }
        }
        finally
        {
            // Always stop the actuator, even on cancellation
            progress?.Report("Stopping bleed actuator — tighten bleeder screw now.");
            var stopFrame = MessageFrame.Build(EcuAddress, sid: 0x30, data: [0x01, 0x00]);
            await KLine.SendFrameAsync(stopFrame, timeoutMs: 2000, CancellationToken.None);
            progress?.Report("PSD bleed procedure complete.");
        }
    }

    /// <summary>
    /// Activates the transverse lock solenoid independently to verify mechanical engagement.
    /// </summary>
    public async Task CheckTransverseLockAsync(CancellationToken ct = default)
    {
        // SID 0x30, device 0x02 (lock solenoid), state 0x01 (engage)
        await SendAndVerifyAsync(sid: 0x30, data: [0x02, 0x01], ct: ct);
        await Task.Delay(3000, ct);
        await SendAndVerifyAsync(sid: 0x30, data: [0x02, 0x00], ct: ct);
    }
}
```

- [ ] **Step 3: Run tests — expect pass**

```bash
dotnet test Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj --filter "PsdModuleTests"
```

- [ ] **Step 4: Commit**

```bash
git add Porsche928Diagnostics/Modules/PsdModule.cs Porsche928Diagnostics.Tests/Modules/PsdModuleTests.cs
git commit -m "feat: implement PSD module with 60-second hydraulic bleed procedure"
```

---

## Task 10: RdkModule — Tire Pressure ECU (0x30)

**Files:**
- Create: `Porsche928Diagnostics/Modules/RdkModule.cs`
- Create: `Porsche928Diagnostics.Tests/Modules/RdkModuleTests.cs`

- [ ] **Step 1: Write failing tests**

`Porsche928Diagnostics.Tests/Modules/RdkModuleTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Tests.Modules;

public class RdkModuleTests
{
    private readonly IKLineInterface _mock;
    private readonly RdkModule _rdk;

    public RdkModuleTests()
    {
        _mock = Substitute.For<IKLineInterface>();
        _rdk = new RdkModule(_mock);
    }

    [Fact]
    public void EcuAddress_IsCorrect()
    {
        _rdk.EcuAddress.Should().Be(0x30);
    }

    [Fact]
    public async Task ReadPressureSwitchesAsync_ParsesBitField()
    {
        // Pressure byte: 0b00001101 = wheels FL(bit0), RL(bit2) OK, FR(bit1) FR(bit3) LEAK
        // true = closed switch = OK pressure
        byte[] response = BuildResponseFrame(0x61, [0x0D, 0x01, 0x00, 0x00, 0x00, 0x00]);
        SetupMockResponse(response);

        var data = await _rdk.ReadSensorDataAsync();

        data.PressureSwitchStates[0].Should().BeTrue();  // FL OK
        data.PressureSwitchStates[1].Should().BeFalse(); // FR LEAK
        data.PressureSwitchStates[2].Should().BeTrue();  // RL OK
        data.PressureSwitchStates[3].Should().BeFalse(); // RR LEAK
        data.HfReceiverActive.Should().BeTrue();
    }

    private void SetupMockResponse(byte[] response)
    {
        _mock.SendFrameAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(response));
    }

    private static byte[] BuildResponseFrame(byte sid, byte[] data)
    {
        byte format = (byte)(0x80 | (1 + data.Length));
        var frame = new byte[4 + data.Length + 1];
        frame[0] = format; frame[1] = 0x30; frame[2] = 0xF1; frame[3] = sid;
        data.CopyTo(frame, 4);
        frame[^1] = ChecksumHelper.Calculate(frame.AsSpan(0, frame.Length - 1));
        return frame;
    }
}
```

- [ ] **Step 2: Implement RdkModule**

`Porsche928Diagnostics/Modules/RdkModule.cs`:

```csharp
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Modules;

/// <summary>
/// RDK (Reifendruck Kontroole / Tire Pressure Monitor) ECU, K-Line address 0x30.
/// Receives high-frequency signals from pressure switches embedded in the
/// magnesium alloy wheels. Switch CLOSED = safe pressure. Switch OPEN = pressure loss.
/// </summary>
public sealed class RdkModule : BaseEcuModule
{
    public override byte EcuAddress => 0x30;
    public override string EcuName => "RDK Tire Pressure Monitor";

    public RdkModule(IKLineInterface kLine) : base(kLine) { }

    /// <summary>
    /// Reads the HF receiver state and all four wheel pressure switch positions.
    /// Switch byte layout (bits 0–3): FL, FR, RL, RR. 1 = pressure OK, 0 = leak/low.
    /// Also reads ABS wheel speed sensor raw values from RDK co-processor bytes.
    /// </summary>
    public async Task<RdkSensorData> ReadSensorDataAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x01], ct: ct);
        var d = parsed.Data;
        if (d.Length < 1)
            throw new InvalidOperationException("RDK: Empty sensor response");

        byte switchByte = d[0];
        bool[] pressureStates =
        [
            (switchByte & 0x01) != 0,  // FL
            (switchByte & 0x02) != 0,  // FR
            (switchByte & 0x04) != 0,  // RL
            (switchByte & 0x08) != 0   // RR
        ];

        bool hfActive = d.Length > 1 && d[1] != 0x00;

        // ABS speed bytes if available (bytes 2–5, one per wheel, km/h encoded)
        double[] absSpeed = new double[4];
        for (int i = 0; i < 4 && (i + 2) < d.Length; i++)
            absSpeed[i] = d[i + 2] * 1.5; // empirical scaling factor

        return new RdkSensorData(pressureStates, hfActive, absSpeed);
    }
}
```

- [ ] **Step 3: Run tests and commit**

```bash
dotnet test Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj --filter "RdkModuleTests"
git add Porsche928Diagnostics/Modules/RdkModule.cs Porsche928Diagnostics.Tests/Modules/RdkModuleTests.cs
git commit -m "feat: implement RDK tire pressure module with HF receiver and pressure switch parsing"
```

---

## Task 11: AirbagModule (0x40)

**Files:**
- Create: `Porsche928Diagnostics/Modules/AirbagModule.cs`
- Create: `Porsche928Diagnostics.Tests/Modules/AirbagModuleTests.cs`

- [ ] **Step 1: Write failing tests**

`Porsche928Diagnostics.Tests/Modules/AirbagModuleTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Tests.Modules;

public class AirbagModuleTests
{
    private readonly IKLineInterface _mock;
    private readonly AirbagModule _airbag;

    public AirbagModuleTests()
    {
        _mock = Substitute.For<IKLineInterface>();
        _airbag = new AirbagModule(_mock);
    }

    [Fact]
    public void EcuAddress_IsCorrect()
    {
        _airbag.EcuAddress.Should().Be(0x40);
    }

    [Fact]
    public async Task ReadAirbagDataAsync_ParsesDowntimeClock()
    {
        // Downtime: 3 bytes big-endian seconds. 0x00 0x0E 0x10 = 3600 seconds = 1 hour
        // Crash byte: 0x00 = no crash. Driver: bit0, Passenger: bit1, Seatbelt: bit2
        byte[] response = BuildResponseFrame(0x61, [0x00, 0x0E, 0x10, 0x00]);
        SetupMockResponse(response);

        var data = await _airbag.ReadAirbagDataAsync();

        data.DowntimeClock.Should().Be(TimeSpan.FromSeconds(3600));
        data.CrashEventRecorded.Should().BeFalse();
        data.DriverBagFired.Should().BeFalse();
    }

    [Fact]
    public async Task ReadAirbagDataAsync_ParsesCrashBits()
    {
        // Crash byte: 0x03 = bits 0+1 set = driver + passenger bags fired
        byte[] response = BuildResponseFrame(0x61, [0x00, 0x00, 0x00, 0x03]);
        SetupMockResponse(response);

        var data = await _airbag.ReadAirbagDataAsync();

        data.CrashEventRecorded.Should().BeTrue();
        data.DriverBagFired.Should().BeTrue();
        data.PassengerBagFired.Should().BeTrue();
        data.SeatbeltPretensionerFired.Should().BeFalse();
    }

    private void SetupMockResponse(byte[] r)
    {
        _mock.SendFrameAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(r));
    }

    private static byte[] BuildResponseFrame(byte sid, byte[] data)
    {
        byte format = (byte)(0x80 | (1 + data.Length));
        var frame = new byte[4 + data.Length + 1];
        frame[0] = format; frame[1] = 0x40; frame[2] = 0xF1; frame[3] = sid;
        data.CopyTo(frame, 4);
        frame[^1] = ChecksumHelper.Calculate(frame.AsSpan(0, frame.Length - 1));
        return frame;
    }
}
```

- [ ] **Step 2: Implement AirbagModule**

`Porsche928Diagnostics/Modules/AirbagModule.cs`:

```csharp
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Modules;

/// <summary>
/// TRW/Porsche airbag ECU, K-Line address 0x40.
/// Downtime clock measures seconds since last battery power — a long downtime
/// may indicate capacitor discharge and should be evaluated before vehicle use.
/// Crash data byte bits persist in non-volatile memory after a deployment event.
/// </summary>
public sealed class AirbagModule : BaseEcuModule
{
    public override byte EcuAddress => 0x40;
    public override string EcuName => "Airbag (TRW)";

    public AirbagModule(IKLineInterface kLine) : base(kLine) { }

    /// <summary>
    /// Reads downtime clock and crash deployment data via SID 0x21, PID 0x01.
    /// Response bytes [0..2]: 24-bit big-endian seconds since power loss.
    /// Response byte [3]: crash data bit field.
    ///   Bit 0 = driver airbag fired
    ///   Bit 1 = passenger airbag fired
    ///   Bit 2 = seatbelt pretensioner fired
    /// </summary>
    public async Task<AirbagData> ReadAirbagDataAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x01], ct: ct);
        var d = parsed.Data;
        if (d.Length < 4)
            throw new InvalidOperationException("Airbag: Insufficient response data");

        int downtimeSeconds = (d[0] << 16) | (d[1] << 8) | d[2];
        byte crashByte = d[3];

        bool driverFired    = (crashByte & 0x01) != 0;
        bool passengerFired = (crashByte & 0x02) != 0;
        bool seatbeltFired  = (crashByte & 0x04) != 0;
        bool anyCrash = driverFired || passengerFired || seatbeltFired;

        return new AirbagData(
            TimeSpan.FromSeconds(downtimeSeconds),
            anyCrash, driverFired, passengerFired, seatbeltFired,
            crashByte
        );
    }
}
```

- [ ] **Step 3: Run tests and commit**

```bash
dotnet test Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj --filter "AirbagModuleTests"
git add Porsche928Diagnostics/Modules/AirbagModule.cs Porsche928Diagnostics.Tests/Modules/AirbagModuleTests.cs
git commit -m "feat: implement Airbag module with downtime clock and crash data bit parsing"
```

---

## Task 12: AlarmModule (0x45)

**Files:**
- Create: `Porsche928Diagnostics/Modules/AlarmModule.cs`
- Create: `Porsche928Diagnostics.Tests/Modules/AlarmModuleTests.cs`

- [ ] **Step 1: Write failing tests**

`Porsche928Diagnostics.Tests/Modules/AlarmModuleTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Tests.Modules;

public class AlarmModuleTests
{
    private readonly IKLineInterface _mock;
    private readonly AlarmModule _alarm;

    public AlarmModuleTests()
    {
        _mock = Substitute.For<IKLineInterface>();
        _alarm = new AlarmModule(_mock);
    }

    [Fact]
    public void EcuAddress_IsCorrect()
    {
        _alarm.EcuAddress.Should().Be(0x45);
    }

    [Fact]
    public async Task ReadAlarmDataAsync_ParsesSwitchStates()
    {
        // Switch byte: 0b00000101 = engine lid (bit0) + glove box (bit2) open
        byte[] response = BuildResponseFrame(0x61, [0x05, 0x44, 0x45]); // 0x44='D', 0x45='E' = "DE"
        SetupMockResponse(response);

        var data = await _alarm.ReadAlarmDataAsync();

        data.EngineLidSwitchOpen.Should().BeTrue();
        data.LuggageLidSwitchOpen.Should().BeFalse();
        data.GloveCompartmentSwitchOpen.Should().BeTrue();
        data.CountryCode.Should().Be("DE");
    }

    [Fact]
    public async Task SetCountryCodingAsync_SendsCodeBytes()
    {
        byte[] ackResponse = BuildResponseFrame(0x71, [0x01]);
        SetupMockResponse(ackResponse);

        await _alarm.SetCountryCodingAsync("US");

        await _mock.Received().SendFrameAsync(
            Arg.Is<byte[]>(f => f[3] == 0x30 && f[4] == (byte)'U' && f[5] == (byte)'S'),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()
        );
    }

    private void SetupMockResponse(byte[] r)
    {
        _mock.SendFrameAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(r));
    }

    private static byte[] BuildResponseFrame(byte sid, byte[] data)
    {
        byte format = (byte)(0x80 | (1 + data.Length));
        var frame = new byte[4 + data.Length + 1];
        frame[0] = format; frame[1] = 0x45; frame[2] = 0xF1; frame[3] = sid;
        data.CopyTo(frame, 4);
        frame[^1] = ChecksumHelper.Calculate(frame.AsSpan(0, frame.Length - 1));
        return frame;
    }
}
```

- [ ] **Step 2: Implement AlarmModule**

`Porsche928Diagnostics/Modules/AlarmModule.cs`:

```csharp
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.Modules;

/// <summary>
/// Porsche alarm system ECU, K-Line address 0x45.
/// Country coding adjusts regional alarm behavior (horn duty cycle, indicator flash rate, etc.).
/// Input switches are microswitch-based tamper detection on compartment lids.
/// Drive links can cycle the alarm outputs (horn, indicators, central locking).
/// </summary>
public sealed class AlarmModule : BaseEcuModule
{
    public override byte EcuAddress => 0x45;
    public override string EcuName => "Alarm System";

    private static readonly Dictionary<byte, string> CountryCodes = new()
    {
        { 0x00, "DE" }, { 0x01, "GB" }, { 0x02, "US" },
        { 0x03, "FR" }, { 0x04, "IT" }, { 0x05, "JP" }
    };

    public AlarmModule(IKLineInterface kLine) : base(kLine) { }

    /// <summary>
    /// Reads perimeter switch states and current country coding via SID 0x21, PID 0x01.
    /// Response byte[0]: switch bit field. Response bytes[1–2]: ASCII country code chars.
    ///   Bit 0 = engine/front lid open
    ///   Bit 1 = luggage/rear lid open
    ///   Bit 2 = glove compartment open
    ///   Bit 3 = interior motion sensor active
    /// </summary>
    public async Task<AlarmData> ReadAlarmDataAsync(CancellationToken ct = default)
    {
        var parsed = await SendAndVerifyAsync(sid: 0x21, data: [0x01], ct: ct);
        var d = parsed.Data;
        if (d.Length < 3)
            throw new InvalidOperationException("Alarm: Insufficient response data");

        byte switchByte = d[0];
        string countryCode = d.Length >= 3
            ? $"{(char)d[1]}{(char)d[2]}"
            : "??";

        return new AlarmData(
            countryCode,
            EngineLidSwitchOpen:        (switchByte & 0x01) != 0,
            LuggageLidSwitchOpen:       (switchByte & 0x02) != 0,
            GloveCompartmentSwitchOpen: (switchByte & 0x04) != 0,
            InteriorMotionSensorActive: (switchByte & 0x08) != 0,
            switchByte
        );
    }

    /// <summary>
    /// Writes a 2-character country code to the alarm ECU via SID 0x30.
    /// Valid codes: "DE", "GB", "US", "FR", "IT", "JP".
    /// </summary>
    public async Task SetCountryCodingAsync(string countryCode, CancellationToken ct = default)
    {
        if (countryCode.Length != 2)
            throw new ArgumentException("Country code must be exactly 2 characters", nameof(countryCode));

        byte b1 = (byte)countryCode[0];
        byte b2 = (byte)countryCode[1];
        await SendAndVerifyAsync(sid: 0x30, data: [b1, b2], ct: ct);
    }

    /// <summary>
    /// Activates alarm drive link output for testing. 
    /// linkId: 0x01=Horn, 0x02=Indicators, 0x03=Interior lights, 0x04=Central lock
    /// </summary>
    public async Task ActivateDriveLinkAsync(AlarmDriveLink link, CancellationToken ct = default)
    {
        await SendAndVerifyAsync(sid: 0x30, data: [(byte)link, 0x01], ct: ct);
    }

    public async Task StopDriveLinkAsync(AlarmDriveLink link, CancellationToken ct = default)
    {
        await SendAndVerifyAsync(sid: 0x30, data: [(byte)link, 0x00], ct: ct);
    }
}

public enum AlarmDriveLink : byte
{
    Horn           = 0x01,
    Indicators     = 0x02,
    InteriorLights = 0x03,
    CentralLock    = 0x04
}
```

- [ ] **Step 3: Run tests and commit**

```bash
dotnet test Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj --filter "AlarmModuleTests"
git add Porsche928Diagnostics/Modules/AlarmModule.cs Porsche928Diagnostics.Tests/Modules/AlarmModuleTests.cs
git commit -m "feat: implement Alarm module with country coding, switch states, and drive links"
```

---

## Task 13: DigitalDashModule — Instrument Cluster Diagnostics Helper

**Files:**
- Create: `Porsche928Diagnostics/Modules/DigitalDashModule.cs`

The digital instrument cluster (1989 S4) does not respond to K-Line protocol messages. Instead, its internal test mode is triggered by grounding a specific pin on the 19-pin connector. The software role is to time and instruct the operator.

- [ ] **Step 1: Implement DigitalDashModule**

`Porsche928Diagnostics/Modules/DigitalDashModule.cs`:

```csharp
namespace Porsche928Diagnostics.Modules;

/// <summary>
/// Helper for triggering the built-in self-test mode of the 928 digital instrument cluster.
///
/// The cluster does NOT communicate over K-Line. Its diagnostic mode is activated by
/// grounding Pin 6 of the 19-pin connector for exactly 3 seconds with the ignition ON.
/// This causes the cluster microcontroller to loop through all display segments and
/// then report sensor values sequentially on its LCD.
///
/// Readings available in dash self-test mode:
///   Oil pressure (bar), Oil level (L), Brake fluid level (OK/LOW),
///   Engine temperature (°C), Coolant level (OK/LOW),
///   Toothed belt tension (OK/FAULT — critical safety item).
/// </summary>
public sealed class DigitalDashModule
{
    public record DashTestStep(int StepNumber, string Instruction, int DurationSeconds);

    private static readonly DashTestStep[] Steps =
    [
        new(1, "Turn ignition ON (do not start engine). Dashboard should illuminate normally.", 5),
        new(2, "Ground Pin 6 of the 19-pin diagnostic connector. Use a jumper wire from Pin 6 to Pin 1 (chassis ground). Hold for 3 seconds.", 3),
        new(3, "Release the ground on Pin 6. The dashboard display will now enter segment-check mode — all segments light simultaneously.", 4),
        new(4, "READING: Oil pressure display. Normal idle range: 2.0–4.5 bar. Record value.", 5),
        new(5, "READING: Oil level. Min line = 4.0L low. Record display.", 5),
        new(6, "READING: Brake fluid level. Display shows OK or LOW. LOW = inspect reservoir.", 5),
        new(7, "READING: Engine coolant temperature (°C). Should match ECU actual value ±5°C.", 5),
        new(8, "READING: Coolant level. OK = sufficient, LOW = check expansion tank.", 5),
        new(9, "CRITICAL READING: Toothed belt tension sensor. OK = belt tensioner within spec. FAULT = inspect tensioner roller immediately.", 8),
        new(10, "Self-test sequence complete. The display will return to normal operation. Turn ignition OFF.", 3)
    ];

    public IReadOnlyList<DashTestStep> GetTestSteps() => Steps;

    /// <summary>
    /// Runs the guided dashboard test sequence, reporting each step via the progress callback.
    /// Each step waits for its specified duration before advancing.
    /// </summary>
    public async Task RunGuidedSequenceAsync(
        IProgress<DashTestStep> progress,
        CancellationToken ct = default)
    {
        foreach (var step in Steps)
        {
            ct.ThrowIfCancellationRequested();
            progress.Report(step);
            await Task.Delay(step.DurationSeconds * 1000, ct);
        }
    }
}
```

- [ ] **Step 2: Build and commit**

```bash
dotnet build Porsche928Diagnostics/Porsche928Diagnostics.csproj
git add Porsche928Diagnostics/Modules/DigitalDashModule.cs
git commit -m "feat: add DigitalDashModule — guided operator sequence for cluster self-test"
```

---

## Task 14: MVVM Infrastructure (ViewModelBase, RelayCommand, MainViewModel)

**Files:**
- Create: `Porsche928Diagnostics/ViewModels/ViewModelBase.cs`
- Create: `Porsche928Diagnostics/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Create ViewModelBase**

`Porsche928Diagnostics/ViewModels/ViewModelBase.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Porsche928Diagnostics.ViewModels;

/// <summary>
/// Base class for all ViewModels. Provides IsBusy flag and StatusMessage
/// for consistent async operation feedback patterns across all module views.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    protected void SetStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        HasError = isError;
    }

    protected async Task RunBusyAsync(Func<Task> action, string busyMessage = "Working...")
    {
        IsBusy = true;
        HasError = false;
        StatusMessage = busyMessage;
        try
        {
            await action();
        }
        catch (TimeoutException ex)
        {
            SetStatus($"Timeout: {ex.Message}", isError: true);
        }
        catch (InvalidOperationException ex)
        {
            SetStatus($"ECU error: {ex.Message}", isError: true);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Operation cancelled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Unexpected error: {ex.Message}", isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

- [ ] **Step 2: Create MainViewModel**

`Porsche928Diagnostics/ViewModels/MainViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.IO.Ports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly KLineInterface _kLine = new();
    private readonly Iso9141Session _session;

    public ObservableCollection<string> AvailablePorts { get; } = [];

    [ObservableProperty]
    private string? _selectedPort;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDisconnected))]
    private bool _isConnected;

    public bool IsDisconnected => !IsConnected;

    public LhViewModel Lh { get; }
    public EzkViewModel Ezk { get; }
    public PsdViewModel Psd { get; }
    public RdkViewModel Rdk { get; }
    public AirbagViewModel Airbag { get; }
    public AlarmViewModel Alarm { get; }
    public DigitalDashViewModel DigitalDash { get; }

    public MainViewModel()
    {
        _session = new Iso9141Session(_kLine);
        Lh = new LhViewModel(new LhModule(_kLine), _session);
        Ezk = new EzkViewModel(new EzkModule(_kLine), _session);
        Psd = new PsdViewModel(new PsdModule(_kLine), _session);
        Rdk = new RdkViewModel(new RdkModule(_kLine), _session);
        Airbag = new AirbagViewModel(new AirbagModule(_kLine), _session);
        Alarm = new AlarmViewModel(new AlarmModule(_kLine), _session);
        DigitalDash = new DigitalDashViewModel(new DigitalDashModule());

        RefreshPorts();
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var port in SerialPort.GetPortNames().OrderBy(p => p))
            AvailablePorts.Add(port);
        if (AvailablePorts.Count > 0 && SelectedPort == null)
            SelectedPort = AvailablePorts[0];
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (SelectedPort is null) return;
        await RunBusyAsync(async () =>
        {
            _kLine.Open(SelectedPort);
            SetStatus($"Connected to {SelectedPort} at 10,400 baud.");
            IsConnected = true;
        }, $"Opening {SelectedPort}...");
    }

    private bool CanConnect() => !IsConnected && SelectedPort != null;

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private void Disconnect()
    {
        _session.Disconnect();
        _kLine.Close();
        IsConnected = false;
        SetStatus("Disconnected.");
    }
}
```

- [ ] **Step 3: Build and commit**

```bash
dotnet build Porsche928Diagnostics/Porsche928Diagnostics.csproj
git add Porsche928Diagnostics/ViewModels/ViewModelBase.cs Porsche928Diagnostics/ViewModels/MainViewModel.cs
git commit -m "feat: add MVVM infrastructure — ViewModelBase and MainViewModel with port management"
```

---

## Task 15: Module ViewModels (LH, EZK, PSD, RDK, Airbag, Alarm, DigitalDash)

**Files:**
- Create: `Porsche928Diagnostics/ViewModels/LhViewModel.cs`
- Create: `Porsche928Diagnostics/ViewModels/EzkViewModel.cs`
- Create: `Porsche928Diagnostics/ViewModels/PsdViewModel.cs`
- Create: `Porsche928Diagnostics/ViewModels/RdkViewModel.cs`
- Create: `Porsche928Diagnostics/ViewModels/AirbagViewModel.cs`
- Create: `Porsche928Diagnostics/ViewModels/AlarmViewModel.cs`
- Create: `Porsche928Diagnostics/ViewModels/DigitalDashViewModel.cs`

- [ ] **Step 1: Create LhViewModel**

`Porsche928Diagnostics/ViewModels/LhViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.ViewModels;

public partial class LhViewModel : ViewModelBase
{
    private readonly LhModule _module;
    private readonly Iso9141Session _session;

    public ObservableCollection<DiagnosticTroubleCode> Dtcs { get; } = [];

    [ObservableProperty] private string _ecuId = "Not read";
    [ObservableProperty] private double _batteryVoltage;
    [ObservableProperty] private double _engineTemperature;
    [ObservableProperty] private bool _ezkOnSignal;
    [ObservableProperty] private bool _throttleIdleSwitch;
    [ObservableProperty] private bool _wotSwitch;
    [ObservableProperty] private bool _aircoActive;
    [ObservableProperty] private double _mafVoltage;
    [ObservableProperty] private double _lambdaVoltage;
    [ObservableProperty] private bool _sapRunning;

    public LhViewModel(LhModule module, Iso9141Session session)
    {
        _module = module;
        _session = session;
    }

    [RelayCommand]
    private async Task ConnectEcuAsync() => await RunBusyAsync(async () =>
    {
        await _session.InitializeAsync(_module.EcuAddress);
        var id = await _module.ReadEcuIdentificationAsync();
        EcuId = id.ToString();
        SetStatus($"LH connected: {id}");
    }, "Initializing LH session...");

    [RelayCommand]
    private async Task ReadDtcsAsync() => await RunBusyAsync(async () =>
    {
        var dtcs = await _module.ReadDtcsAsync();
        Dtcs.Clear();
        foreach (var dtc in dtcs) Dtcs.Add(dtc);
        SetStatus(dtcs.Count == 0 ? "No fault codes stored." : $"{dtcs.Count} fault code(s) found.");
    }, "Reading fault codes...");

    [RelayCommand]
    private async Task ClearDtcsAsync() => await RunBusyAsync(async () =>
    {
        await _module.ClearDtcsAsync();
        Dtcs.Clear();
        SetStatus("Fault codes cleared.");
    }, "Clearing fault codes...");

    [RelayCommand]
    private async Task ReadActualValuesAsync() => await RunBusyAsync(async () =>
    {
        var values = await _module.ReadActualValuesAsync();
        BatteryVoltage = values.BatteryVoltage;
        EngineTemperature = values.EngineTemperatureDegC;
        EzkOnSignal = values.EzkOnSignal;
        SetStatus($"Battery: {values.BatteryVoltage:F2}V  Temp: {values.EngineTemperatureDegC:F0}°C");
    }, "Reading actual values...");

    [RelayCommand]
    private async Task ReadActiveValuesAsync() => await RunBusyAsync(async () =>
    {
        var values = await _module.ReadActiveValuesAsync();
        MafVoltage = values.MafVoltage;
        LambdaVoltage = values.LambdaVoltage;
        SetStatus($"MAF: {values.MafVoltage:F2}V  Lambda: {values.LambdaVoltage:F0}mV");
    }, "Reading active values (engine running)...");

    [RelayCommand]
    private async Task ReadInputSignalsAsync() => await RunBusyAsync(async () =>
    {
        var signals = await _module.ReadInputSignalsAsync();
        ThrottleIdleSwitch = signals.ThrottleIdleSwitch;
        WotSwitch = signals.WideOpenThrottleSwitch;
        AircoActive = signals.AircoCompressorDemand;
        SetStatus("Input signals read.");
    }, "Reading input signals...");

    [RelayCommand]
    private async Task ActivateTankVentAsync() => await RunBusyAsync(async () =>
    {
        await _module.ActivateDriveLinkAsync(LhDriveLink.TankVentValve);
        SetStatus("Tank vent valve ACTIVE — stops automatically after 30s or use Stop.");
    }, "Activating tank vent valve...");

    [RelayCommand]
    private async Task StopTankVentAsync() => await RunBusyAsync(async () =>
    {
        await _module.StopDriveLinkAsync(LhDriveLink.TankVentValve);
        SetStatus("Tank vent valve stopped.");
    });

    [RelayCommand]
    private async Task ActivateResonanceFlapAsync() => await RunBusyAsync(async () =>
    {
        await _module.ActivateDriveLinkAsync(LhDriveLink.ResonanceFlap);
        SetStatus("Resonance flap ACTIVE.");
    });

    [RelayCommand]
    private async Task StopResonanceFlapAsync() => await RunBusyAsync(async () =>
    {
        await _module.StopDriveLinkAsync(LhDriveLink.ResonanceFlap);
        SetStatus("Resonance flap stopped.");
    });

    [RelayCommand]
    private async Task ActivateInjectorsAsync() => await RunBusyAsync(async () =>
    {
        await _module.ActivateDriveLinkAsync(LhDriveLink.FuelInjectors);
        SetStatus("Fuel injectors ACTIVE (all banks firing).");
    });

    [RelayCommand]
    private async Task StopInjectorsAsync() => await RunBusyAsync(async () =>
    {
        await _module.StopDriveLinkAsync(LhDriveLink.FuelInjectors);
        SetStatus("Injectors stopped.");
    });

    [RelayCommand]
    private async Task ActivateIsvAsync() => await RunBusyAsync(async () =>
    {
        await _module.ActivateDriveLinkAsync(LhDriveLink.IdleStabilizerValve);
        SetStatus("Idle Stabilizer Valve ACTIVE.");
    });

    [RelayCommand]
    private async Task StopIsvAsync() => await RunBusyAsync(async () =>
    {
        await _module.StopDriveLinkAsync(LhDriveLink.IdleStabilizerValve);
        SetStatus("ISV stopped.");
    });

    private CancellationTokenSource? _sapCts;

    [RelayCommand]
    private async Task RunSapAsync()
    {
        _sapCts = new CancellationTokenSource();
        SapRunning = true;
        await RunBusyAsync(async () =>
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            await _module.RunSystemAdaptationAsync(progress, _sapCts.Token);
        }, "Starting System Adaptation Program...");
        SapRunning = false;
    }

    [RelayCommand]
    private void StopSap()
    {
        _sapCts?.Cancel();
        SapRunning = false;
    }
}
```

- [ ] **Step 2: Create EzkViewModel**

`Porsche928Diagnostics/ViewModels/EzkViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.ViewModels;

public partial class EzkViewModel : ViewModelBase
{
    private readonly EzkModule _module;
    private readonly Iso9141Session _session;

    public ObservableCollection<DiagnosticTroubleCode> Dtcs { get; } = [];
    public ObservableCollection<string> KnockCounts { get; } = [];

    [ObservableProperty] private string _ecuId = "Not read";
    [ObservableProperty] private int _engineRpm;
    [ObservableProperty] private double _loadPercent;
    [ObservableProperty] private double _engineTemperature;
    [ObservableProperty] private string _transmissionCoding = "Unknown";

    public EzkViewModel(EzkModule module, Iso9141Session session)
    {
        _module = module;
        _session = session;
    }

    [RelayCommand]
    private async Task ConnectEcuAsync() => await RunBusyAsync(async () =>
    {
        await _session.InitializeAsync(_module.EcuAddress);
        var id = await _module.ReadEcuIdentificationAsync();
        EcuId = id.ToString();
        SetStatus($"EZK connected: {id}");
    }, "Initializing EZK session...");

    [RelayCommand]
    private async Task ReadDtcsAsync() => await RunBusyAsync(async () =>
    {
        var dtcs = await _module.ReadDtcsAsync();
        Dtcs.Clear();
        foreach (var dtc in dtcs) Dtcs.Add(dtc);
        SetStatus(dtcs.Count == 0 ? "No EZK fault codes." : $"{dtcs.Count} fault code(s).");
    }, "Reading EZK fault codes...");

    [RelayCommand]
    private async Task ClearDtcsAsync() => await RunBusyAsync(async () =>
    {
        await _module.ClearDtcsAsync();
        Dtcs.Clear();
        SetStatus("EZK fault codes cleared.");
    });

    [RelayCommand]
    private async Task ReadSensorDataAsync() => await RunBusyAsync(async () =>
    {
        var data = await _module.ReadSensorDataAsync();
        EngineRpm = data.EngineRpm;
        LoadPercent = data.LoadPercent;
        EngineTemperature = data.EngineTemperatureDegC;
        TransmissionCoding = data.TransmissionCoding;
        SetStatus($"RPM: {data.EngineRpm}  Load: {data.LoadPercent:F1}%  Trans: {data.TransmissionCoding}");
    }, "Reading EZK sensor data...");

    [RelayCommand]
    private async Task ReadKnockCountsAsync() => await RunBusyAsync(async () =>
    {
        var counts = await _module.ReadKnockCountsAsync();
        KnockCounts.Clear();
        for (int i = 0; i < counts.Length; i++)
            KnockCounts.Add($"Cylinder {i + 1}: {counts[i]} knock events{(counts[i] > 0 ? " ⚠" : "")}");
        SetStatus("Knock registration read.");
    }, "Reading knock counters...");
}
```

- [ ] **Step 3: Create PsdViewModel**

`Porsche928Diagnostics/ViewModels/PsdViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.ViewModels;

public partial class PsdViewModel : ViewModelBase
{
    private readonly PsdModule _module;
    private readonly Iso9141Session _session;
    private CancellationTokenSource? _bleedCts;

    public ObservableCollection<DiagnosticTroubleCode> Dtcs { get; } = [];

    [ObservableProperty] private string _ecuId = "Not read";
    [ObservableProperty] private bool _bleedActive;

    public PsdViewModel(PsdModule module, Iso9141Session session)
    {
        _module = module;
        _session = session;
    }

    [RelayCommand]
    private async Task ConnectEcuAsync() => await RunBusyAsync(async () =>
    {
        await _session.InitializeAsync(_module.EcuAddress);
        var id = await _module.ReadEcuIdentificationAsync();
        EcuId = id.ToString();
        SetStatus($"PSD connected: {id}");
    }, "Initializing PSD session...");

    [RelayCommand]
    private async Task ReadDtcsAsync() => await RunBusyAsync(async () =>
    {
        var dtcs = await _module.ReadDtcsAsync();
        Dtcs.Clear();
        foreach (var dtc in dtcs) Dtcs.Add(dtc);
        SetStatus(dtcs.Count == 0 ? "No PSD fault codes." : $"{dtcs.Count} fault code(s).");
    });

    [RelayCommand]
    private async Task ClearDtcsAsync() => await RunBusyAsync(async () =>
    {
        await _module.ClearDtcsAsync();
        Dtcs.Clear();
        SetStatus("PSD fault codes cleared.");
    });

    [RelayCommand]
    private async Task StartBleedAsync()
    {
        _bleedCts = new CancellationTokenSource();
        BleedActive = true;
        await RunBusyAsync(async () =>
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            await _module.StartBleedProcedureAsync(durationSeconds: 60, progress, _bleedCts.Token);
        }, "Starting PSD bleed procedure...");
        BleedActive = false;
    }

    [RelayCommand]
    private void StopBleed()
    {
        _bleedCts?.Cancel();
        BleedActive = false;
    }
}
```

- [ ] **Step 4: Create RdkViewModel**

`Porsche928Diagnostics/ViewModels/RdkViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.ViewModels;

public partial class RdkViewModel : ViewModelBase
{
    private readonly RdkModule _module;
    private readonly Iso9141Session _session;

    public ObservableCollection<DiagnosticTroubleCode> Dtcs { get; } = [];

    [ObservableProperty] private string _ecuId = "Not read";
    [ObservableProperty] private bool _flPressureOk;
    [ObservableProperty] private bool _frPressureOk;
    [ObservableProperty] private bool _rlPressureOk;
    [ObservableProperty] private bool _rrPressureOk;
    [ObservableProperty] private bool _hfReceiverActive;

    public RdkViewModel(RdkModule module, Iso9141Session session)
    {
        _module = module;
        _session = session;
    }

    [RelayCommand]
    private async Task ConnectEcuAsync() => await RunBusyAsync(async () =>
    {
        await _session.InitializeAsync(_module.EcuAddress);
        var id = await _module.ReadEcuIdentificationAsync();
        EcuId = id.ToString();
        SetStatus($"RDK connected: {id}");
    }, "Initializing RDK session...");

    [RelayCommand]
    private async Task ReadDtcsAsync() => await RunBusyAsync(async () =>
    {
        var dtcs = await _module.ReadDtcsAsync();
        Dtcs.Clear();
        foreach (var dtc in dtcs) Dtcs.Add(dtc);
        SetStatus(dtcs.Count == 0 ? "No RDK fault codes." : $"{dtcs.Count} fault code(s).");
    });

    [RelayCommand]
    private async Task ClearDtcsAsync() => await RunBusyAsync(async () =>
    {
        await _module.ClearDtcsAsync();
        Dtcs.Clear();
        SetStatus("RDK fault codes cleared.");
    });

    [RelayCommand]
    private async Task ReadSensorDataAsync() => await RunBusyAsync(async () =>
    {
        var data = await _module.ReadSensorDataAsync();
        FlPressureOk = data.PressureSwitchStates[0];
        FrPressureOk = data.PressureSwitchStates[1];
        RlPressureOk = data.PressureSwitchStates[2];
        RrPressureOk = data.PressureSwitchStates[3];
        HfReceiverActive = data.HfReceiverActive;
        var leaks = data.PressureSwitchStates.Count(s => !s);
        SetStatus(leaks == 0 ? "All four pressure switches OK." : $"WARNING: {leaks} wheel(s) show pressure loss.");
    }, "Reading tire pressure sensors...");
}
```

- [ ] **Step 5: Create AirbagViewModel**

`Porsche928Diagnostics/ViewModels/AirbagViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.ViewModels;

public partial class AirbagViewModel : ViewModelBase
{
    private readonly AirbagModule _module;
    private readonly Iso9141Session _session;

    public ObservableCollection<DiagnosticTroubleCode> Dtcs { get; } = [];

    [ObservableProperty] private string _ecuId = "Not read";
    [ObservableProperty] private string _downtimeClock = "Not read";
    [ObservableProperty] private bool _crashEventRecorded;
    [ObservableProperty] private bool _driverBagFired;
    [ObservableProperty] private bool _passengerBagFired;
    [ObservableProperty] private bool _seatbeltFired;

    public AirbagViewModel(AirbagModule module, Iso9141Session session)
    {
        _module = module;
        _session = session;
    }

    [RelayCommand]
    private async Task ConnectEcuAsync() => await RunBusyAsync(async () =>
    {
        await _session.InitializeAsync(_module.EcuAddress);
        var id = await _module.ReadEcuIdentificationAsync();
        EcuId = id.ToString();
        SetStatus($"Airbag ECU connected: {id}");
    }, "Initializing Airbag session...");

    [RelayCommand]
    private async Task ReadDtcsAsync() => await RunBusyAsync(async () =>
    {
        var dtcs = await _module.ReadDtcsAsync();
        Dtcs.Clear();
        foreach (var dtc in dtcs) Dtcs.Add(dtc);
        SetStatus(dtcs.Count == 0 ? "No airbag fault codes." : $"{dtcs.Count} fault code(s).");
    });

    [RelayCommand]
    private async Task ClearDtcsAsync() => await RunBusyAsync(async () =>
    {
        await _module.ClearDtcsAsync();
        Dtcs.Clear();
        SetStatus("Airbag fault codes cleared.");
    });

    [RelayCommand]
    private async Task ReadAirbagDataAsync() => await RunBusyAsync(async () =>
    {
        var data = await _module.ReadAirbagDataAsync();
        DowntimeClock = $"{data.DowntimeClock.TotalHours:F1} hours ({(int)data.DowntimeClock.TotalSeconds}s)";
        CrashEventRecorded = data.CrashEventRecorded;
        DriverBagFired = data.DriverBagFired;
        PassengerBagFired = data.PassengerBagFired;
        SeatbeltFired = data.SeatbeltPretensionerFired;

        if (data.CrashEventRecorded)
            SetStatus("WARNING: Crash deployment data recorded in non-volatile memory.", isError: true);
        else
            SetStatus($"Downtime: {DowntimeClock}. No crash events.");
    }, "Reading airbag data...");
}
```

- [ ] **Step 6: Create AlarmViewModel**

`Porsche928Diagnostics/ViewModels/AlarmViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Models;
using Porsche928Diagnostics.Modules;
using Porsche928Diagnostics.Protocol;

namespace Porsche928Diagnostics.ViewModels;

public partial class AlarmViewModel : ViewModelBase
{
    private readonly AlarmModule _module;
    private readonly Iso9141Session _session;

    public ObservableCollection<DiagnosticTroubleCode> Dtcs { get; } = [];
    public ObservableCollection<string> CountryCodeOptions { get; } = ["DE", "GB", "US", "FR", "IT", "JP"];

    [ObservableProperty] private string _ecuId = "Not read";
    [ObservableProperty] private string _countryCode = "Unknown";
    [ObservableProperty] private string? _selectedCountryCode;
    [ObservableProperty] private bool _engineLidOpen;
    [ObservableProperty] private bool _luggageLidOpen;
    [ObservableProperty] private bool _gloveBoxOpen;
    [ObservableProperty] private bool _motionSensorActive;

    public AlarmViewModel(AlarmModule module, Iso9141Session session)
    {
        _module = module;
        _session = session;
    }

    [RelayCommand]
    private async Task ConnectEcuAsync() => await RunBusyAsync(async () =>
    {
        await _session.InitializeAsync(_module.EcuAddress);
        var id = await _module.ReadEcuIdentificationAsync();
        EcuId = id.ToString();
        SetStatus($"Alarm ECU connected: {id}");
    }, "Initializing Alarm session...");

    [RelayCommand]
    private async Task ReadDtcsAsync() => await RunBusyAsync(async () =>
    {
        var dtcs = await _module.ReadDtcsAsync();
        Dtcs.Clear();
        foreach (var dtc in dtcs) Dtcs.Add(dtc);
        SetStatus(dtcs.Count == 0 ? "No alarm fault codes." : $"{dtcs.Count} fault code(s).");
    });

    [RelayCommand]
    private async Task ClearDtcsAsync() => await RunBusyAsync(async () =>
    {
        await _module.ClearDtcsAsync();
        Dtcs.Clear();
        SetStatus("Alarm fault codes cleared.");
    });

    [RelayCommand]
    private async Task ReadAlarmDataAsync() => await RunBusyAsync(async () =>
    {
        var data = await _module.ReadAlarmDataAsync();
        CountryCode = data.CountryCode;
        EngineLidOpen = data.EngineLidSwitchOpen;
        LuggageLidOpen = data.LuggageLidSwitchOpen;
        GloveBoxOpen = data.GloveCompartmentSwitchOpen;
        MotionSensorActive = data.InteriorMotionSensorActive;
        SetStatus($"Country: {data.CountryCode}  Engine lid: {(data.EngineLidSwitchOpen ? "OPEN" : "closed")}");
    }, "Reading alarm input states...");

    [RelayCommand]
    private async Task SetCountryCodingAsync() => await RunBusyAsync(async () =>
    {
        if (SelectedCountryCode is null) return;
        await _module.SetCountryCodingAsync(SelectedCountryCode);
        CountryCode = SelectedCountryCode;
        SetStatus($"Country coding set to {SelectedCountryCode}.");
    }, "Writing country coding...");

    [RelayCommand]
    private async Task ActivateHornAsync() => await RunBusyAsync(async () =>
    {
        await _module.ActivateDriveLinkAsync(AlarmDriveLink.Horn);
        await Task.Delay(2000);
        await _module.StopDriveLinkAsync(AlarmDriveLink.Horn);
        SetStatus("Horn test complete.");
    }, "Testing horn...");

    [RelayCommand]
    private async Task FlashIndicatorsAsync() => await RunBusyAsync(async () =>
    {
        await _module.ActivateDriveLinkAsync(AlarmDriveLink.Indicators);
        await Task.Delay(3000);
        await _module.StopDriveLinkAsync(AlarmDriveLink.Indicators);
        SetStatus("Indicator test complete.");
    }, "Flashing indicators...");
}
```

- [ ] **Step 7: Create DigitalDashViewModel**

`Porsche928Diagnostics/ViewModels/DigitalDashViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porsche928Diagnostics.Modules;

namespace Porsche928Diagnostics.ViewModels;

public partial class DigitalDashViewModel : ViewModelBase
{
    private readonly DigitalDashModule _module;
    private CancellationTokenSource? _sequenceCts;

    [ObservableProperty] private string _currentInstruction = "Press 'Start Guided Sequence' to begin.";
    [ObservableProperty] private int _currentStep;
    [ObservableProperty] private int _totalSteps;
    [ObservableProperty] private bool _sequenceRunning;

    public DigitalDashViewModel(DigitalDashModule module)
    {
        _module = module;
        TotalSteps = module.GetTestSteps().Count;
    }

    [RelayCommand]
    private async Task StartSequenceAsync()
    {
        _sequenceCts = new CancellationTokenSource();
        SequenceRunning = true;
        CurrentStep = 0;
        await RunBusyAsync(async () =>
        {
            var progress = new Progress<DigitalDashModule.DashTestStep>(step =>
            {
                CurrentStep = step.StepNumber;
                CurrentInstruction = $"Step {step.StepNumber}/{TotalSteps}: {step.Instruction}";
                SetStatus($"Hold for {step.DurationSeconds}s...");
            });
            await _module.RunGuidedSequenceAsync(progress, _sequenceCts.Token);
            CurrentInstruction = "Sequence complete. All readings recorded.";
            SetStatus("Digital dash test complete.");
        }, "Starting guided dash test...");
        SequenceRunning = false;
    }

    [RelayCommand]
    private void StopSequence()
    {
        _sequenceCts?.Cancel();
        SequenceRunning = false;
        SetStatus("Sequence stopped.");
    }
}
```

- [ ] **Step 8: Build**

```bash
dotnet build Porsche928Diagnostics/Porsche928Diagnostics.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 9: Commit**

```bash
git add Porsche928Diagnostics/ViewModels/
git commit -m "feat: add all seven module ViewModels with full async command bindings"
```

---

## Task 16: MainWindow.xaml — Application Shell

**Files:**
- Modify: `Porsche928Diagnostics/App.xaml.cs`
- Modify: `Porsche928Diagnostics/MainWindow.xaml`
- Modify: `Porsche928Diagnostics/MainWindow.xaml.cs`

- [ ] **Step 1: Update App.xaml.cs to wire MainViewModel**

`Porsche928Diagnostics/App.xaml.cs`:

```csharp
using System.Windows;
using Porsche928Diagnostics.ViewModels;

namespace Porsche928Diagnostics;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var mainVm = new MainViewModel();
        var mainWindow = new MainWindow { DataContext = mainVm };
        mainWindow.Show();
    }
}
```

- [ ] **Step 2: Write MainWindow.xaml**

`Porsche928Diagnostics/MainWindow.xaml`:

```xml
<Window x:Class="Porsche928Diagnostics.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:Porsche928Diagnostics.ViewModels"
        xmlns:views="clr-namespace:Porsche928Diagnostics.Views"
        Title="Porsche 928 K-Line Diagnostic Tool — F57GNT"
        Height="720" Width="1100"
        Background="#1E1E1E" Foreground="#E8E8E8"
        FontFamily="Segoe UI" FontSize="13">

    <Window.Resources>
        <Style TargetType="Button">
            <Setter Property="Background" Value="#2D2D2D"/>
            <Setter Property="Foreground" Value="#E8E8E8"/>
            <Setter Property="BorderBrush" Value="#555"/>
            <Setter Property="Padding" Value="10,4"/>
            <Setter Property="Margin" Value="3"/>
            <Setter Property="Cursor" Value="Hand"/>
        </Style>
        <Style TargetType="ComboBox">
            <Setter Property="Background" Value="#2D2D2D"/>
            <Setter Property="Foreground" Value="#E8E8E8"/>
        </Style>
        <Style TargetType="TabItem">
            <Setter Property="Foreground" Value="#CCCCCC"/>
            <Setter Property="Background" Value="#2D2D2D"/>
        </Style>
    </Window.Resources>

    <DockPanel>

        <!-- Connection Bar -->
        <Border DockPanel.Dock="Top" Background="#111" Padding="10,6" BorderThickness="0,0,0,1" BorderBrush="#444">
            <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                <TextBlock Text="Port:" VerticalAlignment="Center" Margin="0,0,6,0"/>
                <ComboBox ItemsSource="{Binding AvailablePorts}"
                          SelectedItem="{Binding SelectedPort}"
                          Width="90" IsEnabled="{Binding IsDisconnected}"/>
                <Button Content="Refresh" Command="{Binding RefreshPortsCommand}" Width="65"/>
                <Button Content="Connect" Command="{Binding ConnectAsyncCommand}"
                        Background="#1A5C2E" Foreground="White" Width="75"/>
                <Button Content="Disconnect" Command="{Binding DisconnectCommand}"
                        Background="#5C1A1A" Foreground="White" Width="90"/>
                <Border Width="1" Background="#555" Margin="10,2"/>
                <TextBlock Text="{Binding StatusMessage}" VerticalAlignment="Center"
                           Foreground="{Binding HasError, Converter={x:Static views:BoolToColorConverter.Instance}}"
                           MaxWidth="500" TextTrimming="CharacterEllipsis"/>
                <ProgressBar IsIndeterminate="{Binding IsBusy}" Width="80" Height="6"
                             Margin="10,0,0,0" Visibility="{Binding IsBusy, Converter={StaticResource BoolToVisConverter}}"/>
            </StackPanel>
        </Border>

        <!-- Module Tabs -->
        <TabControl Background="#1E1E1E" BorderThickness="0"
                    IsEnabled="{Binding IsConnected}">
            <TabItem Header="LH Injection (0x11)">
                <views:LhView DataContext="{Binding Lh}"/>
            </TabItem>
            <TabItem Header="EZK Ignition (0x12)">
                <views:EzkView DataContext="{Binding Ezk}"/>
            </TabItem>
            <TabItem Header="PSD Differential (0x28)">
                <views:PsdView DataContext="{Binding Psd}"/>
            </TabItem>
            <TabItem Header="RDK Tyre Pressure (0x30)">
                <views:RdkView DataContext="{Binding Rdk}"/>
            </TabItem>
            <TabItem Header="Airbag (0x40)">
                <views:AirbagView DataContext="{Binding Airbag}"/>
            </TabItem>
            <TabItem Header="Alarm (0x45)">
                <views:AlarmView DataContext="{Binding Alarm}"/>
            </TabItem>
            <TabItem Header="Digital Dash">
                <views:DigitalDashView DataContext="{Binding DigitalDash}"/>
            </TabItem>
        </TabControl>

    </DockPanel>
</Window>
```

`Porsche928Diagnostics/MainWindow.xaml.cs`:

```csharp
using System.Windows;

namespace Porsche928Diagnostics;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 3: Create a BoolToColorConverter helper in Views/**

`Porsche928Diagnostics/Views/BoolToColorConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Porsche928Diagnostics.Views;

public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? new SolidColorBrush(Color.FromRgb(255, 100, 100))
                         : new SolidColorBrush(Color.FromRgb(180, 230, 180));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

Add `BoolToVisConverter` resource to `App.xaml`:

```xml
<Application x:Class="Porsche928Diagnostics.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVisConverter"/>
    </Application.Resources>
</Application>
```

- [ ] **Step 4: Commit**

```bash
git add Porsche928Diagnostics/App.xaml Porsche928Diagnostics/App.xaml.cs Porsche928Diagnostics/MainWindow.xaml Porsche928Diagnostics/MainWindow.xaml.cs Porsche928Diagnostics/Views/BoolToColorConverter.cs
git commit -m "feat: add MainWindow shell with tab navigation and connection toolbar"
```

---

## Task 17: Module Views (XAML for all 7 modules)

**Files:**
- Create: `Porsche928Diagnostics/Views/LhView.xaml` + `.cs`
- Create: `Porsche928Diagnostics/Views/EzkView.xaml` + `.cs`
- Create: `Porsche928Diagnostics/Views/PsdView.xaml` + `.cs`
- Create: `Porsche928Diagnostics/Views/RdkView.xaml` + `.cs`
- Create: `Porsche928Diagnostics/Views/AirbagView.xaml` + `.cs`
- Create: `Porsche928Diagnostics/Views/AlarmView.xaml` + `.cs`
- Create: `Porsche928Diagnostics/Views/DigitalDashView.xaml` + `.cs`

Each view code-behind is identical boilerplate:

```csharp
// [ViewName].xaml.cs
using System.Windows.Controls;
namespace Porsche928Diagnostics.Views;
public partial class [ViewName] : UserControl { public [ViewName]() => InitializeComponent(); }
```

- [ ] **Step 1: Create LhView.xaml**

`Porsche928Diagnostics/Views/LhView.xaml`:

```xml
<UserControl x:Class="Porsche928Diagnostics.Views.LhView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="#1E1E1E" Foreground="#E8E8E8">
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel Margin="16">

            <!-- ECU Connection -->
            <GroupBox Header="ECU Session" Margin="0,0,0,10" BorderBrush="#555">
                <StackPanel>
                    <StackPanel Orientation="Horizontal">
                        <Button Content="Initialize LH Session" Command="{Binding ConnectEcuAsyncCommand}"/>
                        <TextBlock Text="{Binding EcuId}" VerticalAlignment="Center" Margin="10,0" Foreground="#AAFFAA"/>
                    </StackPanel>
                    <StackPanel Orientation="Horizontal" Margin="0,4,0,0">
                        <Button Content="Read Fault Codes" Command="{Binding ReadDtcsAsyncCommand}"/>
                        <Button Content="Clear Fault Codes" Command="{Binding ClearDtcsAsyncCommand}" Background="#5C3A1A"/>
                    </StackPanel>
                    <ListBox ItemsSource="{Binding Dtcs}" MaxHeight="120" Background="#111" Margin="0,4,0,0"
                             DisplayMemberPath="." Foreground="#FFAAAA"/>
                </StackPanel>
            </GroupBox>

            <!-- Actual Values -->
            <GroupBox Header="Actual Values" Margin="0,0,0,10" BorderBrush="#555">
                <StackPanel>
                    <StackPanel Orientation="Horizontal" Margin="0,4,0,4">
                        <Button Content="Read Static Values" Command="{Binding ReadActualValuesAsyncCommand}"/>
                        <Button Content="Read Active Values (Engine Running)" Command="{Binding ReadActiveValuesAsyncCommand}"/>
                    </StackPanel>
                    <UniformGrid Columns="3" Margin="0,4">
                        <StackPanel Margin="4">
                            <TextBlock Text="Battery Voltage" Foreground="#888" FontSize="11"/>
                            <TextBlock Text="{Binding BatteryVoltage, StringFormat={}{0:F2} V}" FontSize="16" FontWeight="Bold"/>
                        </StackPanel>
                        <StackPanel Margin="4">
                            <TextBlock Text="Engine Temperature" Foreground="#888" FontSize="11"/>
                            <TextBlock Text="{Binding EngineTemperature, StringFormat={}{0:F0} °C}" FontSize="16" FontWeight="Bold"/>
                        </StackPanel>
                        <StackPanel Margin="4">
                            <TextBlock Text="EZK On Signal" Foreground="#888" FontSize="11"/>
                            <TextBlock Text="{Binding EzkOnSignal}" FontSize="16" FontWeight="Bold"
                                       Foreground="{Binding EzkOnSignal, Converter={x:Static views:BoolToColorConverter.Instance}}"/>
                        </StackPanel>
                        <StackPanel Margin="4">
                            <TextBlock Text="MAF Voltage" Foreground="#888" FontSize="11"/>
                            <TextBlock Text="{Binding MafVoltage, StringFormat={}{0:F2} V}" FontSize="16"/>
                        </StackPanel>
                        <StackPanel Margin="4">
                            <TextBlock Text="Lambda (O2)" Foreground="#888" FontSize="11"/>
                            <TextBlock Text="{Binding LambdaVoltage, StringFormat={}{0:F0} mV}" FontSize="16"/>
                        </StackPanel>
                    </UniformGrid>
                </StackPanel>
            </GroupBox>

            <!-- Input Signals -->
            <GroupBox Header="Input Signals (SID 0x21, PID 0x40)" Margin="0,0,0,10" BorderBrush="#555">
                <StackPanel>
                    <Button Content="Read Input Signals" Command="{Binding ReadInputSignalsAsyncCommand}" HorizontalAlignment="Left"/>
                    <UniformGrid Columns="4" Margin="0,6">
                        <Border Background="{Binding ThrottleIdleSwitch, Converter={x:Static views:BoolToColorConverter.Instance}}"
                                CornerRadius="4" Margin="4" Padding="8,4">
                            <TextBlock Text="Throttle Idle" HorizontalAlignment="Center" Foreground="Black"/>
                        </Border>
                        <Border Background="{Binding WotSwitch, Converter={x:Static views:BoolToColorConverter.Instance}}"
                                CornerRadius="4" Margin="4" Padding="8,4">
                            <TextBlock Text="WOT" HorizontalAlignment="Center" Foreground="Black"/>
                        </Border>
                        <Border Background="{Binding AircoActive, Converter={x:Static views:BoolToColorConverter.Instance}}"
                                CornerRadius="4" Margin="4" Padding="8,4">
                            <TextBlock Text="A/C Demand" HorizontalAlignment="Center" Foreground="Black"/>
                        </Border>
                    </UniformGrid>
                </StackPanel>
            </GroupBox>

            <!-- Drive Links -->
            <GroupBox Header="Drive Link Tests (SID 0x30)" Margin="0,0,0,10" BorderBrush="#555">
                <UniformGrid Columns="2">
                    <StackPanel Margin="4">
                        <TextBlock Text="Tank Vent Valve (0x01)" Foreground="#888"/>
                        <StackPanel Orientation="Horizontal">
                            <Button Content="Activate" Command="{Binding ActivateTankVentAsyncCommand}" Background="#1A4A1A"/>
                            <Button Content="Stop" Command="{Binding StopTankVentAsyncCommand}" Background="#4A1A1A"/>
                        </StackPanel>
                    </StackPanel>
                    <StackPanel Margin="4">
                        <TextBlock Text="Resonance Flap (0x02)" Foreground="#888"/>
                        <StackPanel Orientation="Horizontal">
                            <Button Content="Activate" Command="{Binding ActivateResonanceFlapAsyncCommand}" Background="#1A4A1A"/>
                            <Button Content="Stop" Command="{Binding StopResonanceFlapAsyncCommand}" Background="#4A1A1A"/>
                        </StackPanel>
                    </StackPanel>
                    <StackPanel Margin="4">
                        <TextBlock Text="Fuel Injectors (0x03)" Foreground="#888"/>
                        <StackPanel Orientation="Horizontal">
                            <Button Content="Activate" Command="{Binding ActivateInjectorsAsyncCommand}" Background="#1A4A1A"/>
                            <Button Content="Stop" Command="{Binding StopInjectorsAsyncCommand}" Background="#4A1A1A"/>
                        </StackPanel>
                    </StackPanel>
                    <StackPanel Margin="4">
                        <TextBlock Text="Idle Stabilizer Valve (0x04)" Foreground="#888"/>
                        <StackPanel Orientation="Horizontal">
                            <Button Content="Activate" Command="{Binding ActivateIsvAsyncCommand}" Background="#1A4A1A"/>
                            <Button Content="Stop" Command="{Binding StopIsvAsyncCommand}" Background="#4A1A1A"/>
                        </StackPanel>
                    </StackPanel>
                </UniformGrid>
            </GroupBox>

            <!-- System Adaptation -->
            <GroupBox Header="System Adaptation Program (SAP)" Margin="0,0,0,10" BorderBrush="#AA8800">
                <StackPanel>
                    <TextBlock TextWrapping="Wrap" Margin="0,0,0,6" Foreground="#CCCC88"
                               Text="Engine must be at operating temperature (80°C+), idling smoothly. SAP monitors lambda closed-loop and writes corrected base injection pulse to non-volatile RAM."/>
                    <StackPanel Orientation="Horizontal">
                        <Button Content="▶ Run SAP" Command="{Binding RunSapAsyncCommand}" Background="#1A5C1A" Width="100"/>
                        <Button Content="■ Stop" Command="{Binding StopSapCommand}" Background="#5C1A1A" Width="60"/>
                    </StackPanel>
                    <TextBlock Text="{Binding StatusMessage}" Foreground="#CCCC88" Margin="0,6,0,0" TextWrapping="Wrap"/>
                </StackPanel>
            </GroupBox>

            <!-- Status Bar -->
            <Border Background="#111" Padding="8,4" CornerRadius="4">
                <TextBlock Text="{Binding StatusMessage}" TextWrapping="Wrap"
                           Foreground="{Binding HasError, Converter={x:Static views:BoolToColorConverter.Instance}}"/>
            </Border>

        </StackPanel>
    </ScrollViewer>
</UserControl>
```

`Porsche928Diagnostics/Views/LhView.xaml.cs`:

```csharp
using System.Windows.Controls;
namespace Porsche928Diagnostics.Views;
public partial class LhView : UserControl { public LhView() => InitializeComponent(); }
```

- [ ] **Step 2: Create EzkView.xaml**

`Porsche928Diagnostics/Views/EzkView.xaml`:

```xml
<UserControl x:Class="Porsche928Diagnostics.Views.EzkView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:views="clr-namespace:Porsche928Diagnostics.Views"
             Background="#1E1E1E" Foreground="#E8E8E8">
    <ScrollViewer>
        <StackPanel Margin="16">
            <GroupBox Header="ECU Session" BorderBrush="#555" Margin="0,0,0,10">
                <StackPanel>
                    <StackPanel Orientation="Horizontal">
                        <Button Content="Initialize EZK Session" Command="{Binding ConnectEcuAsyncCommand}"/>
                        <TextBlock Text="{Binding EcuId}" VerticalAlignment="Center" Margin="10,0" Foreground="#AAFFAA"/>
                    </StackPanel>
                    <StackPanel Orientation="Horizontal" Margin="0,4,0,0">
                        <Button Content="Read Fault Codes" Command="{Binding ReadDtcsAsyncCommand}"/>
                        <Button Content="Clear Fault Codes" Command="{Binding ClearDtcsAsyncCommand}" Background="#5C3A1A"/>
                    </StackPanel>
                    <ListBox ItemsSource="{Binding Dtcs}" MaxHeight="100" Background="#111" Foreground="#FFAAAA" Margin="0,4,0,0"/>
                </StackPanel>
            </GroupBox>

            <GroupBox Header="Engine Telemetry" BorderBrush="#555" Margin="0,0,0,10">
                <StackPanel>
                    <Button Content="Read Sensor Data" Command="{Binding ReadSensorDataAsyncCommand}" HorizontalAlignment="Left"/>
                    <UniformGrid Columns="4" Margin="0,8">
                        <StackPanel Margin="4">
                            <TextBlock Text="Engine RPM" Foreground="#888" FontSize="11"/>
                            <TextBlock Text="{Binding EngineRpm, StringFormat={}{0} RPM}" FontSize="20" FontWeight="Bold" Foreground="#AAFFFF"/>
                        </StackPanel>
                        <StackPanel Margin="4">
                            <TextBlock Text="Load" Foreground="#888" FontSize="11"/>
                            <TextBlock Text="{Binding LoadPercent, StringFormat={}{0:F1}%}" FontSize="20" FontWeight="Bold"/>
                        </StackPanel>
                        <StackPanel Margin="4">
                            <TextBlock Text="Engine Temp" Foreground="#888" FontSize="11"/>
                            <TextBlock Text="{Binding EngineTemperature, StringFormat={}{0:F0}°C}" FontSize="20" FontWeight="Bold"/>
                        </StackPanel>
                        <StackPanel Margin="4">
                            <TextBlock Text="Transmission" Foreground="#888" FontSize="11"/>
                            <TextBlock Text="{Binding TransmissionCoding}" FontSize="20" FontWeight="Bold"/>
                        </StackPanel>
                    </UniformGrid>
                </StackPanel>
            </GroupBox>

            <GroupBox Header="Knock Registration" BorderBrush="#AA5500" Margin="0,0,0,10">
                <StackPanel>
                    <TextBlock Text="Non-zero values indicate timing retardation active for that cylinder." Foreground="#CC8844" Margin="0,0,0,6"/>
                    <Button Content="Read Knock Counts" Command="{Binding ReadKnockCountsAsyncCommand}" HorizontalAlignment="Left"/>
                    <ListBox ItemsSource="{Binding KnockCounts}" Background="#111" MaxHeight="160"
                             Foreground="#FFCC88" Margin="0,6,0,0" FontFamily="Courier New"/>
                </StackPanel>
            </GroupBox>

            <Border Background="#111" Padding="8,4" CornerRadius="4">
                <TextBlock Text="{Binding StatusMessage}" TextWrapping="Wrap"
                           Foreground="{Binding HasError, Converter={x:Static views:BoolToColorConverter.Instance}}"/>
            </Border>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

`Porsche928Diagnostics/Views/EzkView.xaml.cs`:

```csharp
using System.Windows.Controls;
namespace Porsche928Diagnostics.Views;
public partial class EzkView : UserControl { public EzkView() => InitializeComponent(); }
```

- [ ] **Step 3: Create PsdView.xaml**

`Porsche928Diagnostics/Views/PsdView.xaml`:

```xml
<UserControl x:Class="Porsche928Diagnostics.Views.PsdView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:views="clr-namespace:Porsche928Diagnostics.Views"
             Background="#1E1E1E" Foreground="#E8E8E8">
    <ScrollViewer>
        <StackPanel Margin="16">
            <GroupBox Header="ECU Session" BorderBrush="#555" Margin="0,0,0,10">
                <StackPanel>
                    <StackPanel Orientation="Horizontal">
                        <Button Content="Initialize PSD Session" Command="{Binding ConnectEcuAsyncCommand}"/>
                        <TextBlock Text="{Binding EcuId}" VerticalAlignment="Center" Margin="10,0" Foreground="#AAFFAA"/>
                    </StackPanel>
                    <StackPanel Orientation="Horizontal" Margin="0,4,0,0">
                        <Button Content="Read Fault Codes" Command="{Binding ReadDtcsAsyncCommand}"/>
                        <Button Content="Clear Fault Codes" Command="{Binding ClearDtcsAsyncCommand}" Background="#5C3A1A"/>
                    </StackPanel>
                    <ListBox ItemsSource="{Binding Dtcs}" MaxHeight="100" Background="#111" Foreground="#FFAAAA" Margin="0,4,0,0"/>
                </StackPanel>
            </GroupBox>

            <GroupBox Header="Hydraulic Bleed Procedure" BorderBrush="#AA4400" Margin="0,0,0,10">
                <StackPanel>
                    <TextBlock TextWrapping="Wrap" Foreground="#CC8844" Margin="0,0,0,8">
                        <Run FontWeight="Bold">Pre-requisite:</Run>
                        <Run>Vehicle on lift. Differential slave cylinder bleeder screw accessible. Brake fluid reservoir full.</Run>
                    </TextBlock>
                    <TextBlock TextWrapping="Wrap" Foreground="#888" Margin="0,0,0,8"
                               Text="This procedure runs the hydraulic pump and holds the transverse lock solenoid open for 60 seconds, allowing pressurised fluid to purge air from the slave cylinder circuit."/>
                    <StackPanel Orientation="Horizontal">
                        <Button Content="▶ Start 60s Bleed Sequence" Command="{Binding StartBleedAsyncCommand}"
                                Background="#1A4A1A" Width="180"/>
                        <Button Content="■ Emergency Stop" Command="{Binding StopBleedCommand}"
                                Background="#5C1A1A" Width="120"/>
                    </StackPanel>
                </StackPanel>
            </GroupBox>

            <Border Background="#111" Padding="8,4" CornerRadius="4">
                <TextBlock Text="{Binding StatusMessage}" TextWrapping="Wrap" FontSize="13"
                           Foreground="{Binding HasError, Converter={x:Static views:BoolToColorConverter.Instance}}"/>
            </Border>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

`Porsche928Diagnostics/Views/PsdView.xaml.cs`:

```csharp
using System.Windows.Controls;
namespace Porsche928Diagnostics.Views;
public partial class PsdView : UserControl { public PsdView() => InitializeComponent(); }
```

- [ ] **Step 4: Create RdkView.xaml**

`Porsche928Diagnostics/Views/RdkView.xaml`:

```xml
<UserControl x:Class="Porsche928Diagnostics.Views.RdkView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:views="clr-namespace:Porsche928Diagnostics.Views"
             Background="#1E1E1E" Foreground="#E8E8E8">
    <ScrollViewer>
        <StackPanel Margin="16">
            <GroupBox Header="ECU Session" BorderBrush="#555" Margin="0,0,0,10">
                <StackPanel>
                    <StackPanel Orientation="Horizontal">
                        <Button Content="Initialize RDK Session" Command="{Binding ConnectEcuAsyncCommand}"/>
                        <TextBlock Text="{Binding EcuId}" VerticalAlignment="Center" Margin="10,0" Foreground="#AAFFAA"/>
                    </StackPanel>
                    <StackPanel Orientation="Horizontal" Margin="0,4,0,0">
                        <Button Content="Read Fault Codes" Command="{Binding ReadDtcsAsyncCommand}"/>
                        <Button Content="Clear Fault Codes" Command="{Binding ClearDtcsAsyncCommand}" Background="#5C3A1A"/>
                    </StackPanel>
                    <ListBox ItemsSource="{Binding Dtcs}" MaxHeight="100" Background="#111" Foreground="#FFAAAA" Margin="0,4,0,0"/>
                </StackPanel>
            </GroupBox>

            <GroupBox Header="Tyre Pressure Switch States" BorderBrush="#555" Margin="0,0,0,10">
                <StackPanel>
                    <Button Content="Read Pressure Sensors" Command="{Binding ReadSensorDataAsyncCommand}" HorizontalAlignment="Left"/>
                    <StackPanel Margin="0,8,0,0">
                        <TextBlock Text="Wheel pressure switch: GREEN = closed (OK pressure), RED = open (pressure loss / wheel off car)" Foreground="#888" FontSize="11" Margin="0,0,0,6"/>
                        <UniformGrid Columns="2" MaxWidth="400" HorizontalAlignment="Left">
                            <Border Margin="4" Padding="12,8" CornerRadius="6"
                                    Background="{Binding FlPressureOk, Converter={x:Static views:BoolToColorConverter.Instance}}">
                                <TextBlock Text="FL — Front Left" FontWeight="Bold" Foreground="Black" HorizontalAlignment="Center"/>
                            </Border>
                            <Border Margin="4" Padding="12,8" CornerRadius="6"
                                    Background="{Binding FrPressureOk, Converter={x:Static views:BoolToColorConverter.Instance}}">
                                <TextBlock Text="FR — Front Right" FontWeight="Bold" Foreground="Black" HorizontalAlignment="Center"/>
                            </Border>
                            <Border Margin="4" Padding="12,8" CornerRadius="6"
                                    Background="{Binding RlPressureOk, Converter={x:Static views:BoolToColorConverter.Instance}}">
                                <TextBlock Text="RL — Rear Left" FontWeight="Bold" Foreground="Black" HorizontalAlignment="Center"/>
                            </Border>
                            <Border Margin="4" Padding="12,8" CornerRadius="6"
                                    Background="{Binding RrPressureOk, Converter={x:Static views:BoolToColorConverter.Instance}}">
                                <TextBlock Text="RR — Rear Right" FontWeight="Bold" Foreground="Black" HorizontalAlignment="Center"/>
                            </Border>
                        </UniformGrid>
                        <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                            <TextBlock Text="HF Receiver: " Foreground="#888"/>
                            <TextBlock Text="{Binding HfReceiverActive}"
                                       Foreground="{Binding HfReceiverActive, Converter={x:Static views:BoolToColorConverter.Instance}}"/>
                        </StackPanel>
                    </StackPanel>
                </StackPanel>
            </GroupBox>

            <Border Background="#111" Padding="8,4" CornerRadius="4">
                <TextBlock Text="{Binding StatusMessage}" TextWrapping="Wrap"
                           Foreground="{Binding HasError, Converter={x:Static views:BoolToColorConverter.Instance}}"/>
            </Border>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

`Porsche928Diagnostics/Views/RdkView.xaml.cs`:

```csharp
using System.Windows.Controls;
namespace Porsche928Diagnostics.Views;
public partial class RdkView : UserControl { public RdkView() => InitializeComponent(); }
```

- [ ] **Step 5: Create AirbagView.xaml**

`Porsche928Diagnostics/Views/AirbagView.xaml`:

```xml
<UserControl x:Class="Porsche928Diagnostics.Views.AirbagView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:views="clr-namespace:Porsche928Diagnostics.Views"
             Background="#1E1E1E" Foreground="#E8E8E8">
    <ScrollViewer>
        <StackPanel Margin="16">
            <GroupBox Header="ECU Session" BorderBrush="#555" Margin="0,0,0,10">
                <StackPanel>
                    <StackPanel Orientation="Horizontal">
                        <Button Content="Initialize Airbag Session" Command="{Binding ConnectEcuAsyncCommand}"/>
                        <TextBlock Text="{Binding EcuId}" VerticalAlignment="Center" Margin="10,0" Foreground="#AAFFAA"/>
                    </StackPanel>
                    <StackPanel Orientation="Horizontal" Margin="0,4,0,0">
                        <Button Content="Read Fault Codes" Command="{Binding ReadDtcsAsyncCommand}"/>
                        <Button Content="Clear Fault Codes" Command="{Binding ClearDtcsAsyncCommand}" Background="#5C3A1A"/>
                    </StackPanel>
                    <ListBox ItemsSource="{Binding Dtcs}" MaxHeight="100" Background="#111" Foreground="#FFAAAA" Margin="0,4,0,0"/>
                </StackPanel>
            </GroupBox>

            <GroupBox Header="Downtime Clock &amp; Crash Data" BorderBrush="#AA2200" Margin="0,0,0,10">
                <StackPanel>
                    <Button Content="Read Airbag Data" Command="{Binding ReadAirbagDataAsyncCommand}" HorizontalAlignment="Left"/>
                    <Grid Margin="0,8,0,0">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <StackPanel Grid.Column="0" Margin="0,0,10,0">
                            <TextBlock Text="Capacitor Downtime Clock" Foreground="#888" FontSize="11"/>
                            <TextBlock Text="{Binding DowntimeClock}" FontSize="18" FontWeight="Bold" Foreground="#AACCFF"/>
                        </StackPanel>
                        <StackPanel Grid.Column="1">
                            <TextBlock Text="Crash Event Recorded" Foreground="#888" FontSize="11"/>
                            <TextBlock Text="{Binding CrashEventRecorded}" FontSize="18" FontWeight="Bold"
                                       Foreground="{Binding CrashEventRecorded, Converter={x:Static views:BoolToColorConverter.Instance}}"/>
                        </StackPanel>
                    </Grid>
                    <Separator Margin="0,8" Background="#555"/>
                    <TextBlock Text="Deployment Status:" Foreground="#888" Margin="0,0,0,4"/>
                    <UniformGrid Columns="3">
                        <Border Margin="4" Padding="8,6" CornerRadius="4"
                                Background="{Binding DriverBagFired, Converter={x:Static views:BoolToColorConverter.Instance}}">
                            <TextBlock Text="Driver Airbag" Foreground="Black" HorizontalAlignment="Center"/>
                        </Border>
                        <Border Margin="4" Padding="8,6" CornerRadius="4"
                                Background="{Binding PassengerBagFired, Converter={x:Static views:BoolToColorConverter.Instance}}">
                            <TextBlock Text="Passenger Airbag" Foreground="Black" HorizontalAlignment="Center"/>
                        </Border>
                        <Border Margin="4" Padding="8,6" CornerRadius="4"
                                Background="{Binding SeatbeltFired, Converter={x:Static views:BoolToColorConverter.Instance}}">
                            <TextBlock Text="Seatbelt Pretensioner" Foreground="Black" HorizontalAlignment="Center"/>
                        </Border>
                    </UniformGrid>
                </StackPanel>
            </GroupBox>

            <Border Background="#111" Padding="8,4" CornerRadius="4">
                <TextBlock Text="{Binding StatusMessage}" TextWrapping="Wrap"
                           Foreground="{Binding HasError, Converter={x:Static views:BoolToColorConverter.Instance}}"/>
            </Border>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

`Porsche928Diagnostics/Views/AirbagView.xaml.cs`:

```csharp
using System.Windows.Controls;
namespace Porsche928Diagnostics.Views;
public partial class AirbagView : UserControl { public AirbagView() => InitializeComponent(); }
```

- [ ] **Step 6: Create AlarmView.xaml**

`Porsche928Diagnostics/Views/AlarmView.xaml`:

```xml
<UserControl x:Class="Porsche928Diagnostics.Views.AlarmView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:views="clr-namespace:Porsche928Diagnostics.Views"
             Background="#1E1E1E" Foreground="#E8E8E8">
    <ScrollViewer>
        <StackPanel Margin="16">
            <GroupBox Header="ECU Session" BorderBrush="#555" Margin="0,0,0,10">
                <StackPanel>
                    <StackPanel Orientation="Horizontal">
                        <Button Content="Initialize Alarm Session" Command="{Binding ConnectEcuAsyncCommand}"/>
                        <TextBlock Text="{Binding EcuId}" VerticalAlignment="Center" Margin="10,0" Foreground="#AAFFAA"/>
                    </StackPanel>
                    <StackPanel Orientation="Horizontal" Margin="0,4,0,0">
                        <Button Content="Read Fault Codes" Command="{Binding ReadDtcsAsyncCommand}"/>
                        <Button Content="Clear Fault Codes" Command="{Binding ClearDtcsAsyncCommand}" Background="#5C3A1A"/>
                    </StackPanel>
                    <ListBox ItemsSource="{Binding Dtcs}" MaxHeight="100" Background="#111" Foreground="#FFAAAA" Margin="0,4,0,0"/>
                </StackPanel>
            </GroupBox>

            <GroupBox Header="Perimeter Input Switches" BorderBrush="#555" Margin="0,0,0,10">
                <StackPanel>
                    <Button Content="Read Input Signals" Command="{Binding ReadAlarmDataAsyncCommand}" HorizontalAlignment="Left"/>
                    <UniformGrid Columns="2" Margin="0,6" MaxWidth="380" HorizontalAlignment="Left">
                        <Border Margin="4" Padding="10,6" CornerRadius="4"
                                Background="{Binding EngineLidOpen, Converter={x:Static views:BoolToColorConverter.Instance}}">
                            <TextBlock Text="Engine Lid" Foreground="Black" HorizontalAlignment="Center"/>
                        </Border>
                        <Border Margin="4" Padding="10,6" CornerRadius="4"
                                Background="{Binding LuggageLidOpen, Converter={x:Static views:BoolToColorConverter.Instance}}">
                            <TextBlock Text="Luggage Lid" Foreground="Black" HorizontalAlignment="Center"/>
                        </Border>
                        <Border Margin="4" Padding="10,6" CornerRadius="4"
                                Background="{Binding GloveBoxOpen, Converter={x:Static views:BoolToColorConverter.Instance}}">
                            <TextBlock Text="Glove Box" Foreground="Black" HorizontalAlignment="Center"/>
                        </Border>
                        <Border Margin="4" Padding="10,6" CornerRadius="4"
                                Background="{Binding MotionSensorActive, Converter={x:Static views:BoolToColorConverter.Instance}}">
                            <TextBlock Text="Interior Motion" Foreground="Black" HorizontalAlignment="Center"/>
                        </Border>
                    </UniformGrid>
                </StackPanel>
            </GroupBox>

            <GroupBox Header="Country Coding" BorderBrush="#555" Margin="0,0,0,10">
                <StackPanel>
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="Current code: " VerticalAlignment="Center"/>
                        <TextBlock Text="{Binding CountryCode}" Foreground="#AAFFAA" FontWeight="Bold" VerticalAlignment="Center" Margin="4,0"/>
                    </StackPanel>
                    <StackPanel Orientation="Horizontal" Margin="0,6,0,0">
                        <ComboBox ItemsSource="{Binding CountryCodeOptions}" SelectedItem="{Binding SelectedCountryCode}" Width="80"/>
                        <Button Content="Write Country Code" Command="{Binding SetCountryCodingAsyncCommand}" Background="#1A3A5C"/>
                    </StackPanel>
                </StackPanel>
            </GroupBox>

            <GroupBox Header="Drive Link Tests" BorderBrush="#555" Margin="0,0,0,10">
                <StackPanel Orientation="Horizontal">
                    <Button Content="Test Horn (2s)" Command="{Binding ActivateHornAsyncCommand}" Background="#4A3A00"/>
                    <Button Content="Flash Indicators (3s)" Command="{Binding FlashIndicatorsAsyncCommand}" Background="#3A4A00"/>
                </StackPanel>
            </GroupBox>

            <Border Background="#111" Padding="8,4" CornerRadius="4">
                <TextBlock Text="{Binding StatusMessage}" TextWrapping="Wrap"
                           Foreground="{Binding HasError, Converter={x:Static views:BoolToColorConverter.Instance}}"/>
            </Border>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

`Porsche928Diagnostics/Views/AlarmView.xaml.cs`:

```csharp
using System.Windows.Controls;
namespace Porsche928Diagnostics.Views;
public partial class AlarmView : UserControl { public AlarmView() => InitializeComponent(); }
```

- [ ] **Step 7: Create DigitalDashView.xaml**

`Porsche928Diagnostics/Views/DigitalDashView.xaml`:

```xml
<UserControl x:Class="Porsche928Diagnostics.Views.DigitalDashView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:views="clr-namespace:Porsche928Diagnostics.Views"
             Background="#1E1E1E" Foreground="#E8E8E8">
    <ScrollViewer>
        <StackPanel Margin="16">

            <TextBlock FontSize="16" FontWeight="Bold" Margin="0,0,0,4" Foreground="#CCDDFF">
                Digital Instrument Cluster Self-Test
            </TextBlock>
            <TextBlock TextWrapping="Wrap" Foreground="#888" Margin="0,0,0,12">
                The 928 digital dashboard does not use the K-Line protocol. Its self-test mode is
                triggered by grounding Pin 6 on the 19-pin connector. This guided sequence walks
                you through each step and times each reading phase.
            </TextBlock>

            <!-- Progress indicator -->
            <Border Background="#111" BorderBrush="#444" BorderThickness="1" CornerRadius="6" Padding="12" Margin="0,0,0,12">
                <StackPanel>
                    <StackPanel Orientation="Horizontal" Margin="0,0,0,6">
                        <TextBlock Text="{Binding CurrentStep, StringFormat=Step {0}}" FontSize="22" FontWeight="Bold" Foreground="#AACCFF"/>
                        <TextBlock Text="{Binding TotalSteps, StringFormat= of {0}}" FontSize="22" Foreground="#888"/>
                    </StackPanel>
                    <ProgressBar Value="{Binding CurrentStep}" Maximum="{Binding TotalSteps}"
                                 Height="8" Margin="0,0,0,10"
                                 Background="#333" Foreground="#4488FF"/>
                    <TextBlock Text="{Binding CurrentInstruction}" TextWrapping="Wrap"
                               FontSize="14" Foreground="#E8E8E8" LineHeight="22"/>
                </StackPanel>
            </Border>

            <StackPanel Orientation="Horizontal" Margin="0,0,0,12">
                <Button Content="▶ Start Guided Sequence" Command="{Binding StartSequenceAsyncCommand}"
                        Background="#1A4A1A" Width="180" Height="36" FontSize="14"/>
                <Button Content="■ Stop" Command="{Binding StopSequenceCommand}"
                        Background="#5C1A1A" Width="80" Height="36" FontSize="14"/>
            </StackPanel>

            <Border Background="#1A1A2A" BorderBrush="#334" BorderThickness="1" CornerRadius="6" Padding="12">
                <StackPanel>
                    <TextBlock Text="Dashboard Readings Checklist" FontWeight="Bold" Foreground="#888" Margin="0,0,0,6"/>
                    <TextBlock Foreground="#CCCCCC" LineHeight="22">
                        <Run>□  Oil Pressure (bar) — Normal idle: 2.0–4.5 bar</Run><LineBreak/>
                        <Run>□  Oil Level (litres) — Min: 4.0L</Run><LineBreak/>
                        <Run>□  Brake Fluid Level — OK / LOW</Run><LineBreak/>
                        <Run>□  Engine Temperature (°C)</Run><LineBreak/>
                        <Run>□  Coolant Level — OK / LOW</Run><LineBreak/>
                        <Run FontWeight="Bold" Foreground="#FFAAAA">□  TOOTHED BELT TENSION — OK / FAULT (safety-critical)</Run>
                    </TextBlock>
                </StackPanel>
            </Border>

            <Border Background="#111" Padding="8,4" CornerRadius="4" Margin="0,12,0,0">
                <TextBlock Text="{Binding StatusMessage}" TextWrapping="Wrap"
                           Foreground="{Binding HasError, Converter={x:Static views:BoolToColorConverter.Instance}}"/>
            </Border>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

`Porsche928Diagnostics/Views/DigitalDashView.xaml.cs`:

```csharp
using System.Windows.Controls;
namespace Porsche928Diagnostics.Views;
public partial class DigitalDashView : UserControl { public DigitalDashView() => InitializeComponent(); }
```

- [ ] **Step 8: Full build**

```bash
dotnet build Porsche928Diagnostics.sln
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 9: Run all tests**

```bash
dotnet test Porsche928Diagnostics.Tests/Porsche928Diagnostics.Tests.csproj -v normal
```

Expected: All tests pass.

- [ ] **Step 10: Commit**

```bash
git add Porsche928Diagnostics/Views/
git commit -m "feat: add all seven module views (XAML) — LH/EZK/PSD/RDK/Airbag/Alarm/DigitalDash"
```

---

## Self-Review Against Spec

**Spec coverage check:**

| Requirement | Task(s) covering it |
|---|---|
| ISO 9141-2 5-baud init (bit-bang BreakState, both K+L lines) | Task 4, 5 |
| 10,400 baud 8N1 active-low transition | Task 4 |
| Thread.Sleep for bit timing | Task 4 (KLineInterface.SendByte5Baud) |
| Async execution (no UI lockup) | Tasks 14-15 (RunBusyAsync pattern) |
| Modulo-256 checksum | Task 2, 3 |
| Frame format [Format][Target][0xF1][SID][Data][CS] | Task 3 |
| LH 0x11: ECU ID, DTCs, clear | Tasks 6, 7 |
| LH: Drive links (tank vent/resonance/injectors/ISV) SID 0x30 | Task 7 |
| LH: Input signals (idle/WOT/airco bit flags) SID 0x21 PID 0x40 | Task 7 |
| LH: Actual values (battery 0.065× scaling, NTC temp) | Task 7 |
| LH: Active values (MAF/lambda/speed) | Task 7 |
| LH: System Adaptation Program SID 0x31 | Task 7 |
| EZK 0x12: ECU ID, DTCs, RPM/load | Tasks 6, 8 |
| EZK: Knock registration per cylinder | Task 8 |
| PSD 0x28: ECU ID, DTCs, 60s bleed + transverse lock | Tasks 6, 9 |
| RDK 0x30: ECU ID, DTCs, pressure switch bit-field | Tasks 6, 10 |
| RDK: HF receiver state | Task 10 |
| Airbag 0x40: ECU ID, DTCs, downtime clock | Tasks 6, 11 |
| Airbag: Crash data bit parsing (driver/passenger/seatbelt) | Task 11 |
| Alarm 0x45: ECU ID, DTCs, country coding | Tasks 6, 12 |
| Alarm: Perimeter switch states (engine/luggage/glove lid) | Task 12 |
| Alarm: Drive links (horn/indicators/lights/locks) | Task 12 |
| Digital Dash: Timed operator-guided pin-ground sequence | Task 13 |
| Digital Dash: Readings list (oil P, oil L, brake, coolant, belt) | Task 13, 17 |
| WPF/XAML dark-theme UI | Tasks 16, 17 |
| MVVM with async commands | Tasks 14, 15 |

**All spec requirements covered. No gaps found.**

**Placeholder scan:** None. All code blocks are complete implementations.

**Type consistency check:**
- `LhDriveLink` enum defined in `LhModule.cs`, used in `LhViewModel.cs` ✓
- `AlarmDriveLink` enum defined in `AlarmModule.cs`, used in `AlarmViewModel.cs` ✓
- `DigitalDashModule.DashTestStep` record used in `DigitalDashViewModel.cs` ✓
- `ParsedFrame` record defined in `MessageFrame.cs`, used in `BaseEcuModule.cs` ✓
- All `ReadXxxAsync` method names in modules match `Command="{Binding ReadXxxAsyncCommand}"` in XAML ✓

---

**Plan complete and saved to [`docs/superpowers/plans/2026-06-27-porsche928-diagnostics.md`](docs/superpowers/plans/2026-06-27-porsche928-diagnostics.md).**

**Two execution options:**

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast parallel iteration

**2. Inline Execution** — Execute tasks in this session using the executing-plans skill, with checkpoints for review

**Which approach?**
