# Phase 03 — Debt scope, ledger fields, S12, debt summary, reconciliation record

**Status:** [ ] pending
**Complexity:** L

## Objective

- Add `scope = Branch | Company` to `debt-balance`, `debt-ledger` and `debt-reconciliation` (review B1).
- Add `ReasonName` and `DueDate` to the debt ledger rows (review B3), on the screen too, and `BranchCode` for the company scope (T21).
- Export **S12-DNN** (one, several or all partners with activity), the **Bảng tổng hợp chi tiết thanh toán** and the **Biên bản đối chiếu công nợ**.
- Frontend: the scope select, the new ledger columns, the export buttons and the target dialog.

## Files

Backend:
- `backend/src/OrderMgmt.Application/Common/ReportScope.cs` (modify — add `ReportScopeGuard`)
- `backend/src/OrderMgmt.Application/CashDebt/Common/DebtBalanceSign.cs` (modify — `SplitMovement`)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Models/CashDebtReportDtos.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Services/CashDebtReportService.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Validators/CashDebtReportValidators.cs` (modify — `Scope` in enum)
- `backend/src/OrderMgmt.Application/Tt133/Models/Tt133ExportModels.cs` (modify — `DebtLedgerExportRequest`)
- `backend/src/OrderMgmt.Application/Tt133/Validators/Tt133ExportValidators.cs` (new)
- `backend/src/OrderMgmt.Application/Tt133/Interfaces/ITt133FormRenderer.cs`, `ITt133ExportService.cs`, `Services/Tt133ExportService.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Excel/Tt133/DebtLedgerForm.cs`, `DebtSummaryForm.cs`, `DebtReconciliationForm.cs` (new)
- `backend/src/OrderMgmt.Infrastructure/Excel/Tt133/Tt133FormRenderer.cs` (modify)
- `backend/src/OrderMgmt.WebApi/Controllers/Tt133ExportController.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Unit/DebtBalanceSignTests.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/DebtLedgerReportTests.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/DebtReportScopeTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Tt133/DebtLedgerExportTests.cs`, `DebtSummaryExportTests.cs`, `DebtReconciliationExportTests.cs` (new)

Frontend:
- `frontend/src/lib/report-scope.ts` (new — `ReportScope`, shared with the stock summary of Phase 04)
- `frontend/src/features/cash-reports/types.ts` (modify)
- `frontend/src/components/report-scope-select/report-scope-select.tsx`, `report-scope-select.test.tsx` (new)
- `frontend/src/components/tt133-export/export-targets-dialog.tsx`, `export-targets-dialog.test.tsx` (new)
- `frontend/src/pages/cash-reports/debt-ledger-table.tsx` (modify)
- `frontend/src/pages/cash-reports/debt-ledger-page.tsx`, `debt-balance-page.tsx`, `debt-reconciliation-page.tsx` and their `.test.tsx` files (modify)

## Reference files (read-only)

- [section-01](../../brainstorms/261012-2237-tt133-report-forms/section-01-form-specs.md) — S12, Bảng tổng hợp, Biên bản đối chiếu; [section-02 B1, B3](../../brainstorms/261012-2237-tt133-report-forms/section-02-review-decisions.md)
- Phase 02 of this plan — builder contract, `Tt133ExportService` helpers, `Tt133Sheet` test helper, `Tt133ExportButton`, `useTt133Download`
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/ReportDataset.cs`, `DebtLedgerReportTests.cs`, `DebtBalanceReportTests.cs`, `DebtReconciliationReportTests.cs`
- `frontend/src/pages/warehouses/warehouse-list-page.tsx` — reading `branches.access_all` in a page

## Contracts

```csharp
// ReportScope.cs
public static class ReportScopeGuard
{
    /// Company needs branches.access_all; otherwise ForbiddenException("Cần quyền làm việc ở mọi chi nhánh để xem toàn công ty.").
    public static void EnsureAllowed(ICurrentUser user, ReportScope scope);
}

// DebtBalanceSign.cs — movement columns of S12 and the summary (P24 convention)
/// Receivable: (increase, decrease). Payable: (decrease, increase).
public static (decimal Debit, decimal Credit) SplitMovement(DebtSide side, decimal increase, decimal decrease);

// CashDebtReportDtos.cs — additions
DebtBalanceReportRequest         { … ReportScope Scope = ReportScope.Branch; }
DebtBalanceReportDto             { … ReportScope Scope; }
DebtLedgerReportRequest          { … ReportScope Scope = ReportScope.Branch; }
DebtLedgerReportDto              { … ReportScope Scope; }
DebtLedgerRowDto                 { … string? ReasonName; DateOnly? DueDate; string? BranchCode; }   // BranchCode: Company scope only (T21)
DebtReconciliationReportRequest  { … ReportScope Scope = ReportScope.Branch; }
DebtReconciliationReportDto      { … ReportScope Scope; string? BranchName; }   // BranchName becomes nullable: null for Company
```

