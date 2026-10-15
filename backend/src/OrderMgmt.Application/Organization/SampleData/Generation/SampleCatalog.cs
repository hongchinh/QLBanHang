using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Generation;

/// Fixed catalog of the sample dataset: products, partners, warehouses, funds and credit limits.
/// Static data only, so every branch gets the same names and prices.
/// Static initializers run in textual order: the row tables come first and the public lists derived from them after.
public static class SampleCatalog
{
    public const string PartnerSuffix = " (mẫu)";

    public const string TransportProductCode = "DM-SP30";

    // Index 0 is DM-SP01. Dimensions: Length/Width/Thickness in mm (Length = long side), Density in kg/m3.
    // Primary is the supplier of the product group; null for the untracked transport line.
    private sealed record ProductRow(string Name, string UnitCode, decimal Price, bool Track,
        decimal? Length, decimal? Width, decimal? Thickness, decimal? Density, string? Specification,
        string? Primary, string? Alt, decimal WeeklySales, bool InK1, bool InK2);

    private static readonly ProductRow[] ProductRows =
    {
        new("Tấm xốp EPS 1000×2000×20mm, tỷ trọng 10", "TAM", 28000, true, 2000, 1000, 20, 10, "1000×2000×20mm", "DM-NCC01", null, 236, true, false),
        new("Tấm xốp EPS 1000×2000×30mm, tỷ trọng 10", "TAM", 40000, true, 2000, 1000, 30, 10, "1000×2000×30mm", "DM-NCC01", null, 142, true, true),
        new("Tấm xốp EPS 1000×2000×50mm, tỷ trọng 10", "TAM", 65000, true, 2000, 1000, 50, 10, "1000×2000×50mm", "DM-NCC01", null, 236, true, false),
        new("Tấm xốp EPS 1000×2000×50mm, tỷ trọng 12", "TAM", 78000, true, 2000, 1000, 50, 12, "1000×2000×50mm", "DM-NCC01", null, 94, true, false),
        new("Tấm xốp EPS 1000×2000×100mm, tỷ trọng 12", "TAM", 150000, true, 2000, 1000, 100, 12, "1000×2000×100mm", "DM-NCC01", null, 47, true, true),
        new("Xốp EPS khối, tỷ trọng 10", "M3", 1350000, true, null, null, null, 10, "Khối, tỷ trọng 10", "DM-NCC01", null, 9, true, false),
        new("Tấm xốp XPS 600×1200×25mm", "TAM", 65000, true, 1200, 600, 25, null, "600×1200×25mm", "DM-NCC02", null, 71, true, false),
        new("Tấm xốp XPS 600×1200×30mm", "TAM", 78000, true, 1200, 600, 30, null, "600×1200×30mm", "DM-NCC02", null, 71, true, true),
        new("Tấm xốp XPS 600×1200×50mm", "TAM", 125000, true, 1200, 600, 50, null, "600×1200×50mm", "DM-NCC02", null, 47, true, false),
        new("Tấm xốp XPS 600×1200×100mm", "TAM", 245000, true, 1200, 600, 100, null, "600×1200×100mm", "DM-NCC02", null, 24, false, true),
        new("PE foam 1mm", "M2", 4500, true, null, null, 1, null, "Dày 1mm", "DM-NCC03", null, 1180, true, false),
        new("PE foam 3mm", "M2", 9000, true, null, null, 3, null, "Dày 3mm", "DM-NCC03", null, 944, true, false),
        new("PE foam 5mm", "M2", 14000, true, null, null, 5, null, "Dày 5mm", "DM-NCC03", null, 472, true, true),
        new("PE foam 10mm", "M2", 26000, true, null, null, 10, null, "Dày 10mm", "DM-NCC03", null, 236, true, false),
        new("PE foam tráng bạc 5mm", "M2", 22000, true, null, null, 5, null, "Dày 5mm, tráng bạc", "DM-NCC03", null, 236, true, false),
        new("Cao su non 1000×2000×10mm", "TAM", 120000, true, 2000, 1000, 10, null, "1000×2000×10mm", "DM-NCC04", null, 71, true, false),
        new("Cao su non 1000×2000×20mm", "TAM", 230000, true, 2000, 1000, 20, null, "1000×2000×20mm", "DM-NCC04", null, 59, true, true),
        new("Cao su non 1000×2000×30mm", "TAM", 340000, true, 2000, 1000, 30, null, "1000×2000×30mm", "DM-NCC04", null, 30, true, false),
        new("Cao su non tráng bạc 10mm", "TAM", 150000, true, null, null, 10, null, "Dày 10mm, tráng bạc", "DM-NCC04", null, 47, false, true),
        new("Thùng xốp 5kg", "THUNG", 18000, true, null, null, null, null, "Loại 5kg", "DM-NCC05", "DM-DT01", 354, true, false),
        new("Thùng xốp 10kg", "THUNG", 28000, true, null, null, null, null, "Loại 10kg", "DM-NCC05", "DM-DT01", 295, true, false),
        new("Thùng xốp 20kg", "THUNG", 45000, true, null, null, null, null, "Loại 20kg", "DM-NCC05", "DM-DT01", 177, true, true),
        new("Thùng xốp 30kg", "THUNG", 62000, true, null, null, null, null, "Loại 30kg", "DM-NCC05", "DM-DT01", 118, true, false),
        new("Bông khoáng 40kg/m³ dày 50mm", "M2", 42000, true, null, null, 50, 40, "40kg/m³, dày 50mm", "DM-NCC06", "DM-NCC08", 295, true, false),
        new("Bông khoáng 60kg/m³ dày 50mm", "M2", 55000, true, null, null, 50, 60, "60kg/m³, dày 50mm", "DM-NCC06", "DM-NCC08", 177, true, false),
        new("Bông khoáng 100kg/m³ dày 50mm", "M2", 85000, true, null, null, 50, 100, "100kg/m³, dày 50mm", "DM-NCC06", "DM-NCC08", 94, false, true),
        new("Bông thủy tinh cuộn 12kg/m³ dày 50mm", "M2", 22000, true, null, null, 50, 12, "Cuộn 12kg/m³, dày 50mm", "DM-NCC07", "DM-NCC08", 354, true, false),
        new("Bông thủy tinh cuộn 24kg/m³ dày 50mm", "M2", 32000, true, null, null, 50, 24, "Cuộn 24kg/m³, dày 50mm", "DM-NCC07", "DM-NCC08", 236, true, true),
        new("Bông thủy tinh tấm 48kg/m³ dày 50mm", "M2", 75000, true, null, null, 50, 48, "Tấm 48kg/m³, dày 50mm", "DM-NCC07", "DM-NCC08", 118, true, false),
        new("Vận chuyển nội thành", "CHUYEN", 350000, false, null, null, null, null, null, null, null, 0, false, false),
    };

