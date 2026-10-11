# Cash & Debt Round 2A — Receipts/Payments, Funds, Receivables/Payables (Đợt 2A — Thu/Chi, Quỹ & Công nợ)

> Created: 2026-10-07 23:14:51 · Source design: [brainstorm SUMMARY](../../../brainstorms/261007-2252-cash-debt-round2/SUMMARY.md), [section-01 decisions](../../../brainstorms/261007-2252-cash-debt-round2/section-01-decisions.md), [section-02 design](../../../brainstorms/261007-2252-cash-debt-round2/section-02-design.md), [section-03 delivery](../../../brainstorms/261007-2252-cash-debt-round2/section-03-delivery.md), [section-04 review decisions](../../../brainstorms/261007-2252-cash-debt-round2/section-04-review-decisions.md) · Builds on: [Round 1 plan](../261006-2259-inventory-round1/SUMMARY.md)

## Goal

Deliver Round 2A of the legacy `ChungTu.vb` / `SoDuCongNoMuaBan.vb` replacement. Track money, without a chart of accounts, following MISA's business model:

- **Receipts and payments:** header + lines, quotation-style list and form.
- **Catalogs:** cash reasons with a `DebtEffect` flag; funds per branch (cash and any number of bank accounts) with opening balances; fund transfers.
- **Debt ledger:** receivables and payables kept per document (`DebtEntry` + `DebtSettlement`). It is fed by:
  - stock vouchers, through `StockReason.DebtEffect`, including customer returns and returns to suppliers;
  - manual receipts and payments;
  - automatic receipts/payments that a stock voucher manages from its paid amount;
  - opening debts per legacy document;
  - debt offset vouchers.
- **Matching:** at voucher time, on a manual matching screen, or by debt offset.
- **Credit limits** per customer × branch, with a configurable Allow / Warn / Block policy.
- **Negative-fund policy**, also Allow / Warn / Block.
- **Payment terms:** `Customer.CreditDays` and `StockVoucher.DueDate`.
- **Reports:** Số dư công nợ, Sổ chi tiết công nợ and Sổ quỹ.

Everything ships behind new permissions and is invisible to users who don't have them.

## Scope

**In scope**

- Backend:
  - permissions, seeds and role grants;
  - `CashDebtSettings`;
  - numbering for `Receipt`, `Payment`, `FundTransfer`, `DebtOffset`;
  - `Branch.OpeningDate`;
  - catalogs: `CashReason`, `Fund`, `PaymentMethod.FundKind`, `StockReason.DebtEffect` (+ seeds `NTL`/`XTL`), `Customer.CreditDays`, `CreditLimit`;
  - pure calculators;
  - `DebtEntry` / `DebtSettlement` ledger, partner/fund advisory locks, credit-limit and negative-fund guards;
  - opening debts and fund opening balance;
  - cash voucher API (with allocation at voucher time);
  - fund transfer API;
  - stock-voucher integration (debt posting, the paid amount ↔ fund rule, automatic vouchers, `DueDate`/`FundId`, credit limit, partner summary);
  - open items, manual matching and unmatching, debt offset vouchers;
  - reports `debt-balance`, `debt-ledger`, `cash-book`;
  - invariant suite.
- Frontend:
  - permissions, role matrix module, the **"Tiền & Công nợ"** sidebar group and routes;
  - catalog screens: cash reasons, funds (with opening balance), credit limits; `FundKind` on payment methods, `DebtEffect` on stock reasons, `CreditDays` on customers/suppliers;
  - settings: cash policies, numbering for the new types, branch opening date;
  - receipt and payment list and form (allocation grid);
  - fund transfer list and form;
  - stock voucher form changes: fund, due date, customer debt panel, generic confirmation dialog;
  - matching screen, debt offset list and form, opening debts screen;
  - the three reports.
- Docs: `product-goals.md`, `system-architecture.md`, `directory-structure.md`, `conventions.md`, `docs/SUMMARY.md`.

**Out of scope**