Scope rules in `CashDebtReportService` (it now also injects `ICurrentUser`):
- every `Build…` starts with `ReportScopeGuard.EnsureAllowed(_currentUser, request.Scope)`;
- the entry query is `_db.DebtEntries.AsNoTracking()` plus `.Where(e => e.BranchId == branchId)` only for `Branch`; nothing else changes, so the Company ledger merges the rows of every branch in the existing order (`PostedAt`, `SourceType`, `DocCode`, `Id`) and its running balance is recomputed over the merged rows;
- `BranchCode` (T21): for `Company`, each row carries the code of its entry's branch, read in one query (`_db.Branches.IgnoreQueryFilters()` by the distinct `BranchId` of the period rows); for `Branch` it stays null;
- reconciliation: `sidesWithEntries` follows the same filter, each side's ledger is built with the request's scope, and `BranchName` is null for `Company`;
- validators: `RuleFor(x => x.Scope).IsInEnum().WithMessage("Phạm vi không hợp lệ.")` on the three requests.

Reason names of ledger rows (T8), resolved for the period rows only, in two queries:
- `StockIn` / `StockOut`: `_db.StockVouchers.IgnoreQueryFilters()` by `SourceId` → `Reason.Name`;
- `Receipt` / `Payment`: `_db.CashVouchers.IgnoreQueryFilters()` by `SourceId` → the name from `_db.CashReasons.IgnoreQueryFilters()` (as `FundMovementQuery` reads it);
- `Offset`: the constant `"Bù trừ công nợ"`.

Export routes (all `[HasPermission(Permissions.Reports.Debt)]`; `format` is an optional query parameter on all three). `debt-ledger/export` is a **POST** with `[FromBody] DebtLedgerExportRequest` (T3, T5); the other two are GET with `[FromQuery]`:

| Route | Request | Service method | Base file name |
|---|---|---|---|
| `POST debt-ledger/export` | `DebtLedgerExportRequest` (JSON body) | `ExportDebtLedgerAsync` | `S12-DNN_{from}_{to}` |
| `GET debt-balance/export` | `DebtBalanceReportRequest` | `ExportDebtSummaryAsync` | `TongHopCongNo_{from}_{to}` |
| `GET debt-reconciliation/export` | `DebtReconciliationReportRequest` | `ExportDebtReconciliationAsync` | `DoiChieuCongNo_{PartnerCode}_{from}_{to}` |

`ExportDebtSummaryAsync` (T22): a request with `PartnerId` → `ValidationDomainException`, key `partnerId`, "Bảng tổng hợp xuất cho mọi đối tượng. Dùng sổ S12 cho một đối tượng."; `Search` is passed to the report as the screen does and, trimmed, to `RenderDebtSummary` (null when blank).

`DebtLedgerExportRequestValidator`: `Side` in enum ("Vui lòng chọn loại công nợ."), `From` / `To` in `ReportDates` and `To ≥ From` (the messages of `DebtLedgerReportRequestValidator`), `Scope` in enum, and:
- `All == false` and `PartnerIds` empty → key `partnerIds`, "Chọn ít nhất một đối tượng hoặc xuất tất cả.".

There is no cap on the list itself: `EnsureSheetCount` applies `MaxSheets` to it (T5).

**Notation for the S12 tests below:** a request written as `side=Receivable&partnerIds={KH1}` means `POST /api/reports/debt-ledger/export` (plus `?format=…` when a format is named) with the JSON body `{ "side": "Receivable", "from": "2026-10-01", "to": "2026-10-31", "partnerIds": ["{KH1}"] }`. `from` / `to` are October 2026 unless the test gives others, `all=true` is `"all": true`, `scope=Company` is `"scope": "Company"`, and a repeated `partnerIds=` adds an array element. Send it with `PostAsJsonAsync(url, body, TestJson.Options)`. Enums travel as their names and dates as `yyyy-MM-dd`, as in the other JSON request DTOs of this API (for example `UpsertCashVoucherRequest`); if the body does not bind, compare with how those DTOs are posted before changing any serializer setting.

`ExportDebtLedgerAsync` targets: `All` → the `PartnerId` of every row of `GetDebtBalanceAsync(side, from, to, scope)` (a row exists exactly when the opening balance or the period movement is non-zero; `PartnerIds` is ignored); otherwise the distinct `PartnerIds`. `EnsureSheetCount(targets.Count)`. One `GetDebtLedgerAsync` per target; the ledgers are ordered by `PartnerCode` (ordinal). The context is built with the request's scope.

## Form layouts

### S12-DNN (`DebtLedgerForm`) — landscape, form code `S12-DNN`, one sheet per ledger named by `PartnerCode`

- Title: `SỔ CHI TIẾT THANH TOÁN VỚI NGƯỜI MUA` (Receivable) or `SỔ CHI TIẾT THANH TOÁN VỚI NGƯỜI BÁN` (Payable).
- Parameter lines: `Loại công nợ: Phải thu khách hàng` or `Loại công nợ: Phải trả người bán`; `Đối tượng: {PartnerCode} – {PartnerName}`; `Loại tiền: VNĐ`.

