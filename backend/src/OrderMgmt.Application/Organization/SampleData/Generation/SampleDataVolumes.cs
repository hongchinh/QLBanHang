namespace OrderMgmt.Application.Organization.SampleData.Generation;

/// Volume knobs of the sample dataset. Monthly figures are for a full month; a partial month gets the part that falls
/// inside the window. Percentages are whole percents.
public static class SampleDataVolumes
{
    /// Opening stock in K1: weeks of planned sales.
    public const int OpeningWeeksK1Min = 3;
    public const int OpeningWeeksK1Max = 6;

    /// Opening stock in K2 of a product that is also in K1: a smaller amount.
    public const int OpeningWeeksK2SharedMin = 1;
    public const int OpeningWeeksK2SharedMax = 3;

    /// Opening stock in K2 of a K2-only product: the full range, as it can be sold from K2 only until bought.
    public const int OpeningWeeksK2OnlyMin = 3;
    public const int OpeningWeeksK2OnlyMax = 6;

    // ---- Documents: day and time ----

    /// Document times, in minutes after VN midnight: 08:00 to 17:30.
    public const int FirstMinute = 8 * 60;
    public const int LastMinute = 17 * 60 + 30;

    // ---- Sales (XBH) ----

    /// Sales in a full month (spec 35–45): one per working day, the rest on random working days.
    public const int SalesPerMonthMin = 37;
    public const int SalesPerMonthMax = 43;

    /// Goods lines of a sale; a transport line may come on top.
    public const int SaleLinesMin = 1;
    public const int SaleLinesMax = 6;

    /// Share of the sales with one DM-SP30 transport line (quantity 1).
    public const int TransportLinePercent = 20;

    /// Share of the sales shipped from K1; the rest from K2.
    public const int K1SalePercent = 80;

    /// Planned demand in percent of the catalog's weekly sales. Above 100 because K2 holds only part of the products:
    /// K1 then sells 80% × 125% = 100% of a product's weekly sales.
    public const int DemandPercent = 125;

    /// Customer tiers: share of the sales (count) and line quantity in percent of the average one.
    /// Large 30% × 140 = 42% of the volume, medium 50% × 96 = 48%, small 20% × 50 = 10%.
    public const int LargeTierPercent = 30;
    public const int SmallTierPercent = 20;
    public const int LargeQuantityPercent = 140;
    public const int MediumQuantityPercent = 96;
    public const int SmallQuantityPercent = 50;

    /// A line quantity varies by up to this much around its planned amount.
    public const int SaleQuantitySpreadPercent = 50;

    /// Share of the non-retail sales paid at once; of those, the share paid by bank transfer (the rest in cash).
    public const int PaidSalePercent = 30;
    public const int PaidSaleBankPercent = 50;

    /// A credit sale to a customer with a limit keeps its open receivables at or below this share of the limit.
    public const int CreditLimitUsePercent = 80;

    // ---- Purchases (NMH) ----

    /// A product is reordered when its stock in a warehouse falls under this many weeks of its demand there (about 1.5
    /// weeks of sales in K1, so the first month, which opens with 3–6 weeks of stock, already has 8 or more purchases).
    /// A purchase restocks the products of that product group below the warehouse's minimum level up to a level drawn in
    /// its range, adding products of the group the warehouse never had when that is needed for PurchaseLinesMin lines.
    /// K2 sells little per product but in large lines, so it keeps more weeks.
    public const decimal ReorderWeeks = 1.5m;
    public const decimal RestockWeeksK1Min = 3.5m;
    public const decimal RestockWeeksK1Max = 5.0m;
    public const decimal RestockWeeksK2Min = 6.0m;
    public const decimal RestockWeeksK2Max = 8.0m;

    public const int PurchaseLinesMin = 3;
    public const int PurchaseLinesMax = 8;
    public const int PurchasesPerDayMax = 2;

    /// Purchasing pace: the buyer places about PurchasesPerMonth orders a month, at most PurchaseBurst in a row (a token
    /// bucket refilled every working day). Due reorders wait, the most urgent (lowest cover) first. The burst also keeps
    /// the pace of the first, quiet weeks (opening stock lasts 3–6 weeks), so the first month still reaches 8 purchases.
    public const int PurchasesPerMonth = 11;
    public const int PurchaseBurst = 5;

    /// Share of the purchases placed with the product's alternate supplier, when it has one.
    public const int AltSupplierPercent = 20;

    /// Share of the purchases paid at once by bank transfer (when a bank fund keeps the margin).
    public const int PaidPurchasePercent = 50;

    /// Purchase price: cost price ± this many percent, rounded to 100.
    public const int PurchasePriceSpreadPercent = 5;

    // ---- Returns and damaged goods ----

    /// Sales returns (NTL) per month, of a sale of the last SaleReturnLookbackDays days; the returned quantity is
    /// ReturnPercentMin..Max of the sold one.
    public const int SaleReturnsPerMonthMin = 1;
    public const int SaleReturnsPerMonthMax = 2;
    public const int SaleReturnLookbackDays = 30;
    public const int ReturnLinesMax = 2;
    public const int ReturnPercentMin = 10;
    public const int ReturnPercentMax = 30;

    /// Purchase returns (XTL) per month, of the latest purchase.
    public const int PurchaseReturnsPerMonth = 1;

