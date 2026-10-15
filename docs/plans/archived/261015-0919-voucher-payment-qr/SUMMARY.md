# VietQR transfer QR on receipts and stock-out vouchers (QR chuyển khoản trên phiếu thu và phiếu xuất kho)

> Created: 2026-10-15 09:19 · Based on `qlbanhang/main` at `3ae3804` (PR #19 branch sample data merged; PR #18 app icon registry included) · Source design: [brainstorm SUMMARY](../../../brainstorms/261015-0656-voucher-payment-qr/SUMMARY.md) (design approved, reviewed against code 2026-10-15, R1–R14; mockups alongside) · Builds on: [TT133 voucher print forms](../261014-1632-tt133-voucher-print-forms/SUMMARY.md) (PR #15)

## Goal

Let a customer pay a receipt (01-TT) or a stock-out voucher (02-VT) by scanning a VietQR code:

- The **printed** voucher (Excel and PDF, single and bulk) carries the QR when the person printing leaves "Kèm QR chuyển khoản" ticked (remembered per user, default on).
- The **voucher screen** has a "QR chuyển khoản" button that opens a dialog (a bottom sheet on a phone) with the QR, the amount, the account and the transfer content, with copy and "Tải ảnh QR".
- Money goes to a **bank fund** of the voucher's branch: the voucher's own fund when it is a bank fund, else the branch's default bank fund. Admin enters the bank and the account holder on the fund screen.
- The backend owns the rules and the payload (`VietQrPayloadBuilder`), so the paper and the screen always carry the same payload.

## Scope

**In scope**

- Backend:
  - `Fund.BankId` and `Fund.AccountHolderName` (one migration, DTOs, validator, `FundService`);
  - `Application/CashDebt/PaymentQr/`: `VoucherPaymentQrRules` (pure), `IVoucherPaymentQrService` / `VoucherPaymentQrService`, `VoucherPaymentQrDto`;
  - `GET api/cash-vouchers/{id}/payment-qr` and `GET api/stock-vouchers/{id}/payment-qr` (422 `PAYMENT_QR_UNAVAILABLE`);
  - `includeQr` on the four print routes, `QrPngRenderer` (QRCoder), QR on 01-TT (beside the lines) and 02-VT (a payment block).
- Frontend:
  - bank and account holder on the fund form;
  - `qr-pref-storage`, the "Kèm QR chuyển khoản" menu item (receipts) and checkbox (stock-out price dialog), `includeQr` in `useVoucherPrint`;
  - `components/payment-qr/` (`PaymentQrButton`, `PaymentQrDialog`) and `features/voucher-payment-qr/`, wired into the receipt and stock-out pages.
- Docs: `docs/SUMMARY.md`, `conventions.md`, `system-architecture.md`, `product-goals.md`, `directory-structure.md`, a go-live checklist.

**Out of scope** (brainstorm non-goals)

- Payment confirmation, matching bank transactions to vouchers, webhooks (SePay is mobile hub round 5).
- QR on payments (02-TT), stock-in (01-VT), the voucher list screens (only the bulk-print checkbox).
- Mapping the old free-text `BankName` to `Banks`; choosing the receiving account at print time; the employee's personal account; VietQR/NAPAS logo; QR size settings.
- Cash voucher form redesign ([brainstorm 261015-0644](../../../brainstorms/261015-0644-cash-voucher-form-redesign/SUMMARY.md)): this plan only adds one button to the form page's button row; whichever plan merges second rebases.

## Decisions made while planning (in addition to the brainstorm and its review R1–R14)

| # | Topic | Decision |
|---|---|---|
| Q1 | Branch and workspace | Branch `feat/voucher-payment-qr` from `qlbanhang/main`, in the worktree `.claude/worktrees/voucher-qr`. Backend tests use their own database base name `qldonhang_integtest_vqr`. Phase 04 (frontend only) may run in a second worktree `.claude/worktrees/voucher-qr-fe` on `feat/voucher-payment-qr-fe`, created from the Phase 02 commit by Phase 04 Task 4.0 and merged back by Phase 05 Task 5.0. Two agents never commit in the same worktree. |
| Q2 | Base has the icon registry | `qlbanhang/main` already contains the `AppIcon` registry (PR #18): new frontend code never imports `lucide-react` outside `components/icons`; it renders `<AppIcon name="…" />`. `paymentQr` exists; this plan adds `copy`, `download` and `share` (with a line in `icon-registry.test.ts`). Colours come from the name's tone; icons on a filled button pass `tone="none"`. |
| Q3 | Fund fields | `BankId` and `AccountHolderName` are stored only for `Kind = Bank`; a create/update with another kind stores both as `null` (the old `BankName`/`AccountNumber` keep their present behaviour). `BankShortName` in `FundDto` = `Bank.ShortName ?? Bank.Code` (null without a bank). Validation: `BankId` must name an active bank (400 `bankId`, service); with a `BankId`, `AccountNumber` must match `^[0-9A-Za-z]{1,19}$` (400 `accountNumber`, validator; a bank fund without `BankId` keeps the old 50-character free text); `AccountHolderName` ≤ 255 (400 `accountHolderName`). |
| Q4 | Print models carry the DTO | The brainstorm's `VoucherQrPrint` class is dropped: `CashVoucherPrintModel.Qr` and `StockVoucherPrintModel.Qr` are `VoucherPaymentQrDto?`, and the printed lines are made by pure `VoucherPrintRules` helpers (`QrAmountLine`, `QrBreakdownLine`, `QrBankCaption`) so they are unit-tested without a workbook. `null` means no QR. |
| Q5 | Service signatures | `IVoucherPrintService.PrintCashVouchersAsync(ids, format, bool includeQr = false, ct)` and `PrintStockVouchersAsync(ids, priceMode, format, bool includeQr = false, ct)`: `includeQr` sits before `ct`, so call sites that passed `ct` positionally are updated in the same task. `IncludeQr` on both request classes defaults to `false`. The stock model gets its `Qr` only when the effective price mode is `Voucher`. |
| Q6 | One resolver for screen and paper | `VoucherPaymentQrService` has one private resolver that returns, per voucher id, either the DTO or the `PaymentQrBlock` reason. `GetFor*Async` throws the 422 from the reason; `ResolveManyAsync` keeps only the DTOs (an absent key = no QR) and never throws. `ResolveManyAsync` does not check permissions or branch (the print service has just done that, per id) and runs in the caller's snapshot. |
| Q7 | QR size on paper | The QR prints about 25 mm wide. The sheet is shrunk to one page wide (A4) or one page (A5), so the picture is drawn `25 mm / scale` wide in the sheet: A4 reuses the scale `VoucherFormBuilder` already computes for the page break, A5 landscape uses the constant `A5LandscapeScale` (start 0.90) that Task 3.6 replaces by the measured value. Picture pixels = sheet points / 0.75 (96 dpi); the PNG is rendered at 8 pixels per module and shown smaller. The shared LibreOffice converter uses its default PDF options (`--convert-to pdf`), which may downsample the picture to 300 dpi and compress it with JPEG; Task 3.6 inspects the image embedded in the PDF and scans it before the layout is accepted. |
| Q8 | Packages | `QRCoder` (Infrastructure). Tests: `ZXing.Net` and `ZXing.Net.Bindings.SkiaSharp`. If the bindings package conflicts with `SkiaSharp 3.119.4`, use `ZXing.Net` alone and feed `RGBLuminanceSource` from a `SKBitmap` the test decodes itself; the decode-back test stays. Each package is added at the newest stable version `dotnet add package` offers; record the versions in the commit message. |
| Q9 | Query keys and cache | `payment-qr` queries live under `cashKeys.all` (`['cash', 'payment-qr', kind, id]`), so every cash and stock voucher mutation invalidates them; the hook uses `staleTime: 0` and `gcTime: 0` so each opening asks the server. The two routes are never added to `CACHEABLE_API_PREFIXES`; a test pins that. |
| Q10 | Test data | Banks come from the seeded `Banks` table (`GET api/banks`; take the first). A QR-ready fund is made with `CreateBankFundAsync` of `InventoryTestBase` extended with optional `bankId`, `accountHolderName`, `accountNumber` (default account `0123456789`); a fund missing one field is made by passing `null`. Stock-out test vouchers use the reason `XBH`; a non-sale reason is any system reason whose `DebtEffect` is not `ReceivableIncrease`. |
| Q11 | Docs that conflict with the main checkout | The main checkout holds uncommitted edits of `docs/SUMMARY.md` (it already lists the brainstorm). Phase 00 copies the brainstorm folder (with the mockups) and the plan into the worktree and adds the rows there; the report tells the user to `git checkout -- docs/SUMMARY.md` in the main checkout (or keep the identical rows) before merging. |
| Q12 | Automatic receipts in a cash fund | Decided by the user after the plan review: an automatic receipt (`SourceStockVoucherId != null`) gets a QR only when its own fund is a **bank** fund. One in a cash fund records money the customer already handed over, so a QR would ask for it again; it answers 422 `PAYMENT_QR_UNAVAILABLE` with `Phiếu thu tự động thu tiền mặt, không có QR chuyển khoản.` (block `AutoReceiptInCash`), and prints without a QR. A manual receipt in a cash fund still falls back to the branch's default bank fund (brainstorm rule unchanged). The part of a partly cash-paid stock-out that is still owed is asked by the stock-out's own QR. |

## Assumptions

- The brainstorm and its review are the approved spec; Q1–Q11 refine it. The three accountant/PO questions of the brainstorm (cash fund receipts paid by QR, a customer code in the content, deposits on partly paid transfers) are go-live gates (Phase 05 checklist), not blockers.
- Local setup as for the earlier plans: PostgreSQL on `localhost:5432` (`postgres`/`1`); LibreOffice at `C:\Program Files\LibreOffice\program\soffice.exe`; PyMuPDF (`import pymupdf`) is installed for Python 3.14, which Task 3.6 uses to measure the printed QR; HTTP clients (`curl`, `wget`) are denied, so real output is produced by a test and converted with `soffice` directly.
- `qlbanhang/main` may move while this runs; Phase 00 and Phase 05 check `git ls-remote` and merge main before the final verification.

## Risks

- **Layout on paper.** ClosedXML picture anchors are read back in unit tests, but only a real conversion shows the size and place of the QR after LibreOffice shrinks the page. Task 3.6 converts the samples and measures the picture with PyMuPDF; the scale constant is corrected there.
- **A QR that does not scan after the PDF conversion.** LibreOffice's default PDF export may resample the 424-pixel PNG to about 295 pixels over 25 mm (not a whole number of pixels per module) and JPEG-compress it. If the embedded image is blurred or the decode fails, the fix is a decision for the user (PDF filter options on the shared converter, or drawing the QR as filled cells instead of a picture), not a silent change.
- **A QR that does not scan.** The decode-back test proves the PNG; the printed size (25 mm, about 0.47 mm per module with the quiet zone) is checked by the manual scan in the go-live checklist, with at least three bank apps on A5, A4 and the screen.
- **Money to the wrong account.** A receipt booked to a cash fund gets the branch's default bank fund QR; the "Kèm QR" switch and the dialog's fund line ("Quỹ nhận") are the mitigation, the policy question stays with the accountant (brainstorm "Trường hợp biên").
- **Two QRs for one payment.** A stock-out paid by bank transfer and its automatic receipt give identical QRs (same account, amount, content `PX…`); accepted by the brainstorm (R1).
- **Clipboard and share APIs.** `navigator.clipboard` needs HTTPS and `canShare({ files })` is present on Chromium and Edge on Windows and on Safari on macOS too, so `shareOrDownload` shares only when the device is touch-primary (`pointer: coarse`) and a computer always downloads (decided, checklist row 11); the dialog toasts a failed copy and falls back to a file download. Only a real iPhone running the installed app proves the share sheet (go-live).
- **Concurrent docs work.** The cash voucher form redesign and other plans edit `cash-voucher-form-page.tsx` and the same docs files; resolve conflicts by keeping both additions.
- **Machine load.** Run the backend track while at most two other tracks run tests; check `git worktree list`.
- **Baseline failures.** Phase 00 records them; later phases compare test by test.

## Phases

- [x] Phase 00 — Worktree, branch, failure baseline (S) — `phase-00-baseline.md`
- [x] Phase 01 — Bank and account holder on funds (M) — `phase-01-fund-bank-fields.md`
- [x] Phase 02 — Payment QR rules, service and `payment-qr` routes (L) — `phase-02-payment-qr-backend.md`
- [x] Phase 03 — QR on the printed vouchers and the print switch (XL) — `phase-03-print-with-qr.md`
- [x] Phase 04 — QR button and dialog on the voucher screens (M) — `phase-04-screen-dialog.md`
- [x] Phase 05 — Docs, real output, go-live checklist, final verification (M) — `phase-05-docs-and-verification.md`

Order: 00 → 01 → 02. After 02, Phase 03 (backend print + print switch, worktree `voucher-qr`) and Phase 04 (screen, frontend only, may use the second worktree) are independent: Phase 04 mocks the API and the route is fixed below. Phase 05 runs last on the merged result. Run one phase per request (session limits) unless the user asks for several.

## Shared contracts (frozen in the phase named; later phases only use them)

```csharp
// Domain/Entities/CashDebt/Fund.cs (Phase 01)
public Guid? BankId { get; set; }                    // FK Banks, OnDelete Restrict, indexed
public Bank? Bank { get; set; }
public string? AccountHolderName { get; set; }       // max 255

// Application/CashDebt/Funds/Models/FundDtos.cs (Phase 01): FundDto gains
public Guid? BankId { get; set; }
public string? BankShortName { get; set; }           // Bank.ShortName ?? Bank.Code
public string? AccountHolderName { get; set; }
// CreateFundRequest and UpdateFundRequest gain BankId (Guid?) and AccountHolderName (string?).

// Application/CashDebt/PaymentQr/ (Phase 02)
public enum PaymentQrKind { Cash, Stock }

public enum PaymentQrBlock
{
    None, NotApplicable, Cancelled, AutoReceiptInCash, NotSale, NoAmount, FundIncomplete, NoDefaultBankFund,
}

/// A fund with its bank, flat; BankBin/BankName/BankShortName are null without a bank.
public sealed record QrFundInfo(
    Guid Id, string Code, string Name, FundKind Kind, bool IsActive, string? AccountNumber, string? AccountHolderName,
    string? BankBin, string? BankName, string? BankShortName, bool BankIsActive);

public static class VoucherPaymentQrRules
{
    public static decimal TransferAmount(decimal total, decimal paidAmount, FundKind? fundKind);   // round(total - (fundKind == Cash ? paidAmount : 0)), away from zero
    public static bool IsQrReady(QrFundInfo? fund);                                                  // active, Kind Bank, active bank with BIN, account number, holder name
    public static (QrFundInfo? Fund, PaymentQrBlock Block) PickFund(QrFundInfo? voucherFund, QrFundInfo? branchDefaultBank);
    public static PaymentQrBlock CheckCash(CashVoucherType type, DocumentStatus status, bool isAutomatic, FundKind fundKind);   // Payment -> NotApplicable; Cancelled; automatic receipt in a non-bank fund -> AutoReceiptInCash
    public static PaymentQrBlock CheckStock(StockDirection type, StockVoucherStatus status, DebtEffect reasonEffect);
    public static PaymentQrBlock CheckAmount(decimal amount);                                        // amount <= 0 -> NoAmount
    public static string Content(string voucherCode, string? sourceStockVoucherCode);                // source ?? voucher code
    public static string Message(PaymentQrBlock block, string? fundCode);                            // the 422 texts
}

public class VoucherPaymentQrDto
{
    public string Payload { get; set; } = default!;
    public decimal Amount { get; set; }                 // what the QR asks for
    public decimal Total { get; set; }                  // the voucher total
    public decimal CashPaid { get; set; }               // stock-out paid into a cash fund, else 0
    public string BankName { get; set; } = default!;
    public string BankShortName { get; set; } = default!;
    public string AccountNumber { get; set; } = default!;
    public string AccountHolderName { get; set; } = default!;   // full name, not the cut payload text
    public string Content { get; set; } = default!;             // the voucher code or the source stock-out code
    public string FundCode { get; set; } = default!;
    public string FundName { get; set; } = default!;
    public string? SourceStockVoucherCode { get; set; }         // automatic receipt only
}

public interface IVoucherPaymentQrService
{
    Task<VoucherPaymentQrDto> GetForCashVoucherAsync(Guid id, CancellationToken ct = default);
    Task<VoucherPaymentQrDto> GetForStockVoucherAsync(Guid id, CancellationToken ct = default);
    /// For printing: only the vouchers that get a QR; never throws for an ineligible voucher.
    Task<IReadOnlyDictionary<Guid, VoucherPaymentQrDto>> ResolveManyAsync(
        PaymentQrKind kind, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}

// Application/Tt133/Vouchers (Phase 03)
CashVoucherPrintModel.Qr / StockVoucherPrintModel.Qr : VoucherPaymentQrDto?
CashVoucherPrintRequest.IncludeQr / StockVoucherPrintRequest.IncludeQr : bool (default false)
VoucherPrintRules.QrAmountLine(VoucherPaymentQrDto) -> "Số tiền: 1.000.000 đồng"
VoucherPrintRules.QrBreakdownLine(VoucherPaymentQrDto) -> null when CashPaid == 0, else "Tổng 1.500.000 · Đã trả tiền mặt 500.000 · Cần chuyển khoản 1.000.000"
VoucherPrintRules.QrBankCaption(VoucherPaymentQrDto) -> "{BankShortName} · {AccountNumber}"
VoucherPrintRules.QrScanCaption = "Quét để chuyển khoản"
// Infrastructure/Qr/QrPngRenderer.cs: public static byte[] Render(string payload, int pixelsPerModule = 8)  // error level M, quiet zone 4 modules
```

```text
GET  api/cash-vouchers/{id}/payment-qr     200 ApiResponse<VoucherPaymentQrDto> | 422 PAYMENT_QR_UNAVAILABLE | 403 | 404
GET  api/stock-vouchers/{id}/payment-qr    same
GET  api/cash-vouchers/{id}/print?format=…&includeQr=true
POST api/cash-vouchers/print?format=…        body { ids, includeQr }
GET  api/stock-vouchers/{id}/print?format=…&priceMode=…&includeQr=true
POST api/stock-vouchers/print?format=…       body { ids, priceMode, includeQr }
```

```ts
// frontend (Phase 03)  features/voucher-print/qr-pref-storage.ts
export function readQrPref(userId: string | undefined): boolean;          // default true; localStorage qldh:voucher-print-qr:{userId}
export function writeQrPref(userId: string | undefined, on: boolean): void;
// VoucherPrintRequest.includeQr?: boolean   (sent only when true: query `includeQr=true` for one voucher, body field for several)
// frontend (Phase 04)  features/voucher-payment-qr/types.ts
export type PaymentQrKind = 'cash' | 'stock';
export interface VoucherPaymentQr { /* the DTO above, camelCase */ }
export function useVoucherPaymentQr(kind: PaymentQrKind, id: string | undefined, options: { enabled: boolean }): UseQueryResult<VoucherPaymentQr>;
// components/payment-qr/payment-qr-button.tsx: <PaymentQrButton kind id code disabled disabledReason />
```

## Final Verification

From the worktree, after Phase 05 has merged every track:

- Backend (`backend/`): `export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_vqr;Username=postgres;Password=1"`, then `dotnet build OrderMgmt.sln` and `dotnet test OrderMgmt.sln --results-directory "$TEMP/vqr-trx"`; the failures equal `BASELINE.md`, test by test.
- Frontend (`frontend/`): `npm run typecheck`, `npm run lint`, `npm run test -- --testTimeout=30000`, `npm run build`, then `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo` (skip an untracked path).
- Real output (Task 3.6, re-checked after the merge): the 01-TT A5 and 02-VT A4 samples converted with LibreOffice, the printed QR measured at 25 mm ± 1 mm, the embedded image inspected, a page rendered and looked at.
- End to end in a real browser (Task 5.2) and the manual scan table (Task 5.1, `GO-LIVE-CHECKLIST.md`).

## Rollback / Recovery

- Nothing is pushed or merged by the phases. The feature is additive: reverting the merge commit removes the routes, the print switch and the dialog.
- The migration only adds two nullable columns, a foreign key and an index to `funds`. To undo it on a database that ran it: `dotnet ef database update <previous migration> -p src/OrderMgmt.Infrastructure -s src/OrderMgmt.WebApi` (the previous migration is `20261014130035_AddBranchSampleData`), then delete the new migration files.
- A bad print layout never blocks printing: the QR is off when "Kèm QR" is unticked, and `includeQr` defaults to `false` on the API.