| # | Symbol | Group | Header | Kind | Value |
|---|---|---|---|---|---|
| 1 | A | — | Ngày, tháng ghi sổ | Date | `PostedAt` |
| 2 | B | Chứng từ | Số hiệu | Text | `DocCode` |
| 3 | C | Chứng từ | Ngày, tháng | Date | `DocDate` |
| 4 | D | — | Diễn giải | Text | `Description`, with `[{BranchCode}] ` in front when `BranchCode` is not null (T21) |
| 5 | E | — | Lý do | Text | `ReasonName` |
| 6 | 1 | — | Hạn thanh toán | Date | `DueDate` (empty on Decrease rows) |
| 7 | 2 | Số phát sinh | Nợ | Money | `SplitMovement(side, Increase, Decrease).Debit` |
| 8 | 3 | Số phát sinh | Có | Money | `….Credit` |
| 9 | 4 | Số dư | Nợ | Money | `DebtBalanceSign.Split(side, RunningBalance).Debit` |
| 10 | 5 | Số dư | Có | Money | `….Credit` |

A zero amount is written as an empty cell in columns 7–10. Summary rows: `Số dư đầu kỳ` (9 = `OpeningDebit`, 10 = `OpeningCredit`), `Cộng số phát sinh` (7, 8 = `SplitMovement(side, TotalIncrease, TotalDecrease)`), `Số dư cuối kỳ` (9 = `ClosingDebit`, 10 = `ClosingCredit`). In summary rows a zero is written as 0 in the balance column of the side's natural direction (Nợ for Receivable, Có for Payable) and left empty in the other. Ends with `WriteBookFooter()` and `Finish()`.

### Bảng tổng hợp (`DebtSummaryForm`) — landscape, no form code (`WriteHeaderBlock(null)`), one sheet named `TongHop`

- Title: `BẢNG TỔNG HỢP CHI TIẾT THANH TOÁN VỚI NGƯỜI MUA` or `… VỚI NGƯỜI BÁN`. One parameter line, only when `search` is not null: `Điều kiện lọc: mã hoặc tên chứa "{search}"` (T22).

| # | Symbol | Group | Header | Kind | Value |
|---|---|---|---|---|---|
| 1 | A | — | STT | Quantity | 1, 2, 3… |
| 2 | B | — | Mã đối tượng | Text | `PartnerCode` |
| 3 | C | — | Tên đối tượng | Text | `PartnerName` |
| 4 | 1 | Số dư đầu kỳ | Nợ | Money | `OpeningDebit` |
| 5 | 2 | Số dư đầu kỳ | Có | Money | `OpeningCredit` |
| 6 | 3 | Số phát sinh | Nợ | Money | `SplitMovement(side, Increase, Decrease).Debit` |
| 7 | 4 | Số phát sinh | Có | Money | `….Credit` |
| 8 | 5 | Số dư cuối kỳ | Nợ | Money | `ClosingDebit` |
| 9 | 6 | Số dư cuối kỳ | Có | Money | `ClosingCredit` |

Zero amounts are empty cells in the body. One summary row `Cộng` with the six totals from `report.Totals` (zeros written as 0). An empty report still produces the sheet with the `Cộng` row of zeros. Ends with `WriteBookFooter()` and `Finish()`.

### Biên bản đối chiếu (`DebtReconciliationForm`) — portrait, no form code, no book footer, one sheet named `DoiChieu`, `Finish(repeatTableHeader: false)`

Columns of every table: `A STT (Quantity)`, `B Ngày CT (Date, DocDate)`, `C Số CT (Text, DocCode)`, `D Diễn giải (Text, Description with the `[{BranchCode}] ` prefix of S12)`, `1 Phát sinh tăng (Money, Increase)`, `2 Phát sinh giảm (Money, Decrease)`. The builder is created with `labelColumn: 4`, so the summary labels sit under "Diễn giải".

Lines, in order (T18; `{X}` formatted as in "Money in text" of the SUMMARY):
1. `CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM` (bold, centered); `Độc lập – Tự do – Hạnh phúc` (bold, centered); an empty row.
2. `BIÊN BẢN ĐỐI CHIẾU CÔNG NỢ` (bold, 14, centered); `Từ ngày dd/MM/yyyy đến ngày dd/MM/yyyy` (centered, always this form); an empty row.
3. `Hôm nay, ngày dd tháng MM năm yyyy, chúng tôi gồm:` (from `PrintDate`).
4. `BÊN A: {CompanyName}` (bold); `Địa chỉ: {context.Address}`; `Mã số thuế: {context.TaxCode}`; `Đại diện: {LegalRepresentativeName}`.
5. `BÊN B: {PartnerName}` (bold); `Địa chỉ: {report.Address}`; `Mã số thuế: {report.TaxCode}`; `Đại diện:`.
6. `Hai bên cùng đối chiếu công nợ như sau:`.
7. For each side in `report.Sides`: a bold heading `Công nợ phải thu (Bên B nợ Bên A)` or `Công nợ phải trả (Bên A nợ Bên B)`; the table header; the summary row `Số dư đầu kỳ` with `OpeningBalance` in column 5 when it is ≥ 0, else its absolute value in column 6; the rows; `Cộng phát sinh` (5 = `TotalIncrease`, 6 = `TotalDecrease`); `Số dư cuối kỳ` placed like the opening row with `ClosingBalance`; an empty row.
8. Only when `NetBalance` is not null: `Bù trừ: Bên B còn nợ Bên A {X} đồng` (`NetBalance > 0`), `Bù trừ: Bên A còn nợ Bên B {X} đồng` (`< 0`, absolute value), `Bù trừ: hai bên không còn công nợ` (`== 0`).
9. Conclusion, with `N = NetBalance ?? (receivable side present ? Receivable.ClosingBalance : −Payable.ClosingBalance)` and `0` when no side is listed:
   - `N > 0`: `Tính đến ngày dd/MM/yyyy, Bên B còn nợ Bên A số tiền: {N} đồng (Bằng chữ: {VndAmountInWords.Read(N)}).`
   - `N < 0`: `Tính đến ngày dd/MM/yyyy, Bên A còn nợ Bên B số tiền: {|N|} đồng (Bằng chữ: {VndAmountInWords.Read(|N|)}).`
   - `N == 0`: `Tính đến ngày dd/MM/yyyy, hai bên không còn công nợ.`
   The date is `report.To`.
