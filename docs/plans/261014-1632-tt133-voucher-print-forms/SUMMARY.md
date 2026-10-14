# TT133 voucher print forms — Excel/PDF of receipts, payments, stock-in and stock-out vouchers (Mẫu in chứng từ TT133)

> Created: 2026-10-14 16:32 · Based on `qlbanhang/main` at `8135477` (PR #14 executive dashboard merged) · Source design: [brainstorm SUMMARY](../../brainstorms/261013-2042-tt133-voucher-print-forms/SUMMARY.md) (reviewed against code 2026-10-14, R1–R12) · Builds on: [TT133 report forms plan](../archived/261013-1926-tt133-report-forms/SUMMARY.md) (PR #12) · Reviewed against code: 2026-10-14 20:15 (fixes applied: test data that the guards refuse, lint in test wrappers, list page variables, signature and header layout, portal clicks on list rows, indexed ledger read, a worktree per track)

## Goal

Print the four vouchers the software already records in the layout of Circular 133/2016/TT-BTC (Phụ lục 3), as Excel and PDF, from the voucher page and from the list page (one voucher, or several selected rows, one sheet / page each):

| Form | Title | Source | Paper | Routes |
|---|---|---|---|---|
| 01-TT | Phiếu thu | `CashVoucher`, `Type = Receipt` | A5 landscape | `GET api/cash-vouchers/{id}/print`, `POST api/cash-vouchers/print` |
| 02-TT | Phiếu chi | `CashVoucher`, `Type = Payment` | A5 landscape | same |
| 01-VT | Phiếu nhập kho | `StockVoucher`, `Type = In` | A4 portrait | `GET api/stock-vouchers/{id}/print`, `POST api/stock-vouchers/print` |
| 02-VT | Phiếu xuất kho | `StockVoucher`, `Type = Out` | A4 portrait | same |

Stock vouchers print in one of three price modes chosen at print time: the voucher's prices (`Voucher`), the inventory cost read from the stock ledger (`Cost`), or no money (`None`). The printed numbers are the voucher's own numbers (or the ledger's, for `Cost`), so paper equals screen.

## Scope

**In scope**

- Backend:
  - `Tt133ExportSupport`: the form context, the sheet-count check and the xlsx → `ExportFile` / PDF step, moved out of `Tt133ExportService` so both services use them (behaviour of the report exports unchanged);
  - `Application/Tt133/Vouchers/`: `IVoucherPrintService` / `VoucherPrintService`, `IVoucherFormRenderer`, the print models, `VoucherPrintRules` (pure rules), `StockPrintPriceMode`;
  - `Infrastructure/Excel/Tt133/Vouchers/`: `VoucherFormBuilder`, `CashVoucherForm`, `StockVoucherForm`, `VoucherFormRenderer`;
  - four actions on `CashVouchersController` and `StockVouchersController`.
- Frontend:
  - `lib/blob-download.ts` (shared file request and save, used by the TT133 exports too), `lib/pdf-tab.ts` (PDF in a new tab opened before the request);
  - `CompanyProfileHint` taken out of the TT133 export button and reused;
  - `features/voucher-print/` and `components/voucher-print/` (`VoucherPrintButton`, `StockPriceModeDialog`);
  - `lib/use-row-selection.ts` and `components/ui/selection-checkbox.tsx`;
  - print buttons on the four voucher pages, a checkbox column, a row print button and a "Đã chọn N phiếu" bar on the four list pages.
- Docs: `docs/SUMMARY.md`, `conventions.md`, `directory-structure.md`, `system-architecture.md`, `product-goals.md`, the brainstorm's next steps, a go-live checklist for the accountant.

**Out of scope** (brainstorm non-goals)

- Fund transfer vouchers, a list summary ("bảng kê"), admin-editable templates, per-user templates.
- Paper size or copies settings (copies are set in the printer dialog), HTML print pages, a print permission of its own, TT99.
- Chart of accounts: Nợ, Có and Quyển số print as dotted lines. Foreign currency lines of 01-TT/02-TT are left out.
- Any migration, any change to how voucher totals or costs are computed.

## Decisions made while planning (in addition to the brainstorm and its review R1–R12)

| # | Topic | Decision |
|---|---|---|
| V1 | Branch and workspace | Branch `feat/tt133-voucher-print-forms` from `qlbanhang/main`, in the worktree `.claude/worktrees/tt133-voucher-print` (another session works in the main checkout). Backend tests use their own database base name `qldonhang_integtest_vprint`. When the frontend track runs at the same time as the backend track, it works in a second worktree `.claude/worktrees/tt133-voucher-print-fe` on `feat/tt133-voucher-print-forms-fe` (branched from the baseline commit of Phase 00, Task 0.3; no database needed) and is merged into `feat/tt133-voucher-print-forms` by Phase 05 Task 5.0. Two agents never commit in the same worktree. |
| V2 | Shared export pieces | New `Application/Tt133/Services/Tt133ExportSupport.cs` (scoped, concrete class, no interface): `BuildContextAsync(ReportScope, ct)`, `EnsureSheetCount(int count, Func<int, int, string> tooManyMessage)`, `ToFileAsync(byte[] xlsx, string baseName, ExportFormat, ct)` and the two content-type constants. The bodies are moved unchanged from `Tt133ExportService`; the report exports pass their current message `"Số sổ cần xuất ({count}) vượt giới hạn {max}. Hãy thu hẹp điều kiện lọc."`, the voucher prints pass `"Số phiếu cần in ({count}) vượt giới hạn {max}. Hãy chọn ít phiếu hơn."`. The `EXPORT_NO_TARGETS` message stays `"Không có đối tượng nào để xuất."` for both. |
| V3 | Layering | Application: `Application/Tt133/Vouchers/` (one namespace `OrderMgmt.Application.Tt133.Vouchers`): `IVoucherPrintService.cs`, `IVoucherFormRenderer.cs`, `VoucherPrintModels.cs`, `VoucherPrintRules.cs`, `VoucherPrintService.cs`. Infrastructure: `Infrastructure/Excel/Tt133/Vouchers/` (namespace `OrderMgmt.Infrastructure.Excel.Tt133.Vouchers`): `VoucherFormBuilder.cs`, `CashVoucherForm.cs`, `StockVoucherForm.cs`, `VoucherFormRenderer.cs`. `Tt133FormRenderer.Render` becomes `internal static` so `VoucherFormRenderer` reuses its save step (it makes the repeated table rows absolute for LibreOffice). |
| V4 | Builder | `VoucherFormBuilder` is a builder of its own that **wraps** a `Tt133FormBuilder` (property `Table`) for the table header, rows, summary rows, plain text lines and the column widths, the way `DebtReconciliationForm` already writes its own blocks next to the builder. It writes the voucher blocks itself: the unit block without the MST line, the title block (title, "ĐÃ HỦY", date, number, Nợ/Có), the label lines, the signature block with N signers, and the paper setup. Signer blocks are split by **column width**, not by column count (02-VT has a 5-wide STT column next to a 34-wide name column), and their texts wrap with explicit row heights. `Tt133FormBuilder` is not changed (brainstorm decision); a form that needs a taller table-header row sets it itself (01-VT/02-VT). |
| V5 | Routes | `GET api/cash-vouchers/{id:guid}/print?format=xlsx\|pdf`, `POST api/cash-vouchers/print?format=…` with `CashVoucherPrintRequest { ids }`, `GET api/stock-vouchers/{id:guid}/print?format=…&priceMode=Voucher\|Cost\|None`, `POST api/stock-vouchers/print?format=…` with `StockVoucherPrintRequest { ids, priceMode }`. Both controllers carry `[DashboardSignal]`, so each POST action gets `[SkipDashboardSignal]` (it changes nothing) and `DashboardSignalTests.WithoutHost.Every_document_controller_is_tagged` (the reflection test that lists the skipped actions) is updated in the same task. |
| V6 | Access and errors | As the voucher page (`GetAsync`), checked per id in request order: missing or soft-deleted → 404 (`NotFoundException`); no view permission of the voucher's type (`CashVoucherPermissions.View` / `StockVoucherPermissions.View`) → 403; another branch than `ICurrentBranch` → 403 `"Phiếu thuộc chi nhánh khác."`. The first failing id fails the whole request. Before any database read: an undefined `priceMode` → 400 (key `priceMode`, `"Chế độ giá không hợp lệ."`); `Cost` without `inventory.view_cost` → 403 `"Cần quyền xem giá vốn để in phiếu theo giá vốn."`. Then `ids` (a JSON `null` counts as empty) is de-duplicated and counted with `EnsureSheetCount` (0 → 422 `EXPORT_NO_TARGETS`, more than `MaxSheets` → 422 `EXPORT_TOO_MANY_SHEETS`); counting before any read is new for vouchers (the report exports count inside their snapshot) and safe, because `EnsureSheetCount` reads only the options. PDF failure → 503 `PDF_EXPORT_FAILED`. An undefined `format`, or an undefined `priceMode` in the query string, is refused by model binding with a 400 `ValidationProblemDetails` body (not an `ApiResponse`); only a `priceMode` from the JSON body (integers are accepted by `JsonStringEnumConverter`) reaches the service check. |
| V7 | One snapshot | As the report exports: one `ITransactionRunner.RunSnapshotAsync` loads the form context (`ReportScope.Branch`; the vouchers are in the working branch after V6), the vouchers, the ledger rows and the inventory settings; rendering and the PDF conversion run after it. Queries use `IgnoreQueryFilters()` and filter `!IsDeleted` on the voucher and its lines by hand (pattern of `CashVoucherService.LoadDtoAsync`), so soft-deleted partners, reasons, warehouses and users keep their names and lines removed by an edit never print (the reason and the owner are required navigations: with the filters on, a soft-deleted one would drop the voucher and answer 404). Ledger rows are read through the `(SourceType, SourceId)` index (`SourceType` StockIn/StockOut, `SourceId` among the printed voucher ids) and matched to the lines by `SourceLineId` in memory; `SourceLineId` has no index and no migration is allowed. |
| V8 | Sheets and files | Sheets in `VoucherAt`, then `Code` (ordinal) order, one per voucher, named by `Tt133FormBuilder.SheetName(code, used)`. One voucher → `{Code}.{xlsx\|pdf}`. Several → `{prefix}_{yyyyMMdd-HHmm}` in VN time with prefix `PhieuThu`, `PhieuChi`, `PhieuNhapKho`, `PhieuXuatKho`, or `PhieuThuChi` / `PhieuNhapXuat` when a batch mixes types (only an API caller can do that: the list pages are per type). The frontend builds the same names itself (T10 of the report plan). |
| V9 | Cash voucher content | Person: `PersonName`, else `PartnerName`. Address: `PartnerAddress`. Reason: `Description`, else the line descriptions in `SortOrder` joined by `"; "`, else the cash reason name. Amount line `"Số tiền: 1.500.000 đồng"` (vi-VN digits) and `"(Viết bằng chữ): {VndAmountInWords.Read(Total)}"`. "Kèm theo": a whole number `n` → `"Kèm theo: n chứng từ gốc"`, other text → `"Kèm theo: {text}"`, blank → `"Kèm theo: …………………… chứng từ gốc"`. A blank value prints `VoucherPrintRules.Dots` (`"……………………"`). The date above the signatures is the voucher's VN date. Giám đốc = `LegalRepresentativeName`, Kế toán trưởng = `ChiefAccountantName`, Người lập phiếu = the owner's full name; the other signers have no name. |
| V10 | Stock voucher lines | Name: `ProductName`; a line whose `PricingMode` is not `PerUnit` adds `" ({L}×{W}×{T} mm, {S} tấm)"` with only the dimensions that are set and greater than zero and the sheet count when set (vi-VN number format `0.###`), e.g. `"Kính (1200×2400×5 mm, 10 tấm)"`. Unit: `UnitName` as stored (m, m², m³ for those modes). Both quantity columns print `Quantity`. Warehouse: the names of the printed lines' warehouses, distinct, in line order, joined by `", "`; no printed line → the voucher's warehouse. Several warehouses add a column `Kho` (warehouse code) after Đơn vị tính. |
| V11 | Price modes | **Voucher:** every line, `UnitPrice` / `Amount`. Rows under the table come from `VoucherPrintRules.VoucherTotals`: when the discount (`LineDiscountTotal + OrderDiscount`), the added VAT and `Freight` are all 0, one row `"Cộng"` = `Total`; otherwise `"Cộng tiền hàng"` = `GoodsAmount`, then `"Chiết khấu"`, `"Thuế GTGT"`, `"Cước vận chuyển"` (each only when not 0), then `"Tổng cộng"` = `Total`. Added VAT = `Total − Freight − (GoodsAmount − LineDiscountTotal − OrderDiscount)` (from the stored totals, never from the current `NetExcludesVat` setting). A stock-out with `PriceIncludesVat` lines adds the italic line `"Trong đó thuế GTGT đã gồm trong giá: {Σ VatAmount of those lines}"` under the table, outside the sum. Words: `Total`. **Cost:** `TrackInventory` lines only; the line's ledger row (`InventoryLedgerEntry.SourceLineId`): stock-in `R4(InValue / QtyIn)` and `InValue`, stock-out `UnitCost` and `CostAmount`; one row `"Cộng"` = their sum; words: that sum. Unit costs are rounded with a private `R4` (`Math.Round(x, 4, MidpointRounding.AwayFromZero)`, the stock card's rounding; the report's `R4` is private). A stock-out for which `CostProvisional.IsProvisional(VN date, settings.CostingPeriod, today VN)` is true (its costing period ends on or after today; the stock card's rule, `Inventory/Common/CostProvisional.cs`) gets the note `"Giá vốn tạm tính (kỳ tính giá vốn chưa kết thúc)."`; a stock-in never does (its value is its own). **None:** `TrackInventory` lines only, empty price and amount, row `"Cộng"` without amount, words as dots. **Cancelled voucher in Cost:** printed as None with the note `"Phiếu đã hủy, không có giá vốn."` (its ledger rows are gone). **No printed line** (Cost or None): the table has no rows, `"Cộng"` has no amount, words as dots, note `"Phiếu không có hàng theo dõi tồn kho."`. |
| V12 | Page setup | Cash vouchers: A5 landscape, fit to one page wide and one page tall (one voucher = one page). Stock vouchers: A4 portrait, fit to one page wide, table header rows repeated on every page. Margins as the report forms (0.59 inch). |
| V13 | Frontend | `VoucherPrintButton` (menu "In (PDF)" / "Excel"): PDF opens in a new tab opened **before** the request (`openPdfTab`, the quotation pattern, closed again on failure); Excel downloads through `saveBlob`. Failures show the toast `"Không thể in phiếu"` with `formatApiErrorDetails`. Stock vouchers first open `StockPriceModeDialog` (Giá trên phiếu / Giá vốn / Không in tiền); Giá vốn is hidden without `inventory.view_cost`; the last choice is kept in `localStorage` key `qldh:stock-print-price-mode:{userId}` (try/catch, default `Voucher`, a stored `Cost` without the permission falls back to `Voucher`). Voucher page: the button shows for a saved voucher (`isEdit`) and is disabled while `form.formState.isDirty`, with the title `"Lưu thay đổi trước khi in"`. List page: a checkbox column (select-all of the page with an indeterminate state), a row print button (icon), and a bar `"Đã chọn N phiếu"` with the print button and "Bỏ chọn"; the selection is cleared when the page, the page size or a filter changes. React click events bubble out of portals along the component tree, and a Radix dialog's overlay is a sibling of its content, so the list rows navigate only for clicks inside their own DOM (`e.currentTarget.contains(e.target as Node)`): a click in the menu, in the price-mode dialog or on its overlay never opens the voucher. `CompanyProfileHint` ("Chưa có thông tin đơn vị", link to Cài đặt with `settings.company`) is rendered by `VoucherPrintButton` in its `button` variant (voucher page, selection bar), not by the row icons. |
| V14 | Tests | Pure tests: `backend/tests/OrderMgmt.IntegrationTests/Tt133/Unit/Vouchers/` (no collection, no database). Integration tests: `backend/tests/OrderMgmt.IntegrationTests/Tt133/Vouchers/` (`[Collection(nameof(PostgresCollection))]`; cash tests inherit `CashDebtTestBase`, stock tests `InventoryTestBase`). Workbooks are read with `Tt133Sheet.OpenAsync` and rows found with `Tt133Sheet.RowOf` / `Contains` (exact text), never by a fixed row number. Frontend tests sit next to the file under test. |

## Assumptions

- The brainstorm and its review (R1–R12) are the approved spec; V1–V14 refine it. The accountant/PO questions of the brainstorm are **not answered yet**; they are go-live gates (Phase 05 checklist), not blockers.
- Layouts come from secondary sources; the accountant compares the printed samples with the original Phụ lục 3 before go-live. Known deviations are listed in the checklist (no Nợ/Có/Quyển số values, no foreign-currency lines, extra total rows in `Voucher` mode, the spec in the name column, the `Kho` column, "(Hoặc bộ phận có nhu cầu nhập)" not printed under Kế toán trưởng).
- Local setup as for the report plan: PostgreSQL on `localhost:5432` (`postgres`/`1`); LibreOffice at `C:\Program Files\LibreOffice\program\soffice.exe`; HTTP clients (`curl`, `wget`) are denied in this environment, so real output is produced by a test and converted with `soffice` directly (Phase 05).
- A voucher list page never shows more than 100 rows (`PAGE_SIZE_OPTIONS`), below `MaxSheets` (300), so the UI never meets `EXPORT_TOO_MANY_SHEETS`.

## Risks

- **Dirty state on the voucher pages.** Both forms load clean: the stock page's due-date effect (`stock-voucher-form-page.tsx` L404-412) never fires on load (`partnerOf(voucher)` carries no credit days, and a stored due date marks the field as touched), the `paidAmount` / `fundId` effects set values without `shouldDirty`, and the cash allocation effect returns early for stored vouchers. One edge remains: after a user edits a field and types the old value back, `isDirty` can stay true when a silent `setValue` changed the defaults (e.g. a voucher without a stored fund gets the default fund), so "In" stays disabled until the user saves. Accepted (the button title says why); Phases 02 and 04 test a clean edit form.
- **Layout on paper.** ClosedXML properties are read back in unit tests, but only a real conversion shows the A5 page, wrapping and the repeated header. Phase 05 converts every form with LibreOffice, checks the page size from the PDF `MediaBox` and looks at each page.
- **Number format in PDF cells.** Numeric cells follow the LibreOffice locale (`1.500.000` in the backend image, which runs `soffice` in `vi_VN`; maybe `1,500,000` on a local Windows install). Text lines (amount line, VAT line) are formatted by the backend and do not depend on it.
- **Concurrent docs work.** The branch-sample-data plan edits the same five docs; Phase 05 docs edits may conflict at merge. Resolve by keeping both additions.
- **Machine load.** The branch-sample-data plan runs up to four lanes in its own worktrees (`branch-sample-data`, `sd-*`); this machine saturates beyond about three test-running tracks (full backend suite 30–52 minutes under load). Run this plan's backend track only while at most two other tracks run tests; the frontend track (vitest only) is lighter. Check `git worktree list` and ask the user when unsure.
- **Layout the unit tests cannot see.** Text clipped in a narrow column, or a row too low for wrapped text, only shows in a real PDF. The plan pins the two known cases with tests (signer blocks by width, the 01-VT/02-VT sub-header row height); Phase 05 looks at every page.
- **Baseline failures.** Phase 00 records them; later phases compare test by test.

## Phases

- [ ] Phase 00 — Worktree, branch, failure baseline and the parallel decision (S) — `phase-00-baseline.md`
- [ ] Phase 01 — Shared export support and cash voucher forms, backend (L) — `phase-01-cash-voucher-backend.md`
- [ ] Phase 02 — Print button and row selection on the cash voucher pages (M) — `phase-02-cash-voucher-frontend.md`
- [ ] Phase 03 — Stock voucher forms and price modes, backend (L) — `phase-03-stock-voucher-backend.md`
- [ ] Phase 04 — Price mode dialog and the stock voucher pages (M) — `phase-04-stock-voucher-frontend.md`
- [ ] Phase 05 — Real output, go-live checklist, docs, final verification (M) — `phase-05-docs-and-verification.md`

Order: 00 first. Phases 01 → 03 (backend) and 02 → 04 (frontend) are two tracks that may run in parallel, because the frontend tests mock the API and the routes are fixed above; Phase 03 needs Phase 01 and Phase 04 needs Phase 02. In parallel, the backend track works in `.claude/worktrees/tt133-voucher-print` and the frontend track in `.claude/worktrees/tt133-voucher-print-fe` (V1, created by Phase 00 Task 0.3); one after the other, both work in the first worktree. Phase 05 runs last, on the merged result (Task 5.0 merges the frontend branch).

## Shared contracts (frozen in the phase named; later phases only add the members marked for them)

```csharp
// Application/Tt133/Services/Tt133ExportSupport.cs (Phase 01 Task 1.1) — scoped, registered in Application/DependencyInjection.cs
public class Tt133ExportSupport
{
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string PdfContentType = "application/pdf";

    public Tt133ExportSupport(IAppDbContext db, ICurrentUser currentUser, ICurrentBranch currentBranch, IDateTime clock,
        ISpreadsheetPdfConverter pdfConverter, IOptions<Tt133ExportOptions> options, ILogger<Tt133ExportSupport> logger);

    /// Company profile, working branch (scope Branch) and signers; moved from Tt133ExportService.BuildContextAsync.
    public Task<Tt133FormContext> BuildContextAsync(ReportScope scope, CancellationToken ct);
    /// 0 → 422 EXPORT_NO_TARGETS "Không có đối tượng nào để xuất."; more than MaxSheets → 422 EXPORT_TOO_MANY_SHEETS tooManyMessage(count, max).
    public void EnsureSheetCount(int count, Func<int, int, string> tooManyMessage);
    /// Xlsx as is ({baseName}.xlsx); Pdf through ISpreadsheetPdfConverter ({baseName}.pdf), a failure → 503 PDF_EXPORT_FAILED.
    public Task<ExportFile> ToFileAsync(byte[] xlsx, string baseName, ExportFormat format, CancellationToken ct);
}

// Application/Tt133/Vouchers/VoucherPrintModels.cs
public enum StockPrintPriceMode { Voucher = 0, Cost = 1, None = 2 }                                    // Phase 01

public class CashVoucherPrintRequest { public List<Guid> Ids { get; set; } = new(); }                  // Phase 01
public class StockVoucherPrintRequest                                                                   // Phase 03
{
    public List<Guid> Ids { get; set; } = new();
    public StockPrintPriceMode PriceMode { get; set; } = StockPrintPriceMode.Voucher;
}

public class CashVoucherPrintModel                                                                      // Phase 01
{
    public CashVoucherType Type { get; set; }
    public string Code { get; set; } = default!;
    public DateOnly Date { get; set; }                  // VN date of VoucherAt
    public bool IsCancelled { get; set; }
    public string? PersonName { get; set; }             // after the V9 fallback
    public string? Address { get; set; }
    public string? Reason { get; set; }                 // after the V9 fallback
    public decimal Amount { get; set; }                 // Total
    public string AmountInWords { get; set; } = default!;
    public string AttachedDocsLine { get; set; } = default!;   // the whole "Kèm theo: …" line
    public string? PreparerName { get; set; }           // owner's full name
}

public class StockVoucherPrintModel                                                                     // Phase 03
{
    public StockDirection Type { get; set; }
    public string Code { get; set; } = default!;
    public DateOnly Date { get; set; }
    public bool IsCancelled { get; set; }
    public StockPrintPriceMode PriceMode { get; set; }  // the effective mode (Cost on a cancelled voucher → None)
    public List<string> Notes { get; set; } = new();    // italic lines under the title block
    public string? HandlerName { get; set; }            // HandlerName, else PartnerName
    public string? PartnerName { get; set; }            // "của …" on 01-VT
    public string? PartnerAddress { get; set; }         // 02-VT
    public string? ReasonName { get; set; }             // 02-VT
    public string WarehouseNames { get; set; } = default!;
    public bool ShowWarehouseColumn { get; set; }
    public List<StockVoucherPrintLine> Lines { get; set; } = new();
    public List<StockVoucherPrintTotal> Totals { get; set; } = new();
    public string? VatIncludedLine { get; set; }        // "Trong đó thuế GTGT đã gồm trong giá: …" or null
    public string? AmountInWords { get; set; }          // null → dots
    public string? PreparerName { get; set; }
}

public class StockVoucherPrintLine                                                                      // Phase 03
{
    public int Number { get; set; }
    public string Name { get; set; } = default!;        // after V10
    public string Code { get; set; } = default!;
    public string UnitName { get; set; } = default!;
    public string? WarehouseCode { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Amount { get; set; }
}

public sealed record StockVoucherPrintTotal(string Label, decimal? Amount);                            // Phase 03

// Application/Tt133/Vouchers/VoucherPrintRules.cs — pure, unit-tested
public static class VoucherPrintRules
{
    public const string Dots = "……………………";
    public static string? FirstFilled(params string?[] values);                                         // Phase 01
    public static string? CashReason(string? description, IReadOnlyList<string> lineDescriptions, string? reasonName);  // Phase 01
    public static string AttachedDocsLine(string? attachedDocs);                                        // Phase 01
    public static string Money(decimal amount);                                                         // Phase 01: "1.500.000"
    public static string BatchFileBase(string prefix, DateTimeOffset utcNow);                           // Phase 01: "{prefix}_{yyyyMMdd-HHmm}" VN time
    public static string LineName(string productName, PricingMode mode, decimal? length, decimal? width,
        decimal? thickness, decimal? sheetCount);                                                       // Phase 03
    public static IReadOnlyList<StockVoucherPrintTotal> VoucherTotals(decimal goodsAmount, decimal lineDiscountTotal,
        decimal orderDiscount, decimal freight, decimal total);                                         // Phase 03
}

// Application/Tt133/Vouchers/IVoucherFormRenderer.cs — implemented by Infrastructure VoucherFormRenderer
public interface IVoucherFormRenderer
{
    byte[] RenderCashVouchers(Tt133FormContext context, IReadOnlyList<CashVoucherPrintModel> vouchers);    // Phase 01
    byte[] RenderStockVouchers(Tt133FormContext context, IReadOnlyList<StockVoucherPrintModel> vouchers);  // Phase 03
}

// Application/Tt133/Vouchers/IVoucherPrintService.cs
public interface IVoucherPrintService
{
    Task<ExportFile> PrintCashVouchersAsync(IReadOnlyList<Guid> ids, ExportFormat format, CancellationToken ct = default);   // Phase 01
    Task<ExportFile> PrintStockVouchersAsync(IReadOnlyList<Guid> ids, StockPrintPriceMode priceMode, ExportFormat format,
        CancellationToken ct = default);                                                                                      // Phase 03
}

// Infrastructure/Excel/Tt133/Vouchers/VoucherFormBuilder.cs (Phase 01 Task 1.3)
public enum VoucherPaper { A5Landscape, A4Portrait }
public sealed record VoucherSigner(string Title, string Hint, string? Name);
public sealed class VoucherFormBuilder
{
    public VoucherFormBuilder(IXLWorksheet sheet, Tt133FormContext context, IReadOnlyList<Tt133Column> columns,
        VoucherPaper paper, int? labelColumn = null);
    public Tt133FormBuilder Table { get; }
    public IXLWorksheet Sheet { get; }
    public void WriteHeaderBlock(string formCode);
    public void WriteTitleBlock(string title, DateOnly date, bool cancelled, IReadOnlyList<string> centreLines,
        IReadOnlyList<string> rightLines);
    public void WriteLine(string label, string? value);           // "{label} {value}" or "{label} {Dots}"
    public void WriteSignatures(DateOnly date, IReadOnlyList<VoucherSigner> signers);
    public void Finish();
}
```

```ts
// frontend/src/lib/blob-download.ts (Phase 02 Task 2.1)
export const FILE_REQUEST_TIMEOUT_MS = 120_000;
export interface BlobRequest { method: 'get' | 'post'; url: string; params?: Record<string, unknown>; body?: unknown }
export async function requestBlob(request: BlobRequest): Promise<Blob>;   // unwraps blob error bodies (unwrapBlobError)
export function saveBlob(blob: Blob, fileName: string): void;

// frontend/src/lib/pdf-tab.ts (Phase 02 Task 2.2)
export interface PdfTab { show(blob: Blob): void; close(): void }
export function openPdfTab(title: string): PdfTab;                        // call synchronously, before any await

// frontend/src/features/voucher-print/types.ts (Phase 02 Task 2.4; StockPrintPriceMode used from Phase 04)
export type VoucherPrintKind = 'cash' | 'stock';
export type StockPrintPriceMode = 'Voucher' | 'Cost' | 'None';
export interface VoucherPrintTarget { id: string; code: string }
export interface VoucherPrintRequest {
  kind: VoucherPrintKind;
  type: CashVoucherType | StockDirection;
  vouchers: VoucherPrintTarget[];
  format: ExportFormat;                      // from features/tt133-export/types
  priceMode?: StockPrintPriceMode;           // stock only
}
export function voucherPrintFileBase(request: Pick<VoucherPrintRequest, 'kind' | 'type' | 'vouchers'>, now?: Date): string;

// frontend/src/features/voucher-print/api.ts
export function fetchVoucherPrint(request: VoucherPrintRequest): Promise<Blob>;

// frontend/src/features/voucher-print/use-voucher-print.ts
export function useVoucherPrint(): { print: (request: VoucherPrintRequest) => Promise<void>; isPending: boolean };

// frontend/src/components/voucher-print/voucher-print-button.tsx
export interface VoucherPrintButtonProps {
  kind: VoucherPrintKind;
  type: CashVoucherType | StockDirection;
  vouchers: VoucherPrintTarget[];
  variant?: 'button' | 'icon';               // button: label + CompanyProfileHint; icon: row action, no label, no hint
  label?: string;                            // default "In"
  disabled?: boolean;
  disabledReason?: string;                   // shown as the button title while disabled
}

// frontend/src/lib/use-row-selection.ts (Phase 02 Task 2.7) — selection of the rows of one list page
export function useRowSelection(pageIds: readonly string[], resetKey: string): {
  selectedIds: string[];                     // selected ids of the page, in page order
  isSelected: (id: string) => boolean;
  toggle: (id: string, checked: boolean) => void;
  toggleAll: (checked: boolean) => void;
  allSelected: boolean;                      // the page has rows and every one is selected
  someSelected: boolean;                     // some, not all
  clear: () => void;
};

// frontend/src/components/ui/selection-checkbox.tsx (Phase 02 Task 2.7)
export function SelectionCheckbox(props: {
  checked: boolean; indeterminate?: boolean; onCheckedChange: (checked: boolean) => void; 'aria-label': string;
}): JSX.Element;                             // native checkbox; its click never reaches the row

// frontend/src/features/voucher-print/price-mode-storage.ts (Phase 04 Task 4.1) — key qldh:stock-print-price-mode:{userId}
export function readPriceMode(userId: string | undefined, canViewCost: boolean): StockPrintPriceMode;   // default 'Voucher'
export function writePriceMode(userId: string | undefined, mode: StockPrintPriceMode): void;

// frontend/src/components/voucher-print/stock-price-mode-dialog.tsx (Phase 04 Task 4.1)
export function StockPriceModeDialog(props: {
  open: boolean; onOpenChange: (open: boolean) => void; onConfirm: (mode: StockPrintPriceMode) => void;
}): JSX.Element;
```

## Executor conventions

- **Workspace:** the worktree `.claude/worktrees/tt133-voucher-print` on branch `feat/tt133-voucher-print-forms` (Phase 00); for a frontend track running in parallel, `.claude/worktrees/tt133-voucher-print-fe` on `feat/tt133-voucher-print-forms-fe` (V1). Run every command of a phase inside its track's worktree; never let two agents commit in the same worktree. Keep the shell's drive letter uppercase (`D:\`) while running workflows or worktree agents.
- **Commits:** one per task, conventional commits with scope `tt133`, `vouchers` or `frontend` (e.g. `feat(tt133): 01-TT and 02-TT forms`). End every commit message with the attribution line from the session's system reminder. Stage files explicitly from the task's file list; never `git add -A` / `git add .` (`source/` and `.claude/tools/` must stay out).
- **Shell:** commands are written for Git Bash. Windows PowerShell 5.1 has no `&&`.
- **Test database** — set before any backend test command:
  - bash: `export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_vprint;Username=postgres;Password=1"`
  - PowerShell: `$env:TEST_DB_CONNECTION = "Host=localhost;Port=5432;Database=qldonhang_integtest_vprint;Username=postgres;Password=1"`
  - Never `qldonhang_test` or `qldonhang` (dev data); the fixture refuses them.
- **Backend commands** (from `backend/`):
  - Build: `dotnet build OrderMgmt.sln` (if a running dev server locks `bin`, add `-o "$TEMP/qlbh-vprint-build"`).
  - Targeted tests: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~<ClassName>"`.
  - All tests of this plan: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Tt133"` (includes the report forms, which must stay green).
  - Full suite (17–21 min, more under load): `dotnet test OrderMgmt.sln --logger "trx;LogFileName=current.trx" --results-directory "$TEMP/vprint-trx"` (the tracked `TestResults/*.trx` files must not change).
- **Frontend commands** (from `frontend/`): `npx vitest run <path>`, `npm run typecheck`, `npm run lint`, `npm run test -- --testTimeout=30000`, `npm run build`. After `npm run build`, run `git checkout -- frontend/tsconfig.app.tsbuildinfo frontend/tsconfig.node.tsbuildinfo` (skip a path that is not tracked). The worktree has no `node_modules`: Phase 00 links the main checkout's copy.
- **Test conventions:**
  - Pure backend tests: plain `public class` in `Tt133/Unit/Vouchers/`, `[Fact]` / `[Theory]`, FluentAssertions; a workbook is opened with `new XLWorkbook(new MemoryStream(bytes))`; text found with `sheet.CellsUsed()`.
  - Integration tests: `[Collection(nameof(PostgresCollection))]`, constructor `(PostgresFixture pg) : base(pg)`; company profile set through `InDbAsync(db => db.CompanyProfiles.Where(p => p.Id == 1).ExecuteUpdateAsync(…))` as `CashBookExportTests` does; errors read with `ReadFromJsonAsync<ApiResponse>(TestJson.Options)` and `Error!.Code`; a fake PDF converter and a `MaxSheets` override are `file sealed class` factories copied from `Tt133/ExportPdfTests.cs` and `Tt133/CashBookExportTests.cs` (they are file-scoped there and cannot be shared).
  - **Test data the guards refuse:** a new branch's `QTM` fund holds nothing, and a payment that takes a fund below 0 answers 422 `NEGATIVE_FUND_WARNING` (default policy Warn), so give the fund money first (`SetFundOpeningAmountAsync(fundId, 10_000_000m)`, as `CashVoucherCreateTests` does). A stock-out without stock answers 422 (default `NegativeStockPolicy = Warn`), so stock the same product first in the same warehouse. A branch with an opening date (`SetBranchOpeningDateAsync`) refuses vouchers dated before it. `CreateCashAsync` / `CreateVoucherAsync` assert 200, so such a test fails in its setup, not on the print.
  - **400 from model binding** (undefined `format`, `priceMode=Foo` in the query) is a `ValidationProblemDetails` body: assert the status only.
  - **RED can be a build error:** a test that references a type that does not exist yet fails to compile; that counts as RED ("FAIL — build error, `<Type>` does not exist").
  - Happy-path tasks must not implement the rules of a later task, or that task's RED step cannot fail.
  - Frontend: page tests mock the feature hooks and stub `VoucherPrintButton` (`vi.mock('@/components/voucher-print/voucher-print-button', …)`), because the form-page tests render without a `QueryClientProvider`; component tests mock `@/features/company-profile/hooks` and use the real `useAuthStore.setState` for permissions, as `tt133-export-button.test.tsx` does.
  - **Lint in tests:** `eslint.config.js` applies the jsx-a11y recommended rules to test files too, and the baseline has 3 lint errors. A test wrapper that stands in for a clickable table row (`<div onClick={parentClick}>`) gets `{/* eslint-disable-next-line jsx-a11y/click-events-have-key-events, jsx-a11y/no-static-element-interactions -- stands in for a clickable table row */}` on the line before it; otherwise each wrapper adds 2 errors and the phase checkpoint fails.
- **Rules carried over:** every `DateTimeOffset` sent to EF is UTC; a printed date is `VnTime.ToVnDate(instant)`; DTOs use public `{ get; set; }` with lists defaulting to `new()`; validation `details` keys are camelCase.
- **Comparing with the baseline:** after a full backend run, list the failures with the script of Phase 00 Task 0.2 and `diff` them with `BASELINE.md`.
- **Notation:** `10-05` = 2026-10-05; money in examples is VND.

## Final Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_vprint;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test OrderMgmt.sln --logger "trx;LogFileName=final.trx" --results-directory "$TEMP/vprint-trx"

cd ../frontend
npm run typecheck
npm run lint
npm run test -- --testTimeout=30000
npm run build
git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo
```

Only the failures recorded in Phase 00 `BASELINE.md` may remain, compared test by test. The real-output check, the go-live checklist and the smoke test are Phase 05.

## Rollback / Recovery

- **Code:** every task is a separate commit on `feat/tt133-voucher-print-forms` (frontend tasks of a parallel track first on `feat/tt133-voucher-print-forms-fe`, merged by Task 5.0); `git revert` single commits or drop the branch.
- **Database:** no migration, no seed change, no new permission.
- **Behaviour:** the four routes and the UI additions are new; reverting them leaves the vouchers and the report exports as they were. Task 1.1 (moving the shared export pieces) is covered by the existing TT133 export tests; if it must be undone alone, revert its commit and the commits that call `Tt133ExportSupport`.