- **Round 2B** (separate plan; it needs no schema change from 2A): reports Công nợ theo chứng từ, Tuổi nợ, Nợ quá hạn, Công nợ theo nhân viên, Biên bản đối chiếu, and the overdue warning list. 2A already stores `DueDate`, `OwnerUserId` and settlements, and the stock-out panel already shows the overdue amount.
- **Round 3:** print/Excel templates for receipts, payments and reconciliation; Excel import of opening debt; cut-over.
- **Non-goals** (brainstorm):
  - chart of accounts and journal entries;
  - employee partners and advances;
  - a "not posted" state;
  - debt per product;
  - foreign currency;
  - bank sync;
  - backfilling existing test stock vouchers (reset test data instead).

## Decisions made while planning (in addition to the brainstorm)

| # | Topic | Decision |
|---|---|---|
| P1 | Split (user decision 2026-10-07) | This plan is **2A** = brainstorm section-03 phases 1–5 + matching UI + reports 1 (Số dư công nợ), 2 (Sổ chi tiết công nợ) and 8 (Sổ quỹ). 2B (reports 3–7) gets its own plan. |
| P2 | Employee on debt (user decision 2026-10-07) | `DebtEntry.OwnerUserId` = `OwnerUserId` of the source voucher (opening debt: the optional `OwnerUserId` typed on the row). There is no `SalespersonId`. If the BA later answers otherwise, 2B adds it and changes only what is written to `DebtEntry.OwnerUserId`. |
| P3 | Paid amount back to 0 (user decision 2026-10-07) | The automatic receipt/payment is **soft-deleted** and its number is never reused (accepted gap). An activity on the stock voucher records it. If the paid amount becomes > 0 again, a new automatic voucher with a new number is created. |
| P4 | Numbering (user decision 2026-10-07) | One counter per type × branch: `Receipt` (`PT`), `Payment` (`PC`), `FundTransfer` (`CQ`), `DebtOffset` (`BT`), pattern `{KH}{STT}`, length 5, reset `None`. The fund kind does not change the series. |
| P5 | Default role grants (user decision 2026-10-07) | **ADMIN, MANAGER:** every new code. **ACCOUNTANT:** every new code (receipts, payments, fund transfers, offsets, matching, opening debt/cash, cash catalogs, credit limits, cash settings, `debt.partner_summary`, `reports.cash`; it already has `reports.debt`). **SALES:** only `debt.partner_summary`. **WAREHOUSE:** nothing new; automatic vouchers follow the stock voucher permissions. Codes are granted once per code (Round 1 D23 mechanism), so later admin removals survive restarts. |
| P6 | New permission `debt.partner_summary` | Brainstorm §9 left "can SALES see the panel" open. A dedicated code ("Xem công nợ khách trên phiếu xuất") gates `GET /api/debt/partner-summary` and the stock-out panel. `reports.debt` also grants it. |
| P7 | Settings and their permission | New singleton `CashDebtSettings` (Id = 1) with `CreditLimitPolicy` and `NegativeFundPolicy`, enum `LimitPolicy { Allow = 0, Warn = 1, Block = 2 }`, both default **Warn** (pending BA). It has its own permission `cash.settings`; `inventory.settings` is not reused. Numbering of the four new types is edited with `cash.settings`; numbering of `StockIn`/`StockOut` still needs `inventory.settings`. |
| P8 | Branch opening date | New `Branch.OpeningDate` (`DateOnly?`). The migration backfills it per branch from `MIN(opening_stocks.opening_date)` of non-deleted rows. `PUT /api/branches/{id}/opening-date` (`branches.manage`) works only while the branch has **no documents** (non-deleted stock vouchers, cash vouchers, fund transfers or debt offsets: 409 `BRANCH_HAS_DOCUMENTS`), and both the old and new dates must be after `LockedUntil`. A change re-posts the opening stock of every warehouse of the branch and moves the `PostedAt` of opening `DebtEntry` rows (and the `EffectiveAt` of their settlements). Opening stock, opening debt and fund opening balance all use this date. The `OpeningDate` field leaves `SaveOpeningStockRequest`. Saving any of them while the branch has no opening date returns 400 `OPENING_DATE_NOT_SET`. `OpeningStock.OpeningDate` stays as a column, always equal to the branch date. |
| P9 | Paid amount default and fund | `PaidAmount = request.PaidAmount ?? (FundKind(paymentMethod) != None ? Total : 0)`. `PaidAmount > 0` requires a payment method with `FundKind ≠ None` (400 `paymentMethodId`). `StockVoucher.FundId` defaults to the branch's default fund of that kind. If set, it must be active, in the working branch and of the payment method's kind (400 `fundId`). A missing default fund also gives 400 `fundId`. `PaidAmount = 0` stores `FundId = null`. Seeded payment method `CK` migrates to `FundKind = Bank` (the brainstorm said "others → None"; `CK` is our own seed, so this saves a manual step before go-live). |
| P10 | One confirmation exception | New `ConfirmationRequiredException : DomainException` (`Code`, `Message`, `Details: IDictionary<string,string[]>`, `CanConfirm`) maps to **422**, like `NegativeStockException` (which stays unchanged). Codes follow Round 1's `_WARNING`/`_BLOCKED` naming, not the brainstorm's single names: `DEBT_SETTLEMENTS_WILL_BE_REMOVED` (always confirmable), `CREDIT_LIMIT_WARNING` / `CREDIT_LIMIT_BLOCKED`, `NEGATIVE_FUND_WARNING` / `NEGATIVE_FUND_BLOCKED`. Request flags: `confirmRemoveSettlements`, `acknowledgeCreditLimit`, `acknowledgeNegativeFund` (Round 1 uses `acknowledge…`). When several apply, the server reports them in this order: negative stock → settlements removal → credit limit → negative fund. The client keeps the flags it already confirmed and resends. |
| P11 | Blocked debt edits | Changing the partner or side of a `DebtEntry` that still has settlements, or removing that side on save (`DebtEffect` becomes `None`), returns 409 `DEBT_HAS_SETTLEMENTS` (`ConflictException` with a code). Removing an entry on cancel or delete instead asks `DEBT_SETTLEMENTS_WILL_BE_REMOVED`. |
| P12 | Module layout | Entities in `Domain/Entities/CashDebt/`; enums in `Domain/Enums/CashDebtEnums.cs`; use cases in `Application/CashDebt/<Feature>/{Interfaces,Models,Services,Validators}`; shared pieces in `Application/CashDebt/{Common,Interfaces,Ledger,Guards}`; EF configuration in `Infrastructure/Persistence/Configurations/CashDebtConfiguration.cs`; Postgres port in `Infrastructure/CashDebt/PostgresCashDebtLock.cs`. Tests: `tests/OrderMgmt.IntegrationTests/CashDebt/` (+ `Unit/`), base class `CashDebtTestBase : InventoryTestBase`. |
| P13 | Document status, activities | `DocumentStatus { Active = 1, Cancelled = 9 }` for `CashVoucher`, `FundTransfer` and `DebtOffsetVoucher`. One activity table `CashDocumentActivity` (`DocumentType`, `DocumentId`, `Action`, `ActorUserId`, `OccurredAt`, `Description`, `MetadataJson`) for all four new document types. `CashDocumentActivityAction { Created = 1, Updated = 2, Cancelled = 3, Restored = 4, Deleted = 5, SettlementRemoved = 6 }`. `StockVoucherActivityAction` gains `SettlementRemoved = 6` and `AutoVoucherChanged = 7`. |
| P14 | Locks | New port `ICashDebtLock.AcquireAsync(Guid branchId, IEnumerable<Guid> partnerIds, IEnumerable<Guid> fundIds, ct)`. It takes partner keys `debt-partner:{branchId:N}:{partnerId:N}`, then fund keys `cash-fund:{fundId:N}`, each set in one statement ordered `COLLATE "C"` (Round 1 pattern), via a shared helper `AdvisoryLocks.AcquireAsync` extracted from `PostgresInventoryLock`. Every cash/debt write first takes the shared branch gate (`IInventoryLock.AcquireBranchGateAsync(new[]{branchId}, exclusive: false)`), so it serialises with `SetLockAsync` and with the opening-date change. Global order: branch gate → product keys → partner keys → fund keys → counter row. Settings, `LockedUntil`, `OpeningDate`, credit limits and fund data are read **after** the locks. |
| P15 | Period lock helper | New static `PeriodLockGuard.EnsureNotLocked(DateOnly? lockedUntil, IEnumerable<DateTimeOffset> instants)` and an overload taking `DateOnly`s, in `Application/Organization/Branches/PeriodLockGuard.cs`. They throw the same `DomainException("PERIOD_LOCKED", …)` as Round 1. New code uses the helper; Round 1's private copies are not refactored. Settlements are checked on `EffectiveAt`. |
| P16 | `DebtEntry` extra fields | Beyond the brainstorm: `Description` (snapshot, max 500, shown in the ledger report and open-item grids) and `DocDate` (`DateOnly`, VN date of the source). It is updated in place on every save, like the other fields. |
| P17 | Fund running balance | Movements of a fund: `OpeningAmount` first, then Active, non-deleted receipts (+Total) and payments (−Total) with that `FundId`, plus fund transfers (−Amount on `FromFundId`, +Amount on `ToFundId`). Ordered by `VoucherAt`, then inflows before outflows, then code. The negative check follows Round 1 D31: it flags only when the new minimum from the affected instant is < 0 **and** it is lower than before, or the first negative point moved earlier. |
| P18 | Funds | `Fund.Code` is unique per branch. Exactly one default fund per (branch, kind) when the branch has any fund of that kind: the first fund of a kind becomes the default, and setting `IsDefault` clears the others in the same transaction. A default fund cannot be deactivated (400 `isActive`). A fund that is referenced (stock voucher, cash voucher, fund transfer) or has `OpeningAmount ≠ 0` cannot be deleted or change `Kind` (409). The seed and `BranchService.CreateAsync` create cash fund `QTM` "Quỹ tiền mặt" (default) for every branch. |
| P19 | Reason rules | `DebtEffectRules`: a receipt allows `None` / `ReceivableDecrease` / `PayableIncrease`; a payment allows `None` / `PayableDecrease` / `ReceivableIncrease`; stock-in allows `None` / `PayableIncrease` / `ReceivableDecrease`; stock-out allows `None` / `ReceivableIncrease` / `PayableDecrease`. `Receivable*` needs `PartnerType` Customer or Any, `Payable*` needs Supplier or Any, and `None` allows any `PartnerType`. With `DebtEffect ≠ None` the partner is required and must have the role of the side (`IsCustomer` for Receivable, `IsSupplier` for Payable). A reason used by a non-deleted voucher keeps `Direction`, `PartnerType` and `DebtEffect` (400 with the field key, like Round 1). System reasons cannot be deleted and keep `Direction`/`PartnerType`; their `DebtEffect` is locked only when used. `IsAuto` reasons are hidden unless `includeAuto=true` and cannot be chosen on a manual voucher (400 `reasonId`). |
| P20 | Automatic vouchers | A stock-out creates a **Receipt** and a stock-in creates a **Payment**, with reason `TPX` / `CPN`. Contents: one line "Thu tiền theo phiếu xuất {code}" / "Chi tiền theo phiếu nhập {code}"; `PersonName = HandlerName`; partner and its snapshot, `VoucherAt`, branch, `FundId` and `OwnerUserId` from the stock voucher. Its debt entry has the stock entry's side and the opposite direction, but only when the stock reason has `DebtEffect ≠ None`. When `DebtEffect = None` the automatic voucher still exists (money moved) and has no debt entry. The `Auto` settlement amount = `min(PaidAmount, stock entry open excluding Auto, auto entry open excluding Auto)`. Automatic vouchers keep their code when the stock voucher's date moves. |
| P21 | Ledger API shape | `IDebtLedgerService.PostAsync` (save: upsert entries in place) and `RemoveSourceAsync` (cancel/delete). Callers remove their own `AtVoucher` (cash voucher edit) or `Auto` (stock voucher save) settlements first, with `RemoveOwnSettlementsAsync`. That removal needs no confirmation and writes no activity. Every remaining settlement is then subject to P10/P11. |
| P22 | Unmatching origins | `Manual` and `AtVoucher` settlements can be removed on the matching screen. `Auto` settlements are managed by the stock voucher, and `Offset` settlements by cancelling the offset voucher (409 `SETTLEMENT_MANAGED`). |
| P23 | Debt offset lifecycle | Create, view, cancel and delete; there is no edit and no restore (the brainstorm lists no `debt_offset.edit`). Cancel or delete removes its two entries and its `Offset` settlements without confirmation, because those settlements belong to the voucher itself. |
| P24 | Balance sign in reports | Balance = Σ Increase − Σ Decrease. **Receivable:** a positive balance shows as **Dư Nợ**, a negative one as **Dư Có**. **Payable:** a positive balance shows as **Dư Có**, a negative one as **Dư Nợ** (MISA convention). |
| P25 | Cash book with several funds | Each fund group always shows its transfer rows, so its running balance reconciles. In the grand total, a transfer whose two funds are both in the selection is left out, because the two halves cancel. A transfer with only one fund in the selection counts. |
| P26 | Open items for editing | `GET /api/debt/open-items` takes `forSourceType`/`forSourceId`. The open amount then adds back the settlements between each entry and that source's own entry, so the edit grid shows the room the voucher already uses. |
| P27 | Due date default | `StockVoucher.DueDate` (`DateOnly?`). If the request sends `null` while the reason has `DebtEffect = ReceivableIncrease` or `PayableIncrease` and the partner has `CreditDays`, the server stores `VN date + CreditDays`. The form pre-fills the same value and can change it. `DebtEntry.DueDate` = `DueDate ?? DocDate` on Increase rows and null on Decrease rows. |
| P28 | Cash query root | Frontend branch-scoped cash/debt queries live under the root key `['cash']`. Stock voucher mutations invalidate both `['inventory']` and `['cash']`, and cash mutations invalidate `['cash']` and `['inventory']` (stock voucher lists show settlement-driven state). No cash path is added to `lib/sw-routes.ts`. |
| P29 | Opening entries in reports | In report windows, entries with `SourceType = Opening` (posted at 00:00 of the branch opening date) and fund `OpeningAmount` always count as **opening balance**, never as period movements, even when the report starts on the opening date. |

