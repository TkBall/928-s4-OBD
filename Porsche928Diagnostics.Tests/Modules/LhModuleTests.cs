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
