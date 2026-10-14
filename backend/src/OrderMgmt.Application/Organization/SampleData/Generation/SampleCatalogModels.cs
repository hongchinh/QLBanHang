using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Generation;

/// How a customer pays its invoices.
public enum PaymentHabit
{
    OnTime = 0,
    Late = 1,
    BadDebt = 2,
}

/// Share of the sales a customer takes: 5 large (about 40%), 10 medium, 5 small.
public enum SalesTier
{
    Large = 0,
    Medium = 1,
    Small = 2,
}

/// Planned trading of one tracked product. WeeklySales is in the product's unit; AltSupplier may be a dual-role partner.
public sealed record SampleProductPlan(string ProductCode, string PrimarySupplier, string? AltSupplier, decimal WeeklySales,
    bool InK1, bool InK2);

/// Static role of a customer (or dual-role partner) in the generated history.
public sealed record SampleCustomerProfile(string Code, CustomerGroup Group, PaymentHabit Habit, SalesTier Tier,
    decimal? CreditLimit, bool HasPrepayment);
