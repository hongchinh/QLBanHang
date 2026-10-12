# Phase 03 — Settlement confirmation token

**Status:** [x] done (2026-10-12)
**Complexity:** L

## Objective

A confirmed `DEBT_SETTLEMENTS_WILL_BE_REMOVED` removes only settlements the user was shown (decisions B1, B2). Today a resend with `confirmRemoveSettlements: true` removes whatever exists at resend time: a settlement a colleague created between the 422 and the confirmation is dropped unseen.

Contract:
- The 422 body gets `error.token`: the listed settlement ids, distinct, sorted, `N` format, comma-separated (e.g. `"0f3c…,9a1b…"`). Other errors have `token: null`.
- Requests that carry `confirmRemoveSettlements` also accept `confirmedSettlements` (string, optional): the token being confirmed.
- The ledger accepts a confirmed removal when the token is absent (unchanged behaviour) or lists **every** settlement it would remove. Otherwise it answers a new 422 with the current list and token.
- Merging two 422s (stock voucher + automatic voucher, D-8) unions the tokens.

## Files

- Backend: `backend/src/OrderMgmt.Domain/Common/DomainException.cs` (`ConfirmationRequiredException.Token`), `backend/src/OrderMgmt.Application/Common/Models/ApiResponse.cs` (`ApiError.Token`), `backend/src/OrderMgmt.WebApi/Middleware/GlobalExceptionMiddleware.cs`, `backend/src/OrderMgmt.Application/CashDebt/Ledger/SettlementRemovalConfirmation.cs`, `CashDebt/Ledger/IDebtLedgerService.cs`, `CashDebt/Ledger/DebtLedgerService.cs`, `CashDebt/AutoVouchers/IAutoCashVoucherService.cs`, `CashDebt/AutoVouchers/AutoCashVoucherService.cs`, `Inventory/StockVouchers/Interfaces/IStockVoucherCashDebt.cs`, `Inventory/StockVouchers/Services/StockVoucherCashDebt.cs`, `Inventory/StockVouchers/Services/StockVoucherService.cs`, `Inventory/StockVouchers/Models/StockVoucherDtos.cs`, `CashDebt/CashVouchers/Models/CashVoucherDtos.cs`, `CashDebt/CashVouchers/Services/CashVoucherService.cs`, `CashDebt/DebtOffsets/Services/DebtOffsetService.cs`, `CashDebt/OpeningDebts/Models/OpeningDebtDtos.cs`, `CashDebt/OpeningDebts/Interfaces/IOpeningDebtService.cs`, `CashDebt/OpeningDebts/Services/OpeningDebtService.cs`, `backend/src/OrderMgmt.WebApi/Controllers/OpeningDebtsController.cs`
- Backend tests: `CashDebt/Unit/SettlementRemovalConfirmationTests.cs`, `CashDebt/DebtSettlementTests.cs`, `CashDebt/StockVoucherDebt/StockVoucherSettlementTests.cs`
- Frontend: `frontend/src/lib/api-client.ts`, `frontend/src/lib/confirm-required.ts` (+ its test), `frontend/src/features/stock-vouchers/types.ts`, `features/cash-vouchers/types.ts`, `features/debt-offsets/types.ts`, `features/opening-debts/{types,schema,api,hooks}.ts`, `frontend/src/pages/debt/debt-offset-form-page.tsx`, `pages/debt/opening-debt-form-dialog.tsx`, `pages/debt/opening-debt-page.tsx`, and their tests

Commands: backend as in Phase 01; frontend from `frontend/`: `npx vitest run <path>`, `npm run typecheck`, `npx eslint --ext .ts,.tsx <files>`.

## Tasks

### Task 3.1 — Token helpers and the 422 body

