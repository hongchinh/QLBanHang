using FluentAssertions;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Unit;

public class CashDocumentsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsValidVoucherAt_rejects_dates_before_2000()
    {
        CashDocuments.IsValidVoucherAt(new DateTimeOffset(1999, 12, 31, 23, 0, 0, TimeSpan.Zero), Now).Should().BeFalse();
    }

    [Fact]
    public void IsValidVoucherAt_accepts_the_first_instant_of_2000()
    {
        CashDocuments.IsValidVoucherAt(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), Now).Should().BeTrue();
    }

    [Fact]
    public void IsValidVoucherAt_accepts_up_to_one_year_ahead_and_rejects_later()
    {
        CashDocuments.IsValidVoucherAt(Now.AddYears(1), Now).Should().BeTrue();
        CashDocuments.IsValidVoucherAt(Now.AddYears(1).AddSeconds(1), Now).Should().BeFalse();
    }

    [Fact]
    public void NewActivity_copies_the_values_and_leaves_metadata_empty()
    {
        var id = Guid.NewGuid();
        var actor = Guid.NewGuid();

        var activity = CashDocuments.NewActivity(
            DocumentType.Receipt, id, CashDocumentActivityAction.Updated, actor, Now, "Cập nhật phiếu");

        activity.DocumentType.Should().Be(DocumentType.Receipt);
        activity.DocumentId.Should().Be(id);
        activity.Action.Should().Be(CashDocumentActivityAction.Updated);
        activity.ActorUserId.Should().Be(actor);
        activity.OccurredAt.Should().Be(Now);
        activity.Description.Should().Be("Cập nhật phiếu");
        activity.MetadataJson.Should().BeNull();
    }
}
