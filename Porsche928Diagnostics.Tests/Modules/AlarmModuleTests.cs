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
        // Switch byte 0x05: bits 0+2 = engine lid + glove box open
        // Country code bytes: 0x44='D', 0x45='E' => "DE"
        byte[] response = BuildResponseFrame(0x61, [0x05, 0x44, 0x45]);
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