Round 1 rules still apply unchanged: D25 (test database), D26 (test depth), D27 (UTC instants), D28 (bool sentinel), D29 (always mark the header modified), D30/D31 (locks, before/after comparison), D34 (task text wins on implementation detail; the brainstorm wins on business rules unless a P-entry says otherwise).

## Assumptions

- The brainstorm (including section-04 review decisions) is the approved spec. P1–P29 are additions or deliberate refinements.
- The accountant has not yet approved the seeded cash reasons and the stock-reason `DebtEffect` values (section-02 §1). They are seeds, so changing them later is a data or migration task.
- No production data exists yet (go-live after Round 3). Existing **test** stock vouchers are not backfilled. Reset the dev database after deploying 2A, because saving an old test voucher with `PaidAmount > 0` and a method without a fund will be rejected.
- Same local setup as Round 1: PostgreSQL on `localhost:5432` (`postgres`/`1`), `TEST_DB_CONNECTION` pointing at `qldonhang_integtest`, `dotnet-ef` 9.0.15. Migrations go to `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations`. The model snapshot stays where it is (`Infrastructure/Migrations/AppDbContextModelSnapshot.cs`), and `dotnet ef` updates it in place.
- Data volume is small (tens of thousands of debt rows a year). Balances and fund running totals are computed from the ledger and voucher tables in memory per partner or fund, without snapshot tables.

