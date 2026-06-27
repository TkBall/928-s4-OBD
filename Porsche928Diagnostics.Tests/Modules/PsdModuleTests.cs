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