10. Two signature columns (left half / right half of the six columns), written on `builder.Sheet` from `builder.Row`: `ĐẠI DIỆN BÊN A` and `ĐẠI DIỆN BÊN B` (bold, centered); `(Ký, họ tên, đóng dấu)` under both (italic); four empty rows; `{LegalRepresentativeName}` under Bên A; two rows below, under Bên A only, `Kế toán trưởng` (bold) and, after three empty rows, `{ChiefAccountantName}`.

## Dataset numbers (`ReportDataset`, 10-01..10-31)

- **KH1 receivable, Branch (main):** opening 4,000 (Dư Nợ). Rows: `PX00001` 10-03 increase 3,000 → 7,000; `PT00001` 10-03 decrease 1,000 → 6,000; `PT00002` 10-05 decrease 4,000 → 2,000; `PN00001` 10-08 decrease 500 → 1,500. Σ increase 3,000, Σ decrease 5,500, closing 1,500 (Dư Nợ).
- **KH1 receivable, Company:** the same plus branch B's `PX00001` 10-03 increase 9,999. Σ increase 12,999, Σ decrease 5,500, closing 11,499; five rows.
- **NCC1 payable, Branch:** opening 0. Rows: `PN00002` 10-10 increase 6,000 → Có 6,000; `PC00001` 10-10 decrease 2,000 → Có 4,000; `PC00002` 10-12 decrease 3,000 → Có 1,000. Closing 1,000 (Dư Có).
- **Summary receivable, Branch:** one row KH1: opening Nợ 4,000, movement Nợ 3,000 / Có 5,500, closing Nợ 1,500. Company: movement Nợ 12,999, closing Nợ 11,499.
- **Summary payable, Branch:** one row NCC1: movement Nợ 5,000 / Có 6,000, closing Có 1,000.

## Tasks

### Task 3.1 — `DebtBalanceSign.SplitMovement`

1. **Write the failing test** in `CashDebt/Unit/DebtBalanceSignTests.cs`: `[Theory] SplitMovement_follows_the_side`: (Receivable, 3_000, 5_500) → (3_000, 5_500); (Payable, 6_000, 5_000) → (5_000, 6_000); (Receivable, 0, 0) → (0, 0).
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~DebtBalanceSignTests"`. Expected: FAIL — build error, `SplitMovement` does not exist.
3. **Write the minimal implementation** in `DebtBalanceSign`.
4. **Run the test to verify it passes:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): debit/credit split of ledger movements"`

### Task 3.2 — `ReasonName` and `DueDate` on ledger rows

1. **Write the failing test** in `CashDebt/Reports/DebtLedgerReportTests.cs`, `Rows_carry_reason_and_due_date` (dataset, KH1 receivable, October):
   - `ReasonName` of the four rows = `[reason of XBH, reason of the automatic receipt, "Thu tiền khách hàng", reason of NTL]`, the three unknown names read through `InDbAsync` (`StockReasons` by code `XBH` / `NTL`; the `CashReason` of `CashVouchers` `ids.AutoReceiptId`);
   - `DueDate` = `[the DueDate of the sale's DebtEntry (read through InDbAsync), null, null, null]`, and that first value is not null;
   - create a debt offset on a partner that is both customer and supplier (as `DebtReconciliationReportTests.Partner_on_both_sides_after_an_offset` does) and assert its ledger row has `ReasonName == "Bù trừ công nợ"`.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~DebtLedgerReportTests"`. Expected: FAIL — build error, `DebtLedgerRowDto.ReasonName` does not exist.
3. **Write the minimal implementation:** the two DTO members and the reason lookup in `BuildDebtLedgerAsync` (rules above); `DueDate = e.DueDate`.
4. **Run the test to verify it passes:** `--filter "FullyQualifiedName~CashDebt.Reports"`. Expected: PASS (the reconciliation tests share the builder).
5. **Commit:** `git commit -m "feat(reports): reason and due date on debt ledger rows"`

### Task 3.3 — `scope` on the three debt reports