## Risks

- **Large stock-voucher transaction** (stock + debt + fund + automatic voucher). Mitigations: fixed lock order (P14); a lock-count test in Phase 07; the automatic-voucher logic lives in its own service (`IAutoCashVoucherService`).
- **Lost settlements** when entries are rewritten. Mitigation: entries are updated in place under unique `(SourceType, SourceId, Side)`; the invariant suite runs after every scenario (Phase 04 `AssertCashDebtInvariantsAsync`, Phase 09 replay test).
- **No legacy parity:** the legacy SQL (`GetSoDuConNo`, `SoTongHopCongNo`, `SoQuyTienMat`) is not in the repo. Mitigation: hand-computed report tests, plus a manual one-month reconciliation before go-live (gate below).
- **Confirmation chains:** one save can need up to four confirmations. The frontend helper accumulates flags and is unit-tested (Phase 11).
- **Stock voucher form complexity** grows (fund, due date, panel, dialogs). Mitigation: new UI pieces are separate components with their own tests.
- **Phase sizes:** Phases 04, 05, 07 and 11 are XL. Every task inside them is still small and committed separately.
- **Baseline failures:** Round 1 recorded 19 backend and 3 frontend failures. Phase 00 re-records the baseline before any change.

## Phases

- [x] Phase 00 — Baseline commit, branch and failure baseline (S) — `phase-00-baseline.md`
- [x] Phase 01 — Permissions, cash settings, numbering & branch opening date (L) — `phase-01-permissions-settings-opening-date.md`
- [x] Phase 02 — Catalogs: cash reasons, funds, fund kind, reason debt effect, credit days & limits (L) — `phase-02-catalogs.md`
- [x] Phase 03 — Pure calculators (M) — `phase-03-pure-calculators.md`
- [x] Phase 04 — Debt ledger, locks, guards, opening debts & fund opening balance (XL) — `phase-04-debt-ledger-and-guards.md`
- [x] Phase 05 — Cash voucher API (XL) — `phase-05-cash-voucher-api.md`
- [x] Phase 06 — Fund transfer API (M) — `phase-06-fund-transfer-api.md`
- [x] Phase 07 — Stock voucher integration & automatic vouchers (XL) — `phase-07-stock-voucher-integration.md`
- [x] Phase 08 — Matching & debt offset API (L) — `phase-08-matching-and-offset-api.md`
- [x] Phase 09 — Reports API & invariant replay (L) — `phase-09-reports-api.md`
- [x] Phase 10 — Frontend foundations, catalogs & settings (L) — `phase-10-frontend-foundations-catalogs.md`
- [x] Phase 11 — Frontend cash vouchers, fund transfers & stock voucher form (XL) — `phase-11-frontend-vouchers.md`
- [x] Phase 12 — Frontend matching, offset, opening balances & reports (L) — `phase-12-frontend-matching-reports.md`
- [x] Phase 13 — Documentation & final verification (S) — `phase-13-docs-and-verification.md`

