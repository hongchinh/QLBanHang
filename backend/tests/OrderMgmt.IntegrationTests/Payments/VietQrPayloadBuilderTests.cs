using FluentAssertions;
using OrderMgmt.Application.Payments.Services;
using Xunit;

namespace OrderMgmt.IntegrationTests.Payments;

public class VietQrPayloadBuilderTests
{
    [Fact]
    public void Crc16_matches_known_test_vector()
    {
        var crc = VietQrPayloadBuilder.ComputeCrc16("123456789");
        crc.Should().Be("29B1");
    }

    [Fact]
    public void Build_includes_required_tlv_fields_in_order()
    {
        var payload = VietQrPayloadBuilder.Build(
            bankBin: "970436",
            accountNumber: "0123456789",
            accountName: "Nguyễn Văn A",
            amount: 150000m,
            content: "TT don hang DH-0001");

        payload.Should().StartWith("000201"); // tag 00, len 02, "01"
        payload.Should().Contain("010212"); // tag 01, len 02, "12"
        payload.Should().Contain("0006970436"); // bank BIN nested under tag 38/01/00
        payload.Should().Contain("0123456789"); // account number present verbatim
        payload.Should().Contain("5303704"); // currency VND
        payload.Should().Contain("5406150000"); // amount, tag 54 len 06 value 150000
        payload.Should().Contain("5802VN"); // country code
        payload.Should().Contain("NGUYEN VAN A"); // normalized merchant name
        payload.Should().Contain("6304"); // CRC tag+len marker
    }

    [Fact]
    public void Build_crc_is_last_four_chars_and_self_consistent()
    {
        var payload = VietQrPayloadBuilder.Build(
            bankBin: "970407",
            accountNumber: "999888777",
            accountName: "Tran Thi B",
            amount: 50000m,
            content: null);

        var withoutCrc = payload[..^4];
        var expectedCrc = VietQrPayloadBuilder.ComputeCrc16(withoutCrc);
        payload[^4..].Should().Be(expectedCrc);
        payload[^4..].Should().MatchRegex("^[0-9A-F]{4}$");
    }

    [Fact]
    public void Build_omits_tag_62_when_content_is_empty()
    {
        var payload = VietQrPayloadBuilder.Build(
            bankBin: "970436",
            accountNumber: "0123456789",
            accountName: "Test User",
            amount: 1000m,
            content: null);

        payload.Should().NotContain("6208"); // len byte "08" only appears if tag 62/08 present
    }
}
