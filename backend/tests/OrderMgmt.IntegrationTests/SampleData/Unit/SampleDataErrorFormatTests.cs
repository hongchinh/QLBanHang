using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderMgmt.Application.Organization.SampleData.Services;
using OrderMgmt.Domain.Common;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData.Unit;

/// SampleDataError is read by every signed-in user: user-facing reasons only (mirrors GlobalExceptionMiddleware).
public class SampleDataErrorFormatTests
{
    private const string Generic = "Lỗi hệ thống khi tạo dữ liệu mẫu. Hãy xóa chi nhánh demo và tạo lại.";

    [Fact]
    public void Validation_error_gives_its_first_message()
    {
        var ex = new ValidationException(new[]
        {
            new ValidationFailure("amount", "Số tiền phải lớn hơn 0."),
            new ValidationFailure("description", "Diễn giải quá dài."),
        });

        SampleDataRunner.FormatError("CQ-0001", ex).Should().Be("CQ-0001: Số tiền phải lớn hơn 0.");
    }

    [Fact]
    public void Domain_error_gives_its_message()
    {
        SampleDataRunner.FormatError("CATALOG", new ConflictException("Mã DM-KH01 đã được dùng cho dữ liệu thật."))
            .Should().Be("CATALOG: Mã DM-KH01 đã được dùng cho dữ liệu thật.");
        SampleDataRunner.FormatError("XBH-0001", new ValidationDomainException(
                new Dictionary<string, string[]> { ["voucherAt"] = new[] { "Ngày chứng từ trước ngày đầu kỳ." } }, null))
            .Should().Be("XBH-0001: Ngày chứng từ trước ngày đầu kỳ.");
    }

    [Fact]
    public void Policy_error_gives_its_code_message_and_first_detail()
    {
        var stock = new NegativeStockException(false, new Dictionary<string, string[]>
        {
            ["DM-SP01@RS01-K1"] = new[] { "Âm 5 Tấm tại 03/07/2026 09:00" },
        });
        SampleDataRunner.FormatError("XBH-0001", stock).Should().Be(
            "XBH-0001: NEGATIVE_STOCK_BLOCKED: Không đủ tồn kho để thực hiện thao tác. (DM-SP01@RS01-K1: Âm 5 Tấm tại 03/07/2026 09:00)");

        var fund = new ConfirmationRequiredException("NEGATIVE_FUND_BLOCKED", "Không đủ tiền trong quỹ.",
            new Dictionary<string, string[]> { ["QTM"] = new[] { "Âm 1.000 tại 05/07/2026" } }, canConfirm: false);
        SampleDataRunner.FormatError("CK-0001", fund)
            .Should().Be("CK-0001: NEGATIVE_FUND_BLOCKED: Không đủ tiền trong quỹ. (QTM: Âm 1.000 tại 05/07/2026)");

        var noDetail = new ConfirmationRequiredException("CREDIT_LIMIT_BLOCKED", "Dư nợ của khách vượt hạn mức, không thể lưu.",
            new Dictionary<string, string[]>(), canConfirm: false);
        SampleDataRunner.FormatError("XBH-0002", noDetail)
            .Should().Be("XBH-0002: CREDIT_LIMIT_BLOCKED: Dư nợ của khách vượt hạn mức, không thể lưu.");
    }

    [Fact]
    public void Concurrency_and_duplicate_errors_use_the_api_texts()
    {
        SampleDataRunner.FormatError("TKH-0001", new DbUpdateConcurrencyException("xmin changed"))
            .Should().Be("TKH-0001: Dữ liệu đã được người khác cập nhật. Vui lòng tải lại.");

        var unique = new DbUpdateException("save failed",
            new PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation));
        SampleDataRunner.FormatError("KHO-K1", unique)
            .Should().Be("KHO-K1: Dữ liệu bị trùng với bản ghi đã có. Vui lòng tải lại và thử lại.");

        var otherDbError = new DbUpdateException("save failed",
            new PostgresException("null value in column", "ERROR", "ERROR", PostgresErrorCodes.NotNullViolation));
        SampleDataRunner.FormatError("KHO-K1", otherDbError).Should().Be($"KHO-K1: {Generic}");
    }

    [Fact]
    public void Plan_error_gives_its_own_message()
    {
        SampleDataRunner.FormatError("TKH-0001", new SampleDataPlanException("Kế hoạch tham chiếu khóa không tồn tại: XBH-9999"))
            .Should().Be("TKH-0001: Kế hoạch tham chiếu khóa không tồn tại: XBH-9999");
    }

    [Fact]
    public void Generator_failure_keeps_the_generator_message()
    {
        SampleDataRunner.FormatError("KE-HOACH", SampleDataPlanException.GeneratorFailed(new InvalidOperationException("boom")))
            .Should().Be("KE-HOACH: boom");
    }

    [Fact]
    public void Any_other_error_gives_a_generic_message()
    {
        SampleDataRunner.FormatError("XBH-0001", new InvalidOperationException("Sequence contains no elements"))
            .Should().Be($"XBH-0001: {Generic}");
        SampleDataRunner.FormatError("XBH-0001", new NullReferenceException()).Should().Be($"XBH-0001: {Generic}");
    }

    [Fact]
    public void Error_is_cut_to_1000_characters()
    {
        var error = SampleDataRunner.FormatError("CQ-0001", new ConflictException(new string('x', 2000)));

        error.Should().HaveLength(1000).And.StartWith("CQ-0001: xxx");
    }

    [Fact]
    public void Cut_never_splits_a_surrogate_pair()
    {
        // "K: " (3) + 996 letters puts the emoji's high surrogate at index 999, the last kept position.
        var error = SampleDataRunner.FormatError("K", new ConflictException(new string('a', 996) + "😀 tail"));

        error.Should().HaveLength(999);
        char.IsHighSurrogate(error[^1]).Should().BeFalse();
    }
}