Phases run in order. Phases 10–12 depend on the API contracts of Phases 01–09 and must not start before Phase 09 is complete.

## Executor conventions

- **Branch and commits:**
  - Work on branch `feat/cash-debt-round2a` (or the worktree the executing skill creates), after Phase 00 Task 0.1 has committed the docs on `main`.
  - Commit after every task, using conventional commits with scope `cash`, `debt`, `inventory` or `frontend` (e.g. `feat(debt): …`).
- **Staging:** stage files explicitly (`git add <paths>`). Never use `git add -A` / `git add .`: `source/` (legacy VB.NET) is untracked on purpose and must never be committed.
- **Shell:** the commands are written for Git Bash (the Bash tool). Windows PowerShell 5.1 has no `&&`.
- **Test database** (D25) — set before any backend test command:
  - bash: `export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"`
  - PowerShell: `$env:TEST_DB_CONNECTION = "Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"`
  - Never point it at `qldonhang_test` or `qldonhang` (dev databases).
- **Backend commands** (from `backend/`):
  - Build: `dotnet build OrderMgmt.sln`. If a running dev server locks `bin`, add `-o "$TEMP/qlbh-build"`.
  - Targeted tests: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~<ClassName>"`
  - Pure unit tests: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt.Unit"`
  - Full suite: `dotnet test OrderMgmt.sln`
  - Migration: `dotnet ef migrations add <Name> --project src/OrderMgmt.Infrastructure --startup-project src/OrderMgmt.WebApi -o Persistence/Migrations`