1. **Write the failing tests** `CashDebt/Reports/DebtReportScopeTests.cs` (inherits `CashDebtTestBase`, dataset, `MainBranchClient(ids)` which is an admin clone):
   - `Balance_company_scope_adds_every_branch`: `debt-balance?side=Receivable&from=2026-10-01&to=2026-10-31&scope=Company` → one row KH1 with `OpeningDebit` 4,000, `Increase` 12,999, `Decrease` 5,500, `ClosingDebit` 11,499; `Scope == Company`. Without `scope` → the Branch numbers (3,000 / 1,500) and `Scope == Branch`.
   - `Ledger_company_scope_merges_rows`: `debt-ledger?…&partnerId={KH1}&scope=Company` → five rows, two of them with `DocCode == "PX00001"` and `PostedAt == Vn("2026-10-03 09:00")`; `TotalIncrease` 12,999; the last row's `RunningBalance` and `ClosingBalance` 11,499; each row's `RunningBalance` equals the previous one plus `Increase − Decrease`; every row has a `BranchCode`, and the two `PX00001` rows carry the two different branch codes (the main branch's and branch B's, read through `InDbAsync`). The same request without `scope` → every `BranchCode` is null.
   - `Reconciliation_company_scope`: `debt-reconciliation?partnerId={KH1}&from=…&to=…&scope=Company` → `BranchName == null`, the receivable side closes at 11,499; with `scope=Branch` (or none) `BranchName` is the main branch's name and the side closes at 1,500.
   - `Company_scope_needs_access_all`: a client with only `reports.debt` (`CreateClientWithPermissionsAsync("scope_" + suffix, ids.MainBranchId, Permissions.Reports.Debt)`) → 403 on the three routes with `scope=Company`, 200 with `scope=Branch`.
   - `Invalid_scope_is_400`: `scope=7` → 400 on `debt-balance`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtReportScopeTests"`. Expected: FAIL — build error, `DebtBalanceReportDto.Scope` does not exist.
3. **Write the minimal implementation:** `ReportScopeGuard`; the DTO members (`Scope`, `BranchCode`); the scope filter in the three builders; the branch-code lookup; the validator rules; `ICurrentUser` in the service constructor.
4. **Run the tests to verify they pass:** `--filter "FullyQualifiedName~CashDebt.Reports"`. Expected: PASS, including every existing report test (default scope).
5. **Commit:** `git commit -m "feat(reports): branch or company scope for debt reports"`

### Task 3.4 — S12 for one partner

1. **Write the failing tests** `Tt133/DebtLedgerExportTests.cs` (inherits `CashDebtTestBase`, dataset):
   - `S12_receivable_for_one_partner`: `POST /api/reports/debt-ledger/export` with `side=Receivable&from=2026-10-01&to=2026-10-31&partnerIds={KH1}` (see the notation above):
     - 200, xlsx, file name `S12-DNN_20261001_20261031.xlsx`, one worksheet named `ids.Kh1Code` (first 31 characters);
     - cells "Mẫu số S12-DNN", "SỔ CHI TIẾT THANH TOÁN VỚI NGƯỜI MUA", "Loại công nợ: Phải thu khách hàng", `Đối tượng: {Kh1Code} – Đối tượng {Kh1Code}`, "Loại tiền: VNĐ";
     - `Số dư đầu kỳ`: column 9 = 4,000, column 10 empty;
     - four rows with column 2 = `PX00001`, `PT00001`, `PT00002`, `PN00001`; columns 7/8/9/10 = (3,000 / empty / 7,000 / empty), (empty / 1,000 / 6,000 / empty), (empty / 4,000 / 2,000 / empty), (empty / 500 / 1,500 / empty); column 6 of the first row is a date and of the others empty; column 3 of the first row is 2026-10-03;
     - `Cộng số phát sinh`: 3,000 and 5,500; `Số dư cuối kỳ`: column 9 = 1,500;
     - landscape.
   - `S12_payable_puts_increases_in_credit`: `side=Payable&partnerIds={NCC1}` → title "… VỚI NGƯỜI BÁN", "Loại công nợ: Phải trả người bán"; rows `PN00002` (empty / 6,000 / empty / 6,000), `PC00001` (2,000 / empty / empty / 4,000), `PC00002` (3,000 / empty / empty / 1,000); `Số dư cuối kỳ` column 10 = 1,000.
   - `S12_negative_balance_moves_to_the_other_column`: create a second customer `KH3` in the test with only a prepayment opening debt of 800 (`CreateOpeningDebtAsync(client, kh3, DebtSide.Receivable, OpeningDebtKind.Prepayment, "TT-KH3", new DateOnly(2026, 9, 15), 800m)`, the call `ReportDataset` uses for `TT-OLD`); `side=Receivable&from=2026-10-01&to=2026-10-31&partnerIds={KH3}` → `Số dư đầu kỳ` column 9 empty and column 10 = 800 (Dư Có); `Số dư cuối kỳ` the same.
   - `S12_company_scope`: `scope=Company&partnerIds={KH1}` → five rows; `Cộng số phát sinh` 12,999 / 5,500; closing 11,499; the header cell is "Đơn vị: {company name}" without a branch (set the company name through `InDbAsync` first); column 4 of the two `PX00001` rows starts with `[{main branch code}] ` and `[{branch B code}] `. In `S12_receivable_for_one_partner` (Branch scope) no cell of column 4 starts with `[`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtLedgerExportTests"`. Expected: FAIL — 404.
