namespace OrderMgmt.Application.Payments.Models;

public class BankDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? ShortName { get; set; }
    public string Bin { get; set; } = default!;
}