    /// Damaged goods (XKH) per month: 1–2 lines of DamagedPercentMax percent of a week of sales at most.
    public const int DamagedPerMonth = 1;
    public const int DamagedLinesMax = 2;
    public const int DamagedPercentMax = 3;

    /// First working day (0-based, in the month) a return may fall on, so there is something to return.
    public const int SaleReturnFirstDayIndex = 4;
    public const int PurchaseReturnFirstDayIndex = 12;

    // ---- Customer receipts (TKH) ----

    /// On-time customers pay 0–5 days before the due date; late payers (and new sales of the bad debt customer)
    /// 30–60 days after it.
    public const int OnTimeDaysBeforeDueMin = 0;
    public const int OnTimeDaysBeforeDueMax = 5;
    public const int LateDaysAfterDueMin = 30;
    public const int LateDaysAfterDueMax = 60;

    /// A payment date that falls before the opening date is moved to one of the first CatchUpDays days of the window.
    public const int CatchUpDays = 6;

    /// A customer who pays also settles its invoices due within this many days, so receipts often gather several documents.
    public const int ReceiptGatherDays = 7;

    /// Share of the receipts into a bank fund; the rest into cash.
    public const int ReceiptBankPercent = 60;

    /// On the deposit day, while the till holds less than CashKeep + CashDepositMin + CashFloat, that day's sales (but the
    /// dual-role partner's planned credit sale) and receipts are taken in cash, and customers with an invoice to collect
    /// within EarlyCollectionDays pay it early, so every week ends with a deposit.
    public const int EarlyCollectionDays = 14;

    /// Deliberate partial payments: on or after these days of the window, the next receipt of an on-time customer pays
    /// PartialPaymentPercent of its first target (rounded down to PartialPaymentStep, at least PartialPaymentMinOpen open);
    /// the rest is due again PartialRestDaysMin..Max days later.
    public const int PartialPaymentFirstDay = 30;
    public const int PartialPaymentSecondDay = 60;
    public const int PartialPaymentPercent = 50;
    public const decimal PartialPaymentStep = 1_000_000m;
    public const decimal PartialPaymentMinOpen = 10_000_000m;
    public const int PartialRestDaysMin = 20;
    public const int PartialRestDaysMax = 40;

    // ---- Supplier payments (TNCC) ----

    /// Suppliers are paid 0–3 days before the due date, postponed day by day while no bank fund keeps the margin.
    public const int SupplierDaysBeforeDueMin = 0;
    public const int SupplierDaysBeforeDueMax = 3;

    // ---- Funds ----

    /// Every estimated fund balance stays at or above this amount (S12).
    public const decimal FundMargin = 5_000_000m;

    /// Payments the business may postpone (paid purchases, supplier payments, the VCB → TCB transfer) also leave this much
    /// in VCB, which pays the rent and the salaries.
    public const decimal VcbReserve = 80_000_000m;

    /// Weekly deposit QTM → VCB of the cash above CashKeep, when that is at least CashDepositMin; rounded down to
    /// CashAmountStep.
    public const decimal CashKeep = 30_000_000m;
    public const decimal CashDepositMin = 10_000_000m;

    /// Margin above the deposit level the till aims for on the deposit day.
    public const decimal CashFloat = 10_000_000m;

    /// Monthly VCB → TCB transfer, on a working day from this index of the month (retried on the next days).
    public const decimal BankTransferAmount = 50_000_000m;
    public const int BankTransferDayIndexMin = 8;
    public const int BankTransferDayIndexMax = 18;

    // ---- Expenses (CK) and other income (TK) ----

    public const decimal Rent = 15_000_000m;
    public const decimal Salary = 60_000_000m;
    public const decimal ElectricityMin = 3_000_000m;
    public const decimal ElectricityMax = 5_000_000m;
    public const int FuelPerMonthMin = 2;
    public const int FuelPerMonthMax = 4;
    public const decimal FuelMin = 300_000m;
    public const decimal FuelMax = 1_500_000m;
    public const decimal ScrapMin = 1_000_000m;
    public const decimal ScrapMax = 3_000_000m;

    /// Steps the cash amounts are drawn or rounded on: deposits, electricity, scrap and the mistaken receipt on 100,000;
    /// fuel on 50,000.
    public const decimal CashAmountStep = 100_000m;
    public const decimal FuelAmountStep = 50_000m;

    // ---- Once in the whole period (days after the opening date; moved to the next working day) ----

    /// Refund (CHKH) of part of the retail prepayment.
    public const decimal PrepaymentRefund = 5_000_000m;
    public const int RefundDayMin = 35;
    public const int RefundDayMax = 60;

    /// A receipt (TK) entered by mistake and cancelled later the same day.
    public const decimal MistakenIncomeMin = 1_000_000m;
    public const decimal MistakenIncomeMax = 3_000_000m;
    public const int MistakenIncomeDayMin = 15;
    public const int MistakenIncomeDayMax = 45;

    /// The debt offset of the dual-role partner, on the first working day from this day where it has an open credit
    /// sale and an open credit purchase. From OffsetSaleLeadDays days before, its next sale is planned on credit.
    public const int OffsetDayMin = 30;
    public const int OffsetDayMax = 50;
    public const int OffsetSaleLeadDays = 10;
}