    public static IReadOnlyList<SampleProductSpec> Products { get; } = ProductRows
        .Select((r, i) => new SampleProductSpec(ProductCode(i + 1), r.Name, r.UnitCode, r.Price, CostPrice(r.Price),
            r.Track, r.Length, r.Width, r.Thickness, r.Density, r.Specification))
        .ToList();

    /// Plan of each tracked product (29, in product order); the untracked transport line has none.
    public static IReadOnlyList<SampleProductPlan> ProductPlans { get; } = ProductRows
        .Select((r, i) => (Code: ProductCode(i + 1), Row: r))
        .Where(x => x.Row.Track)
        .Select(x => new SampleProductPlan(x.Code, x.Row.Primary!, x.Row.Alt, x.Row.WeeklySales, x.Row.InK1, x.Row.InK2))
        .ToList();

    public static IReadOnlyList<SampleWarehouseSpec> Warehouses { get; } = new[]
    {
        new SampleWarehouseSpec("K1", "Kho chính"),
        new SampleWarehouseSpec("K2", "Kho phụ"),
    };

    public static IReadOnlyList<SampleFundSpec> Funds { get; } = new[]
    {
        new SampleFundSpec("QTM", "Quỹ tiền mặt", FundKind.Cash, null, null, 30_000_000m),
        new SampleFundSpec("VCB", "TK Vietcombank (mẫu)", FundKind.Bank, "Vietcombank", "9999000001", 450_000_000m),
        new SampleFundSpec("TCB", "TK Techcombank (mẫu)", FundKind.Bank, "Techcombank", "9999000002", 150_000_000m),
    };

