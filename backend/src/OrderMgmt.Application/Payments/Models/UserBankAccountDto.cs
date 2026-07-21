namespace OrderMgmt.Application.Payments.Models;

public class UserBankAccountDto
{
    public Guid Id { get; set; }
    public Guid BankId { get; set; }
    public string BankCode { get; set; } = default!;
    public string BankName { get; set; } = default!;
    public string BankBin { get; set; } = default!;
    public string AccountNumber { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    public bool IsDefault { get; set; }
}

public class CreateUserBankAccountRequest
{
    public Guid BankId { get; set; }
    public string AccountNumber { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    public bool IsDefault { get; set; }
}

public class UpdateUserBankAccountRequest
{
    public Guid BankId { get; set; }
    public string AccountNumber { get; set; } = default!;
    public string AccountName { get; set; } = default!;
}
