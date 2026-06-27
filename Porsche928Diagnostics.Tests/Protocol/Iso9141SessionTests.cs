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