    private const decimal AgentCreditLimit = 300_000_000m;

    private sealed record PartnerRow(string Code, string Name, CustomerGroup Group, int CreditDays, string? TaxCode,
        string Address, string Phone, string Contact,
        PaymentHabit Habit = PaymentHabit.OnTime, SalesTier Tier = SalesTier.Medium, decimal? CreditLimit = null,
        bool HasPrepayment = false);

    // Large: KH01, KH02, KH03, KH08, KH10 (owner indexes 0, 1, 2, 1, 0 with 3 owners). Small: the 5 retail customers.
    // Late payers: KH06 (Company) and KH11 (Agent, no limit). Bad debt: KH20. Prepayment: KH12. Limits: KH08, KH10.
    private static readonly PartnerRow[] CustomerRows =
    {
        new("DM-KH01", "Công ty TNHH Xây dựng Phước Lộc", CustomerGroup.Company, 30, "0312000101", "12 Nguyễn Văn Linh, Quận 7, TP.HCM", "0901000101", "Nguyễn Văn An", Tier: SalesTier.Large),
        new("DM-KH02", "Công ty CP Cơ khí Thiên Bảo", CustomerGroup.Company, 30, "0312000102", "45 Lê Văn Việt, TP. Thủ Đức, TP.HCM", "0901000102", "Trần Thiên Bảo", Tier: SalesTier.Large),
        new("DM-KH03", "Công ty TNHH Nội thất Gia Huy", CustomerGroup.Company, 30, "0312000103", "88 Quang Trung, Gò Vấp, TP.HCM", "0901000103", "Lê Gia Huy", Tier: SalesTier.Large),
        new("DM-KH04", "Công ty CP Thực phẩm Bình An", CustomerGroup.Company, 30, "0312000104", "21 Võ Văn Ngân, TP. Thủ Đức, TP.HCM", "0901000104", "Phạm Bình An"),
        new("DM-KH05", "Công ty TNHH Điện lạnh Thành Công", CustomerGroup.Company, 30, "0312000105", "300 Cộng Hòa, Tân Bình, TP.HCM", "0901000105", "Võ Thành Công"),
        new("DM-KH06", "Công ty TNHH Bao bì Tân Phú", CustomerGroup.Company, 30, "0312000106", "17 Lũy Bán Bích, Tân Phú, TP.HCM", "0901000106", "Đặng Thị Hoa", Habit: PaymentHabit.Late),
        new("DM-KH07", "Đại lý vật liệu Hưng Thịnh", CustomerGroup.Agent, 15, "0312000107", "5 Quốc lộ 1A, Bình Chánh, TP.HCM", "0902000107", "Hoàng Hưng"),
        new("DM-KH08", "Đại lý vật tư Phú Quý", CustomerGroup.Agent, 30, "0312000108", "112 Nguyễn Ảnh Thủ, Quận 12, TP.HCM", "0902000108", "Lý Phú Quý", Tier: SalesTier.Large, CreditLimit: AgentCreditLimit),
        new("DM-KH09", "Đại lý xây dựng Kim Ngân", CustomerGroup.Agent, 30, "0312000109", "63 Đại lộ Bình Dương, Thuận An, Bình Dương", "0902000109", "Bùi Kim Ngân"),
        new("DM-KH10", "Đại lý cách nhiệt Đông Nam", CustomerGroup.Agent, 45, "0312000110", "9 Phạm Văn Thuận, Biên Hòa, Đồng Nai", "0902000110", "Ngô Đông Nam", Tier: SalesTier.Large, CreditLimit: AgentCreditLimit),
        new("DM-KH11", "Đại lý vật liệu Tây Đô", CustomerGroup.Agent, 45, "0312000111", "74 Nguyễn Văn Cừ, Ninh Kiều, Cần Thơ", "0902000111", "Trương Tây Đô", Habit: PaymentHabit.Late),
        new("DM-KH12", "Nguyễn Thị Mai", CustomerGroup.Retail, 0, null, "10 Hoàng Hoa Thám, Bình Thạnh, TP.HCM", "0903000112", "Nguyễn Thị Mai", Tier: SalesTier.Small, HasPrepayment: true),
        new("DM-KH13", "Trần Quốc Bảo", CustomerGroup.Retail, 0, null, "36 Phan Văn Trị, Gò Vấp, TP.HCM", "0903000113", "Trần Quốc Bảo", Tier: SalesTier.Small),
        new("DM-KH14", "Lê Hồng Nhung", CustomerGroup.Retail, 0, null, "52 Lê Đức Thọ, Gò Vấp, TP.HCM", "0903000114", "Lê Hồng Nhung", Tier: SalesTier.Small),
        new("DM-KH15", "Phạm Văn Tài", CustomerGroup.Retail, 0, null, "8 Tô Ký, Quận 12, TP.HCM", "0903000115", "Phạm Văn Tài", Tier: SalesTier.Small),
        new("DM-KH16", "Huỳnh Thanh Tâm", CustomerGroup.Retail, 0, null, "27 Tân Kỳ Tân Quý, Tân Phú, TP.HCM", "0903000116", "Huỳnh Thanh Tâm", Tier: SalesTier.Small),
        new("DM-KH17", "Ban QLDA Công trình Sông Xanh", CustomerGroup.Project, 45, "0312000117", "1 Đại lộ Võ Văn Kiệt, Quận 1, TP.HCM", "0904000117", "Đỗ Văn Khoa"),
        new("DM-KH18", "Công ty CP Đầu tư Khu đô thị Vạn Lộc", CustomerGroup.Project, 45, "0312000118", "230 Nguyễn Thị Minh Khai, Quận 3, TP.HCM", "0904000118", "Vũ Vạn Lộc"),
        new("DM-KH19", "Ban QLDA Nhà xưởng Long Hậu", CustomerGroup.Project, 45, "0312000119", "KCN Long Hậu, Cần Giuộc, Long An", "0904000119", "Mai Văn Hậu"),
        new("DM-KH20", "Công ty TNHH Xây lắp Phúc Thịnh", CustomerGroup.Project, 45, "0312000120", "66 Nguyễn Oanh, Gò Vấp, TP.HCM", "0904000120", "Lâm Phúc Thịnh", Habit: PaymentHabit.BadDebt),
    };

