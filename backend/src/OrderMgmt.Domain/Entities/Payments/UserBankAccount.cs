using OrderMgmt.Domain.Common;

namespace OrderMgmt.Domain.Entities.Payments;

public class UserBankAccount : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid BankId { get; set; }
    public string AccountNumber { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    public bool IsDefault { get; set; }

    public Bank Bank { get; set; } = default!;
}
