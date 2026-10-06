namespace OrderMgmt.Application.Inventory.PaymentMethods.Models;

public class PaymentMethodDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsCash { get; set; }
}

public class CreatePaymentMethodRequest
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsCash { get; set; }
}

public class UpdatePaymentMethodRequest
{
    public string Name { get; set; } = default!;
    public bool IsCash { get; set; }
}
