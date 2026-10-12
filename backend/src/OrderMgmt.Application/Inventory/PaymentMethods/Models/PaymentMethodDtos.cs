using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.PaymentMethods.Models;

public class PaymentMethodDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public FundKind FundKind { get; set; }
}

public class CreatePaymentMethodRequest
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public FundKind FundKind { get; set; }
}

public class UpdatePaymentMethodRequest
{
    public string Name { get; set; } = default!;
    public FundKind FundKind { get; set; }
}