    private static readonly PartnerRow[] SupplierRows =
    {
        new("DM-NCC01", "Công ty TNHH Xốp EPS Việt Phát", CustomerGroup.Company, 30, "0313000201", "KCN Tân Bình, Tân Phú, TP.HCM", "0281000201", "Nguyễn Việt Phát"),
        new("DM-NCC02", "Công ty CP Xốp XPS Á Châu", CustomerGroup.Company, 30, "0313000202", "KCN Vĩnh Lộc, Bình Chánh, TP.HCM", "0281000202", "Trần Á Châu"),
        new("DM-NCC03", "Công ty TNHH PE Foam Sài Gòn", CustomerGroup.Company, 15, "0313000203", "KCN Tân Tạo, Bình Tân, TP.HCM", "0281000203", "Lê Sài Gòn"),
        new("DM-NCC04", "Công ty TNHH Cao su non Đại Dương", CustomerGroup.Company, 30, "0313000204", "KCN Sóng Thần, Dĩ An, Bình Dương", "0274000204", "Phạm Đại Dương"),
        new("DM-NCC05", "Công ty TNHH Thùng xốp Bảo Châu", CustomerGroup.Company, 30, "0313000205", "KCN Hiệp Phước, Nhà Bè, TP.HCM", "0281000205", "Võ Bảo Châu"),
        new("DM-NCC06", "Công ty CP Bông khoáng Đông Á", CustomerGroup.Company, 45, "0313000206", "KCN Long Thành, Đồng Nai", "0251000206", "Đặng Đông Á"),
        new("DM-NCC07", "Công ty TNHH Bông thủy tinh Nam Việt", CustomerGroup.Company, 30, "0313000207", "KCN Mỹ Phước, Bến Cát, Bình Dương", "0274000207", "Hoàng Nam Việt"),
        new("DM-NCC08", "Công ty TNHH Vật tư cách nhiệt An Khang", CustomerGroup.Company, 30, "0313000208", "KCN Nhơn Trạch, Đồng Nai", "0251000208", "Bùi An Khang"),
    };

