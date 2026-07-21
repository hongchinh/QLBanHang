namespace OrderMgmt.Application.Payments.Models;

public class GenerateQrRequest
{
    public Guid BankId { get; set; }
    public string AccountNumber { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    public decimal Amount { get; set; }
    public string? Content { get; set; }
}

public class GenerateQrResponse
{
    public string Payload { get; set; } = default!;
}