3. **Write the minimal implementation:** `DebtLedgerExportRequest`; `RenderDebtLedgers` and `DebtLedgerForm`; `ExportDebtLedgerAsync` for the explicit `PartnerIds` path only; an **empty** `DebtLedgerExportRequestValidator`; the controller action `[HttpPost("debt-ledger/export")]` with `[FromBody] DebtLedgerExportRequest request, [FromQuery] ExportFormat format`.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(tt133): S12 debt ledger form"`

### Task 3.5 — S12 for several or all partners, validation and limits

1. **Write the failing tests** in `DebtLedgerExportTests.cs`:
   - `S12_all_partners_with_activity`: add a second customer `KH2` with a 700 sale at 10-20 (`ReportDataset.PostStockAsync` with `ids.MainWarehouseId`, paid 0) and a third customer with no entries; `side=Receivable&…&all=true` → two worksheets, ordered by partner code, none for the third customer; each sheet's closing equals that partner's `ClosingDebit` in `GET debt-balance`.
   - `S12_several_partners`: `partnerIds={KH2}&partnerIds={KH1}` → two sheets in code order (not request order); a repeated id yields one sheet.
   - `Validation_permission_and_limits`:
     - neither `partnerIds` nor `all` → 400 key `partnerIds`;
     - 150 distinct random GUIDs pass the validator (the answer is 404 for an unknown partner, not 400);
     - `GET` on the route → 405;
     - `from` after `to` → 400 key `to`;
     - an unknown partner id → 404;
     - `side=Payable&from=2026-10-01&to=2026-10-04&all=true` (no payable activity yet) → 422 `EXPORT_NO_TARGETS`;
     - a `reports.cash`-only client → 403;
     - `scope=Company` with a `reports.debt`-only client → 403.
2. **Run the tests to verify they fail:** same filter. Expected: FAIL — `all=true` answers 422 `EXPORT_NO_TARGETS` (no target path yet), the sheets come in request order, and the empty validator answers 422 or 500 where 400 is expected.
3. **Write the minimal implementation:** the `All` target path, the ordering and de-duplication, the validator rules.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(tt133): S12 bulk export and validation"`

### Task 3.6 — Debt summary form

1. **Write the failing tests** `Tt133/DebtSummaryExportTests.cs` (dataset):
   - `Summary_receivable`: `GET /api/reports/debt-balance/export?side=Receivable&from=2026-10-01&to=2026-10-31` → file name `TongHopCongNo_20261001_20261031.xlsx`; no cell contains "Mẫu số"; title "BẢNG TỔNG HỢP CHI TIẾT THANH TOÁN VỚI NGƯỜI MUA"; one body row: STT 1, `Kh1Code`, columns 4–9 = (4,000 / empty / 3,000 / 5,500 / 1,500 / empty); `Cộng` = (4,000 / 0 / 3,000 / 5,500 / 1,500 / 0); landscape.
   - `Summary_payable`: title "… VỚI NGƯỜI BÁN"; NCC1 row columns 6–9 = (5,000 / 6,000 / empty / 1,000).
   - `Summary_totals_equal_the_balance_report`: for both sides, the six `Cộng` cells equal `Totals` of `GET debt-balance` (movement through `SplitMovement`).
   - `Summary_closing_equals_S12_closing`: KH1's columns 8/9 equal the `Số dư cuối kỳ` cells of its S12 sheet.
   - `Summary_company_scope`: `scope=Company` → KH1 movement Nợ 12,999, closing Nợ 11,499.
   - `Summary_with_no_rows_still_exports`: `side=Payable&from=2026-10-01&to=2026-10-04` → 200 with a `Cộng` row of zeros.
   - `Summary_with_search_prints_the_filter`: `search={Kh1Code}` → the cell `Điều kiện lọc: mã hoặc tên chứa "{Kh1Code}"` and the KH1 row; `search=zzz-none` → 200 with that line and a `Cộng` row of zeros; without `search` (and with `search=%20`) no cell starts with "Điều kiện lọc:".
   - `Summary_rejects_a_partner_filter`: `partnerId={KH1}` → 400 with `details` key `partnerId`.
   - `Permission`: a `reports.cash`-only client → 403.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtSummaryExportTests"`. Expected: FAIL — 404.
3. **Write the minimal implementation:** `RenderDebtSummary`, `DebtSummaryForm`, `ExportDebtSummaryAsync` (validates with the existing `DebtBalanceReportRequest` validator in the controller; the `partnerId` rule and the `search` line of T22), the action `[HttpGet("debt-balance/export")]`.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(tt133): debt summary form"`

### Task 3.7 — Reconciliation record