- **Frontend commands** (from `frontend/`): `npx vitest run <path>`, `npm run typecheck`, `npm run lint`, `npm run test`, `npm run build`. Every frontend phase ends with `npm run build`.
- **Test depth** (D26):
  - Full coverage for the calculators, ledger, settlements, automatic vouchers, guards, locks, invariants and reports (backend), and for the confirmation helper, allocation grid, matching screen and voucher forms (frontend).
  - Catalog CRUD, settings and simple screens get **smoke-level** tests: one happy path plus permission or business-rule guards where a rule exists.
- **Test locations:**
  - Integration tests: `backend/tests/OrderMgmt.IntegrationTests/CashDebt/`, annotated `[Collection(nameof(PostgresCollection))]`. Stock-voucher integration tests go in `CashDebt/StockVoucherDebt/`.
  - Pure unit tests: `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Unit/` (no collection, no DB).
  - Frontend tests sit next to the file under test.
- **Shared test base:**
  - `CashDebtTestBase : InventoryTestBase` (created in Phase 01 Task 1.1, extended later). It reuses `InDbAsync`, `CreateClientWithPermissionsAsync`, `CloneAdminClient`, `UseBranch`, `CreateBranchAsync`, `CreatePartnerAsync`, `Vn(...)`, `CreateVoucherAsync`, `ReasonIdAsync`, `LineRequest`, `SetPeriodLockAsync`.
  - Logins are rate-limited: reuse `CloneAdminClient()`.