1. Extend `CashDebt/Unit/SettlementRemovalConfirmationTests.cs` with failing tests:
   - `TokenOf(new[] { b, a, b })` == `$"{a:N},{b:N}"` when `a < b` (distinct, sorted by `Guid` order);
   - `Covers(null, ids)` is true; `Covers(TokenOf(new[]{a,b}), new[]{a})` is true; `Covers(TokenOf(new[]{a}), new[]{a,b})` is false; `Covers("garbage", new[]{a})` is false;
   - `Create(details, new[]{a,b}).Token == TokenOf(new[]{a,b})`;
   - `Merge(x, y).Token` == `TokenOf` of the union of both tokens' ids.
2. Run `--filter "FullyQualifiedName~SettlementRemovalConfirmationTests"` — expected: build errors (CS0117 `TokenOf`/`Covers` missing, CS1501 `Create` overload).
3. Implement:
   - `ConfirmationRequiredException`: add `public string? Token { get; }` and an optional constructor parameter `string? token = null`.
   - `ApiError`: add `public string? Token { get; init; }`; the middleware's `ConfirmationRequiredException` arm sets `Token = cre.Token`.
   - `SettlementRemovalConfirmation`: `TokenOf(IEnumerable<Guid> ids)`, `Covers(string? token, IEnumerable<Guid> ids)` (parse with `Guid.TryParseExact(part, "N", …)`; any unparsable part makes it false), `Create(IDictionary<string,string[]> details, IEnumerable<Guid> settlementIds)` setting the token, and `Merge` unioning the ids of both tokens.
4. Run the unit tests — expected: PASS.
5. Commit `feat(debt): add a settlement token to the removal confirmation`.

### Task 3.2 — The ledger checks the token

1. Add failing tests to `CashDebt/DebtSettlementTests.cs` (ledger level, like `Remove_source_requires_confirmation_when_settled`):
   - `Remove_source_with_a_token_removes_only_listed_settlements`: X (source) settled with Y (400). `RemoveSourceAsync(confirm: false)` throws with `Token == TokenOf([s1])`. Then settle X with Z (300) as `Manual`. `RemoveSourceAsync(confirm: true, confirmedSettlements: firstToken)` throws `DEBT_SETTLEMENTS_WILL_BE_REMOVED` whose details list both Y and Z and whose token covers both; nothing removed. With the new token: removed.
   - `Post_shrink_with_a_covering_token_proceeds`: the same for `PostAsync` with a smaller amount.
2. Run `--filter "FullyQualifiedName~DebtSettlementTests"` — expected: build error (no `confirmedSettlements` parameter).
3. Implement: `IDebtLedgerService.PostAsync` and `RemoveSourceAsync` get a trailing optional `string? confirmedSettlements = null` (before `CancellationToken`). In both, the 422 is thrown when there are pending settlements and `!confirmRemoveSettlements || !SettlementRemovalConfirmation.Covers(confirmedSettlements, pending.Select(s => s.Id))`; the exception is built with the pending ids (token).
4. Run the ledger tests and the area command — expected: PASS.
5. Commit `feat(debt): reject a confirmed removal that would drop an unlisted settlement`.

### Task 3.3 — Requests carry the token