1. **Write the failing tests** `Tt133/DebtReconciliationExportTests.cs` (dataset; company profile set through `InDbAsync`: `Name "Công ty ABC"`, `Address "12 Lê Lợi"`, `TaxCode "0101234567"`, `LegalRepresentativeName "Nguyễn A"`, `ChiefAccountantName "Trần B"`):
   - `Customer_only_partner`: `GET /api/reports/debt-reconciliation/export?partnerId={KH1}&from=2026-10-01&to=2026-10-31` → file name `DoiChieuCongNo_{Kh1Code}_20261001_20261031.xlsx`; cells "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", "BIÊN BẢN ĐỐI CHIẾU CÔNG NỢ", "Từ ngày 01/10/2026 đến ngày 31/10/2026", "BÊN A: Công ty ABC", `BÊN B: Đối tượng {Kh1Code}`, "Đại diện: Nguyễn A"; one heading "Công nợ phải thu (Bên B nợ Bên A)" and none for payable; four table rows; `Cộng phát sinh` 3,000 / 5,500; `Số dư cuối kỳ` 1,500 in column 5; no cell starts with "Bù trừ:"; the conclusion cell equals "Tính đến ngày 31/10/2026, Bên B còn nợ Bên A số tiền: 1.500 đồng (Bằng chữ: Một nghìn năm trăm đồng)."; cells "ĐẠI DIỆN BÊN A", "ĐẠI DIỆN BÊN B", "Kế toán trưởng", "Trần B"; no cell contains "Mẫu số" or "Sổ này có"; portrait.
   - `Partner_on_both_sides`: the `BOTH` partner of `DebtReconciliationReportTests.Partner_on_both_sides` (sale 1,000 at 10-20, purchase 400 at 10-21) → two headings; "Bù trừ: Bên B còn nợ Bên A 600 đồng"; conclusion "… Bên B còn nợ Bên A số tiền: 600 đồng (Bằng chữ: Sáu trăm đồng)."
   - `We_owe_the_partner`: `partnerId={NCC1}` → heading "Công nợ phải trả (Bên A nợ Bên B)"; conclusion "Tính đến ngày 31/10/2026, Bên A còn nợ Bên B số tiền: 1.000 đồng (Bằng chữ: Một nghìn đồng)."
   - `Nothing_owed`: `partnerId={NCC1}&from=2026-10-01&to=2026-10-04` → conclusion "Tính đến ngày 04/10/2026, hai bên không còn công nợ."
   - `Company_scope`: `partnerId={KH1}&scope=Company` → the two `PX00001` rows have "Diễn giải" cells starting with the two branch codes in brackets; closing 11,499 and "… số tiền: 11.499 đồng (Bằng chữ: Mười một nghìn bốn trăm chín mươi chín đồng)."
   - `Validation_and_permission`: missing `partnerId` → 400 key `partnerId`; unknown partner → 404; `reports.cash`-only client → 403.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtReconciliationExportTests"`. Expected: FAIL — 404.
3. **Write the minimal implementation:** `RenderDebtReconciliation`, `DebtReconciliationForm` (layout above), `ExportDebtReconciliationAsync`, the action `[HttpGet("debt-reconciliation/export")]` using the existing `DebtReconciliationReportRequest` validator.
4. **Run the tests to verify they pass:** `--filter "FullyQualifiedName~Tt133"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(tt133): debt reconciliation record"`

### Task 3.8 — Frontend: scope select and ledger columns

1. **Write the failing tests:**
   - `components/report-scope-select/report-scope-select.test.tsx`: without `branches.access_all` the component renders nothing; with it, a select labelled "Phạm vi" offers "Chi nhánh đang làm việc" and "Toàn công ty" and calls `onChange('Company')`.
   - `pages/cash-reports/debt-ledger-page.test.tsx`: the table shows the headers "Lý do" and "Hạn TT"; a row with `reasonName: 'Bán hàng'` and `dueDate: '2026-10-20'` shows "Bán hàng" and "20/10/2026"; a row with `branchCode: 'HN'` and `description: 'Bán hàng tháng 10'` shows "[HN] Bán hàng tháng 10", and a row without `branchCode` shows its description alone; with `?scope=Company` in the URL and `branches.access_all`, `useDebtLedger` is called with `scope: 'Company'`; without the permission the hook is called with `scope: 'Branch'` whatever the URL says.
   - `debt-balance-page.test.tsx` and `debt-reconciliation-page.test.tsx`: choosing "Toàn công ty" puts `scope=Company` in the URL and in the hook params; the reconciliation page shows "Toàn công ty" in place of the branch name when `branchName` is null.
2. **Run the tests to verify they fail:** `npx vitest run src/components/report-scope-select src/pages/cash-reports`. Expected: FAIL — `report-scope-select` cannot be imported.
3. **Write the minimal implementation:**
   - `lib/report-scope.ts`: `export type ReportScope = 'Branch' | 'Company'` and `export const REPORT_SCOPE_LABELS: Record<ReportScope, string>` ("Chi nhánh đang làm việc", "Toàn công ty"); `scope?: ReportScope` on `DebtBalanceParams`, `DebtLedgerParams`, `DebtReconciliationParams`; `scope: ReportScope` on the three report types; `reasonName?: string | null`, `dueDate?: string | null` and `branchCode?: string | null` on `DebtLedgerRow`; `branchName: string | null` on `DebtReconciliationReport`.
   - `ReportScopeSelect` (`{ id: string; value: ReportScope; onChange: (scope: ReportScope) => void }`), returning null without `branches.access_all`.
   - The three pages keep `scope` in the URL (`updateParams({ scope })`), read it as `'Company'` only when the user holds `branches.access_all`, and pass it to their hook.
   - `DebtLedgerTable`: columns "Lý do" (after "Diễn giải") and "Hạn TT" (`formatDateOnly`), with the `colSpan` values and `min-w` adjusted; "Diễn giải" gets the `[{branchCode}] ` prefix when the row has one (T21). The reconciliation page uses the same table, so it shows the prefix too.
