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
        // RPM = (high << 8 | low) = (0x0C << 8 | 0xA0) = 3232
        byte[] response = BuildResponseFrame(0x61, [0x0C, 0xA0, 0x80, 0x6A, 0x01, 0x00, 0x01]);
        SetupMockResponse(response);

        var data = await _ezk.ReadSensorDataAsync();

        data.EngineRpm.Should().Be(3232);
        data.LoadPercent.Should().BeApproximately(50.2, 0.5);
        data.TransmissionCoding.Should().Be("Manual");
    }

    [Fact]
    public async Task ReadKnockCountsAsync_ReturnsEightCylinders()
    {
        byte[] knockBytes = [0x00, 0x03, 0x00, 0x01, 0x00, 0x00, 0x07, 0x00];
        byte[] response = BuildResponseFrame(0x61, knockBytes);
        SetupMockResponse(response);

        var counts = await _ezk.ReadKnockCountsAsync();

        counts.Should().HaveCount(8);
        counts[1].Should().Be(3);
        counts[6].Should().Be(7);
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
