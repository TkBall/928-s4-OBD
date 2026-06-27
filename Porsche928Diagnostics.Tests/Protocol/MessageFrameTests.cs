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