- **Notation:** dates are written `10-05` (= 2026-10-05) unless a year is given. `R+ 1,000` = Receivable Increase of 1,000; `R−`, `P+`, `P−` likewise.
- **Never trust totals from the client:** cash voucher `Total` = Σ lines, offset amount = Σ each side, all computed on the server.
- **Delete style:**
  - `CashReason`, `Fund`, `CreditLimit`, `CashVoucher`, `CashVoucherLine`, `FundTransfer`, `DebtOffsetVoucher` and `OpeningDebt` inherit `BaseEntity` and soft-delete.
  - `DebtEntry`, `DebtSettlement`, `CashDocumentActivity` and `CashDebtSettings` do not inherit `BaseEntity`. Debt rows are hard-deleted. Activity rows are append-only, so they have an `Id` but no soft-delete.
- **Round 1 rules to repeat:**
  - Every `DateTimeOffset` sent to EF is UTC (`VnTime.ToUtc`, D27). Every non-nullable bool with a `true` default gets `.HasSentinel(true)` (D28).
  - Add child rows through their `DbSet.Add`.
  - Validation `details` keys are camelCase paths (`lines[0].amount`, `allocations[1].amount`).
  - DTOs use public auto-properties.
  - List results inherit `PagedResult<T>`.
  - Write paths mark the header modified so `xmin` is checked (D29).
  - Happy-path tasks must not implement the validation rules of a later task, or that task's RED step cannot fail.

## Go-live gates (tracked here, not implemented by this plan)

| Gate | Owner | When |
|---|---|---|
| Accountant approves the seeded cash reasons and every `DebtEffect` (cash and stock reasons, incl. `NTL`/`XTL`) — section-02 §1 | PO + accountant | Before go-live (seed/data change only) |
| Accountant accepts the number gap of deleted automatic vouchers (P3) and one series per type (P4) | PO + accountant | Before go-live |
| BA confirms the defaults `CreditLimitPolicy` / `NegativeFundPolicy` = Warn (P7) and SALES access to the debt panel (P5/P6) | BA | Before go-live |
| BA: cross-branch payment, bank accounts shared by several branches (brainstorm open questions) | BA | Before Round 2B planning |
| Fetch the legacy `GetSoDuConNo`, `SoTongHopCongNo`, `SoQuyTienMat` definitions from production | Dev | Before reconciliation |
| Manual reconciliation of one month of debt balances and the cash book against the legacy software | Dev + accountant | Before go-live |
| Decide manual vs Excel import of opening debt (Round 3) | PO | Before Round 3 planning |
| Reset the dev/test database after deploying 2A (no backfill) | Dev | At deploy |

## Final Verification

Run after all phases:

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test OrderMgmt.sln