    private static readonly PartnerRow[] DualRoleRows =
    {
        new("DM-DT01", "Công ty TNHH Gia công Bao bì Thiên Phú", CustomerGroup.Company, 30, "0314000301", "28 Trường Chinh, Tân Bình, TP.HCM", "0281000301", "Ngô Thiên Phú"),
        new("DM-DT02", "Cơ sở Tái chế Xốp Hải Đăng", CustomerGroup.Company, 30, "0314000302", "15 Phạm Hùng, Bình Chánh, TP.HCM", "0281000302", "Lý Hải Đăng"),
    };

    /// Profiles of the 20 customers then the 2 dual-role partners (these buy as well).
    public static IReadOnlyList<SampleCustomerProfile> CustomerProfiles { get; } = CustomerRows.Concat(DualRoleRows)
        .Select(r => new SampleCustomerProfile(r.Code, r.Group, r.Habit, r.Tier, r.CreditLimit, r.HasPrepayment))
        .ToList();

    /// Credit limits derived from the customer profiles.
    public static IReadOnlyList<SampleCreditLimitSpec> CreditLimits { get; } = CustomerProfiles
        .Where(p => p.CreditLimit != null)
        .Select(p => new SampleCreditLimitSpec(p.Code, p.CreditLimit!.Value))
        .ToList();

    /// The customer that gets the credit sale placed relative to T for a late payer or the bad debt customer: the first
    /// one with that habit and no credit limit (a sale over 80% of a limit would be paid at once instead).
    public static SampleCustomerProfile PlacedSaleCustomer(PaymentHabit habit) =>
        CustomerProfiles.First(p => p.Habit == habit && p.CreditLimit == null);

    /// All 30 partners: customers, then suppliers, then the two dual-role partners.
    /// Customers and dual-role partners get OwnerIndex = position % ownerCount (customers first, then dual-role).
    public static List<SamplePartnerSpec> BuildPartners(int ownerCount)
    {
        if (ownerCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ownerCount), ownerCount, "OwnerCount must be at least 1.");
        }

        var partners = new List<SamplePartnerSpec>();
        var position = 0;
        foreach (var r in CustomerRows)
        {
            partners.Add(ToSpec(r, true, false, position++ % ownerCount));
        }
        foreach (var r in SupplierRows)
        {
            partners.Add(ToSpec(r, false, true, null));
        }
        foreach (var r in DualRoleRows)
        {
            partners.Add(ToSpec(r, true, true, position++ % ownerCount));
        }
        return partners;
    }

    private static SamplePartnerSpec ToSpec(PartnerRow r, bool isCustomer, bool isSupplier, int? ownerIndex) =>
        new(r.Code, r.Name + PartnerSuffix, r.Group, isCustomer, isSupplier, r.CreditDays, r.TaxCode, r.Address, r.Phone,
            r.Contact, ownerIndex);

    private static string ProductCode(int number) => $"DM-SP{number:00}";

    private static decimal CostPrice(decimal price) =>
        Math.Round(price * 0.78m / 100m, MidpointRounding.AwayFromZero) * 100m;
}