1. Add a failing API test to `CashDebt/StockVoucherDebt/StockVoucherSettlementTests.cs`, `A_settlement_made_after_the_warning_needs_a_new_confirmation`: a sale of 3,000 with a receipt R1 allocated 1,000; cancelling it answers 422 listing R1 with `Error.Token`; a receipt R2 then allocates 500 to the sale; the cancel resent with `ConfirmRemoveSettlements = true, ConfirmedSettlements = token` answers 422 listing R1 and R2; resent with the new token it succeeds. (`ApiError` in the test project is the application's `ApiError`, which now has `Token`.)
2. Run `--filter "FullyQualifiedName~A_settlement_made_after_the_warning"` — expected: build error (`ConfirmedSettlements` missing on `StockVoucherActionRequest`).
3. Implement:
   - Add `public string? ConfirmedSettlements { get; set; }` next to every `ConfirmRemoveSettlements` in `UpsertStockVoucherRequest`, `StockVoucherActionRequest`, `UpsertCashVoucherRequest`, `CashDocumentActionRequest`, `UpsertOpeningDebtRequest`.
   - `OpeningDebtsController.Delete`: add `[FromQuery] string? confirmedSettlements` and pass it; `IOpeningDebtService.DeleteAsync` gets the parameter.
   - Thread the value to every `PostAsync` / `RemoveSourceAsync` call: `CashVoucherService` (create, update, cancel/restore/delete via `RemoveDebtAsync`), `DebtOffsetService.RemoveDebtAsync`, `OpeningDebtService` (update, delete), `IStockVoucherCashDebt.SyncDebtAsync` (new parameter) and `IAutoCashVoucherService.SyncAsync` (new parameter, passed to its own ledger calls). In `SyncDebtAsync`, the held stock 422 is rethrown or merged as today (merge now unions tokens, Task 3.1).
4. Run the new test and the area command — expected: PASS.
5. Commit `feat(debt): accept the confirmed settlement token on every write that can remove settlements`.

### Task 3.4 — Frontend resends the token

1. Write failing tests:
   - `frontend/src/lib/confirm-required.test.ts`: `toConfirmationRequest` of a 422 whose body has `error.token = "t1"` returns `token: "t1"`; `withConfirmed({}, request)` returns `{ confirmRemoveSettlements: true, confirmedSettlements: 't1' }`; for a code other than `DEBT_SETTLEMENTS_WILL_BE_REMOVED` no `confirmedSettlements` is added.
   - `frontend/src/pages/debt/opening-debt-page.test.tsx` (it covers the form dialog too): after a 422 with a token on save, the confirmed resend's payload contains `confirmedSettlements: 't1'`; the same for delete (`openingDebtsApi.remove` called with the token).
   - `frontend/src/pages/debt/debt-offset-form-page.test.tsx`: the confirmed cancel resend body contains `confirmedSettlements`.
2. Run `npx vitest run src/lib/confirm-required.test.ts src/pages/debt` — expected: FAIL (`token` undefined, payloads without the field).
3. Implement:
   - `lib/api-client.ts`: `ApiError` and `ApiErrorShape` get `token?: string | null`; `getApiError` copies `data.error.token` (and `ApiCallError` carries it if it is constructed from an error body).
   - `lib/confirm-required.ts`: `ConfirmationRequest.token?: string`; `ConfirmFlags` becomes `Partial<Record<ConfirmFlag, boolean>> & { confirmedSettlements?: string }`; `withConfirmed` also sets `confirmedSettlements = request.token` when `request.flag === 'confirmRemoveSettlements'` and a token is present.
   - Request types: add `confirmedSettlements?: string` where `confirmRemoveSettlements` exists (`features/stock-vouchers/types.ts` upsert + action request, `features/cash-vouchers/types.ts` (both `Pick<ConfirmFlags, …>` lists get `'confirmedSettlements'`), `features/debt-offsets/types.ts`, `features/opening-debts/types.ts`). Stock voucher and cash voucher forms already spread `flags` into the payload/body, and their DELETE sends the body as query params, so they need no other change.
   - `pages/debt/debt-offset-form-page.tsx` (body built at ≈line 377): add `confirmedSettlements: flags.confirmedSettlements`.
   - Opening debts: `schema.ts` `toUpsertRequest` extra and `api.ts`/`hooks.ts` `remove` take `confirmedSettlements?: string`; `opening-debt-form-dialog.tsx` and `opening-debt-page.tsx` keep the pending `ConfirmationRequest` and pass its token on the confirmed resend.
4. Run `npx vitest run src/lib src/pages/debt src/pages/stock-vouchers src/pages/cash-vouchers src/features`, `npm run typecheck`, eslint on touched files — expected: PASS.
5. Commit `feat(frontend): resend the confirmed settlement token`.

## Verification

- Backend area command and `--filter "FullyQualifiedName~Inventory.StockVouchers"` — all pass.
- `npx vitest run` — only the 3 baseline failures; `npm run typecheck` clean.

## Exit Criteria

- A confirmed resend never removes a settlement that was not in the 422 the user confirmed (when the client sends the token, which the app always does).
- Requests without the token behave exactly as before.