cd ../frontend
npm run typecheck
npm run lint
npm run test
npm run build
```

Only the failures recorded in Phase 00 Task 0.2 may remain, compared test by test.

Manual smoke test (dev stack: `docker compose up -d`, backend `dotnet run --project src/OrderMgmt.WebApi`, frontend `npm run dev`, login `admin` / `Admin@123`, database reset first):

1. Settings → Chi nhánh: set the CN01 opening date to the first day of the current month. Danh mục → Quỹ: `QTM` exists and is the default; add bank fund `NH01` (default bank) with an opening balance of 10,000,000 (Số dư đầu kỳ).
2. Danh mục → HTTT: `TM` = Tiền mặt, `CK` = Ngân hàng. Lý do nhập xuất: `XBH` shows "Tăng phải thu"; `NTL`/`XTL` exist.
3. Tiền & Công nợ → Nợ đầu kỳ: customer KH1, receivable debt `HD-OLD-1` dated last month, 5,000,000, due last week.
4. Phiếu xuất kho: KH1, reason XBH, total 3,000,000, HTTT `TM`, paid 1,000,000. The panel shows balance 5,000,000 and overdue 5,000,000. Save: the voucher shows its automatic receipt `PT00001` (read-only in Phiếu thu, with a link back).
5. Phiếu thu: new, reason "Thu tiền khách hàng", KH1, fund `QTM`, one line 4,000,000. The allocation grid suggests `HD-OLD-1` 4,000,000 first (earliest due date). Save.
6. Báo cáo → Số dư công nợ (phải thu, this month): KH1 opening 5,000,000 Dư Nợ, increase 3,000,000, decrease 5,000,000, closing 3,000,000 Dư Nợ. Click the row → Sổ chi tiết shows 3 movements with a running balance.
7. Đối trừ chứng từ: KH1 / phải thu. The left side has no open decrease; the right side shows `HD-OLD-1` open 1,000,000 and the sale open 2,000,000.
8. Settings → Cấu hình tiền & công nợ: negative fund = Block. Phiếu chi from `QTM` for 100,000,000 → blocked dialog, nothing saved.
9. Chuyển quỹ `NH01` → `QTM` 2,000,000. Sổ quỹ (Tất cả): both groups show the transfer, and the grand total leaves it out.
10. Edit the stock-out paid amount to 0 → `PT00001` disappears from the Phiếu thu list, and the stock voucher history records it.
11. Log in as SALES: no "Tiền & Công nợ" group; the stock-out panel is visible. Log in as WAREHOUSE: no group, no panel; saving a stock-out with a paid amount still creates the automatic receipt.
12. Settings → Phân quyền: the "Tiền & Công nợ" group is listed and toggles.

## Rollback / Recovery

- **Code:** every task is a separate commit on `feat/cash-debt-round2a`; revert the branch or individual commits with `git revert`.
- **Database:**
  - The new migrations are `AddCashDebtSettings`, `AddBranchOpeningDate`, `AddCashReasons`, `AddFunds`, `AddPaymentMethodFundKind`, `AddStockReasonDebtEffect`, `AddCustomerCreditDaysAndCreditLimits`, `AddDebtLedger`, `AddOpeningDebts`, `AddCashVouchers`, `AddFundTransfers`, `AddStockVoucherDebtFields`, `AddDebtOffsets`, plus `AddFundDefaultUniqueIndex` (added during execution, see EXECUTION-REPORT D-1).
  - To roll back, run `dotnet ef database update AddStockVouchersAndLedger --project src/OrderMgmt.Infrastructure --startup-project src/OrderMgmt.WebApi`. This drops every cash/debt table and column.
  - `PaymentMethod.IsCash` is restored by the `Down` of `AddPaymentMethodFundKind` (`FundKind = 1` → `true`).
  - There is no production data yet, so the rollback is lossless for quotation and inventory data.
- **Permissions:** new rows are harmless if code is rolled back. To remove them (role assignments first; `\_` escapes the LIKE wildcard):
  ```sql
  DELETE FROM role_permissions WHERE permission_id IN (SELECT id FROM permissions WHERE code LIKE 'receipts.%' OR code LIKE 'payments.%' OR code LIKE 'fund\_transfers.%' OR code LIKE 'debt\_offset.%' OR code LIKE 'debt.%' OR code LIKE 'cash.%' OR code IN ('credit_limit.manage','reports.cash'));
  DELETE FROM permissions WHERE code LIKE 'receipts.%' OR code LIKE 'payments.%' OR code LIKE 'fund\_transfers.%' OR code LIKE 'debt\_offset.%' OR code LIKE 'debt.%' OR code LIKE 'cash.%' OR code IN ('credit_limit.manage','reports.cash');
  ```
- **Exposure:** every deploy needs one `DbSeeder` run (`Database__AutoMigrateAndSeed=true`). If 2A reaches production before go-live, an admin revokes the new permissions from ACCOUNTANT, MANAGER and SALES in Settings → Phân quyền right after the deploy; the seeder never re-grants a code. Grant them again at go-live.
