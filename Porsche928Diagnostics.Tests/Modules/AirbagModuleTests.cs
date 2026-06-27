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
        // 0x00 0x0E 0x10 = 3600 seconds
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
        // Crash byte: 0x03 = bits 0+1 = driver + passenger bags fired
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
