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
        // Pressure byte: 0b00000101 = 0x05, bits FL(0), RL(2) set = OK; FR(1), RR(3) not set = LEAK
        byte[] response = BuildResponseFrame(0x61, [0x05, 0x01, 0x00, 0x00, 0x00, 0x00]);
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
