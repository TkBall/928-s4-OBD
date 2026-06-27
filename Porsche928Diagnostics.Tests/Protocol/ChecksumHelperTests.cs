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