4. **Run the tests to verify they pass:** same command, then `npm run typecheck`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): company scope and ledger reason/due date on debt reports"`

### Task 3.9 — Frontend: export buttons and target dialog

1. **Write the failing tests:**
   - `components/tt133-export/export-targets-dialog.test.tsx`:
     - with `current = { id: 'p1', label: 'KH1 - Minh Long' }` the default choice is "Đang xem: KH1 - Minh Long"; confirming calls `onConfirm({ ids: ['p1'] })`;
     - "Chọn nhiều": items added through the `renderPicker` callback appear as removable chips; the same id is not added twice; confirming calls `onConfirm({ ids: [...] })`; with no item the confirm button is disabled;
     - "Tất cả có phát sinh trong kỳ": confirming calls `onConfirm({ all: true })`;
     - without `current` the first choice is absent and "Chọn nhiều" is selected.
   - `debt-ledger-page.test.tsx` (mock `@/components/tt133-export/tt133-export-button` with a stub exposing `forms` and `onSelect`, mock `@/features/tt133-export/use-tt133-download`): the button has one form `S12-DNN` with `report: 'debt-ledger'`, `params: { side, from, to, scope }`, `fileBase: 'S12-DNN_{from}_{to}'`; selecting PDF opens the dialog with the current partner; confirming "Tất cả" calls `download(form, 'pdf', { all: true })`; confirming the current partner calls `download(form, 'pdf', { partnerIds: [partnerId] })`; the button is enabled without a selected partner (the dialog then starts at "Chọn nhiều"); an invalid range disables it.
   - `debt-balance-page.test.tsx`: one form `TongHopCongNo` with `report: 'debt-balance'`, `params: { side, from, to, search, scope }` (the page's current filters), no `onSelect`.
   - `debt-reconciliation-page.test.tsx`: disabled until a partner is chosen; then one form with `code: 'DoiChieuCongNo_{partnerCode}'`, `report: 'debt-reconciliation'`, `params: { partnerId, from, to, scope }`.
   - each of the three pages: choosing the period preset "Tháng trước" updates `from` / `to`.
2. **Run the tests to verify they fail:** `npx vitest run src/components/tt133-export src/pages/cash-reports`. Expected: FAIL — `export-targets-dialog` cannot be imported.
3. **Write the minimal implementation:**
   - `ExportTargetsDialog` (`components/ui/dialog`): props `{ open: boolean; onOpenChange: (open: boolean) => void; title: string; current?: { id: string; label: string } | null; renderPicker: (add: (item: { id: string; label: string }) => void) => React.ReactNode; onConfirm: (choice: { ids: string[] } | { all: true }) => void }`; confirm button "Xuất".
   - Ledger page: the button with `onSelect`, the dialog (title "Chọn đối tượng xuất sổ S12", `renderPicker` = a `ReportPartnerPicker` of the current side), `useTt133Download`.
   - Balance and reconciliation pages: the button with direct download.
   - `PeriodPresetSelect` on the three pages (it calls `updateParams({ from, to })`).
4. **Run the tests to verify they pass:** same command, then `npm run typecheck`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): TT133 exports on the debt report pages"`

### Task 3.10 — Phase checkpoint

1. Run the full backend suite and the frontend `lint`, `test`, `build`; compare with `BASELINE.md`.
2. If a new failure appears, write a focused failing test, fix, re-run.
3. Commit only if a fix was needed: `git commit -m "fix(tt133): <what>"`.

## Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Tt133|FullyQualifiedName~CashDebt.Reports|FullyQualifiedName~DebtBalanceSignTests"
dotnet test OrderMgmt.sln   # compare with BASELINE.md
cd ../frontend
npm run typecheck && npm run lint && npm run test && npm run build
```

## Exit Criteria

- `scope=Company` adds every branch on the three debt reports and needs `branches.access_all`; the default is unchanged.
- Ledger rows carry `ReasonName` and `DueDate`; the screen shows them. In company scope each row names its branch, on the screen and on S12 and the reconciliation record.
- The debt summary prints its search filter and refuses a single-partner request.
- S12 exports one, several or all partners (one sheet each) with the Nợ/Có rule on both sides, including a negative balance; the summary's totals equal the balance report and its closing equals S12; the reconciliation record prints the right conclusion in digits and words for the four cases.
- 400 / 403 / 404 / 422 behave as specified.
- The three debt pages have the scope select (for holders of `branches.access_all`), the period presets and the export button; the ledger page has the target dialog.
- No new failures against `BASELINE.md`.
