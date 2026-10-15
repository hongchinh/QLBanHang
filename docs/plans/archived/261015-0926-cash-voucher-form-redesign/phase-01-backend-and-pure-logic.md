# Phase 01 — Backend `activeOnly` and seed grants; pure logic

**Status:** [x] done (2026-10-15; see EXECUTION-REPORT.md)
**Complexity:** M

## Objective

Land everything the later phases stand on that has no screen: the `activeOnly` parameter on both partner search endpoints, the seed grants that let Accountant and Warehouse users quick add, and four small pure frontend modules (reason match, reason notice hook, auto text, Enter focus) with unit tests.

Tasks 1.1–1.3 (backend, files under `backend/`) and Tasks 1.4–1.7 (frontend, files under `frontend/src`) touch disjoint files and may run in two worktrees (see "Two-worktree split" below). No screen changes in this phase. The only visible change is the seed grants: on a **fresh** database the Accountant and Warehouse roles can now create partners (and the Accountant can view suppliers).

### Two-worktree split (optional)

- Frontend half (Tasks 1.4–1.7): in `$WT` on `feat/cash-voucher-form-redesign`.
- Backend half (Tasks 1.1–1.3): `git -C "$WT" worktree add /D/Projects/QLBanHang/.claude/worktrees/cvf-p01-backend -b feat/cvf-p01-backend feat/cash-voucher-form-redesign`, with its own test database base name `qldonhang_integtest_cvf_p01` in `TEST_DB_CONNECTION`.
- When both halves are done: `git -C "$WT" merge --no-ff feat/cvf-p01-backend -m "merge: phase 01 backend"` (plus the Co-Authored-By trailer), then `git -C "$WT" worktree remove /D/Projects/QLBanHang/.claude/worktrees/cvf-p01-backend` and `git -C "$WT" branch -d feat/cvf-p01-backend`. Run the phase Verification only after the merge, in `$WT`.

## Files

Backend:
- `backend/src/OrderMgmt.WebApi/Controllers/CashVouchersController.cs`, `StockVouchersController.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/CashVouchers/Interfaces/ICashVoucherService.cs`, `…/Services/CashVoucherService.cs` (modify)
- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Interfaces/IStockVoucherService.cs`, `…/Services/StockVoucherService.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/CashVouchers/CashVoucherQueryTests.cs`, `Inventory/StockVouchers/StockVoucherHelpersTests.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/PartnerQuickAddSeedTests.cs` (create)

Frontend:
- `frontend/src/features/cash-common/reason-match.ts` + `.test.ts` (create)
- `frontend/src/features/cash-common/use-reason-notice.ts` + `.test.ts` (create)
- `frontend/src/features/cash-vouchers/auto-text.ts` + `.test.ts` (create)
- `frontend/src/lib/enter-focus.ts` + `.test.ts` (create)

## Tasks

Backend commands run from `$WT/backend` with
`export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_cvf;Username=postgres;Password=1"`.
Frontend commands run from `$WT/frontend`; `T` below means `npx vitest run --config vitest.config.ts`.

### Task 1.1 — `activeOnly` on `GET /api/cash-vouchers/partners`

1. **Write the failing test.** In `CashVoucherQueryTests.cs` extend the private `SearchAsync(HttpClient client, CashVoucherType type, string keyword, Guid? reasonId)` with a last parameter `bool? activeOnly = null` that appends `&activeOnly=true|false` (lower case) to the query when it has a value. Add a test next to the existing partner search assertions:
   ```csharp
   [Fact]
   public async Task Partner_search_active_only_flag()
   {
       var (_, client, _) = await BranchClientAsync();
       var key = NewCode("AO");
       var customer = await CreatePartnerAsync(key + "C", isCustomer: true, isSupplier: false);
       var supplier = await CreatePartnerAsync(key + "S", isCustomer: false, isSupplier: true);
       var inactive = await CreatePartnerAsync(key + "X", isCustomer: false, isSupplier: true);
       await InDbAsync(async db =>
       {
           (await db.Customers.SingleAsync(c => c.Id == inactive)).Status = CustomerStatus.Inactive;
           await db.SaveChangesAsync();
       });
       var tncc = await CashReasonIdAsync("TNCC");

       // New: no reason, every role, active only.
       (await SearchAsync(client, CashVoucherType.Receipt, key, null, activeOnly: true))
           .Should().BeEquivalentTo(new[] { customer, supplier });
       // Unchanged defaults: no reason = filter mode (inactive included); a reason = active only.
       (await SearchAsync(client, CashVoucherType.Receipt, key, null))
           .Should().BeEquivalentTo(new[] { customer, supplier, inactive });
       (await SearchAsync(client, CashVoucherType.Payment, key, tncc))
           .Should().BeEquivalentTo(new[] { supplier });
       // Explicit false with a reason keeps the reason's role and adds the inactive partner.
       (await SearchAsync(client, CashVoucherType.Payment, key, tncc, activeOnly: false))
           .Should().BeEquivalentTo(new[] { supplier, inactive });
   }
   ```
   No new `using` is needed: `CustomerStatus` (`OrderMgmt.Domain.Enums`) and `SingleAsync` (`Microsoft.EntityFrameworkCore`) are already imported. `NewCode`, `BranchClientAsync`, `CreatePartnerAsync`, `CashReasonIdAsync` and `InDbAsync` are already used by the neighbouring tests of the file.
2. **Run it, expect FAIL.**
   `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashVoucherQueryTests.Partner_search_active_only_flag"`
   Expected: the first assertion fails because the endpoint ignores `activeOnly` and also returns `inactive` ("Expected … to be equivalent to … but … contains … extra"). Paste the failing line in the report.
3. **Implement.**
   - `CashVouchersController.SearchPartners`: add `[FromQuery] bool? activeOnly = null,` after `reasonId` and pass it: `_service.SearchPartnersAsync(type, keyword, limit, reasonId, activeOnly, ct)`.
   - `ICashVoucherService.SearchPartnersAsync(CashVoucherType type, string? keyword, int limit, Guid? reasonId, bool? activeOnly = null, CancellationToken ct = default)`; the same signature in `CashVoucherService`.
   - In the service replace `ActiveOnly = reasonId.HasValue,` with `ActiveOnly = activeOnly ?? reasonId.HasValue,` and update the doc comment: "`activeOnly` overrides the default (a reason means active only)".
4. **Run the test and the rest of the class, expect PASS.**
   `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashVoucherQueryTests"`
5. **Commit.** `feat(backend): activeOnly on the cash voucher partner search`

### Task 1.2 — `activeOnly` on `GET /api/stock-vouchers/partners`

1. **Write the failing test.** In `StockVoucherHelpersTests.cs` add, after `Partner_search_scoping`, a test `Partner_search_active_only_flag`. Reuse that test's data (a customer `PS-KH`, a supplier `PS-NCC`, a dual-role `PS-HH`, an inactive supplier `PS-NGUNG`) and its local `SearchAsync` shape (copy the 4-line local function; the existing one is local to its test). Assertions, keyword `PS-`:
   - `type=In&activeOnly=true` (no reason) → `{customer, supplier, both}` (active, every role);
   - `type=In` (no reason, no flag) → `{customer, supplier, both, inactive}` (unchanged);
   - `type=In&reasonId={nmh}` → `{supplier, both}` (unchanged);
   - `type=In&reasonId={nmh}&activeOnly=false` → `{supplier, both, inactive}`.
2. **Run it, expect FAIL.** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~StockVoucherHelpersTests.Partner_search_active_only_flag"` — first assertion fails with an extra `inactive` item.
3. **Implement** exactly as Task 1.1: `StockVouchersController.SearchPartners`, `IStockVoucherService.SearchPartnersAsync(StockDirection type, string? keyword, int limit, Guid? reasonId, bool? activeOnly = null, CancellationToken ct = default)` and `StockVoucherService` (`ActiveOnly = activeOnly ?? reasonId.HasValue`).
4. **Run, expect PASS.** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~StockVoucherHelpersTests"`
5. **Commit.** `feat(backend): activeOnly on the stock voucher partner search`

### Task 1.3 — Seed grants for quick add (D12)

1. **Write the failing test.** Create `backend/tests/OrderMgmt.IntegrationTests/CashDebt/PartnerQuickAddSeedTests.cs`, same shape as `CashDebtSeedTests` (`[Collection(nameof(PostgresCollection))]`, `: CashDebtTestBase`, constructor `(PostgresFixture pg) : base(pg)`, a private `RoleCodesAsync(string roleCode)` that reads `db.Roles … RolePermissions … Permission.Code` into a `HashSet<string>`; copied on purpose, it is private there; the class sits in `CashDebt/` next to its model although the codes are catalog permissions):
   - `Accountant_can_quick_add_customers_and_suppliers`: `RoleCodesAsync(RoleCodes.Accountant)` contains `Permissions.Customers.Create`, `Permissions.Suppliers.View`, `Permissions.Suppliers.Create`.
   - `Warehouse_can_quick_add_customers_and_suppliers`: `RoleCodesAsync(RoleCodes.Warehouse)` contains `Customers.View`, `Customers.Create`, `Suppliers.View`, `Suppliers.Create`.
   - `Sales_keeps_customer_rights_only`: contains `Customers.Create`, does not contain `Suppliers.Create`.
2. **Run, expect FAIL.** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~PartnerQuickAddSeedTests"` — the Accountant and Warehouse facts fail ("Expected … to contain \"customers.create\"" / "\"suppliers.create\""); Sales passes.
3. **Implement.** In `DbSeeder.cs` role definitions:
   - `RoleCodes.Accountant`: after `Permissions.Customers.View, Permissions.Products.View,` add `Permissions.Customers.Create, Permissions.Suppliers.View, Permissions.Suppliers.Create,`.
   - `RoleCodes.Warehouse`: after `Permissions.Customers.View, Permissions.Products.View,` add `Permissions.Customers.Create,`; at the end of its array (after `Permissions.Suppliers.View`) add `Permissions.Suppliers.Create`.
   Add a one-line comment above the Accountant entry: "Quick add of partners from the voucher forms (brainstorm 261015-0644, D12). Existing databases: an admin enables these in the permission matrix; the seeder grants only new permission codes."
4. **Run, expect PASS**, then the neighbours that read role grants:
   `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~PartnerQuickAddSeedTests|FullyQualifiedName~CashDebtSeedTests|FullyQualifiedName~DbSeederUpgradeTests|FullyQualifiedName~AdminRolesCrudTests"`
   If a neighbour asserts an exact permission list for these two roles, update that assertion (the new grants are intended) and say so in the report.
5. **Commit.** `feat(backend): let the accountant and warehouse roles quick add partners`

### Task 1.4 — `reason-match.ts`

1. **Write the failing test** `frontend/src/features/cash-common/reason-match.test.ts`:
   ```ts
   import { describe, expect, it } from 'vitest';
   import {
     matchReasonForPartner, partnerFitsReason, partnerRoleLabel, partnerRolesKnown, preferredPartnerRole,
     reasonMismatchMessage, reasonSwitchMessage, requiredRole, type ReasonRoleInfo,
   } from './reason-match';

   const info = (partnerType: ReasonRoleInfo['partnerType'], debtEffect: ReasonRoleInfo['debtEffect'] = 'None'): ReasonRoleInfo =>
     ({ partnerType, debtEffect });
   const reason = (id: string, partnerType: ReasonRoleInfo['partnerType'], debtEffect: ReasonRoleInfo['debtEffect'] = 'None') =>
     ({ id, partnerType, debtEffect });

   describe('requiredRole (section-01 §4.1)', () => {
     it.each([
       // None+ReceivableIncrease, Customer+PayableIncrease and Supplier+ReceivableDecrease cannot be saved
       // (DebtEffectRules.IsAllowedPartnerType); they only pin "partner type wins".
       ['None', 'None', 'none'], ['None', 'ReceivableIncrease', 'none'],
       ['Customer', 'None', 'customer'], ['Customer', 'PayableIncrease', 'customer'],
       ['Supplier', 'None', 'supplier'], ['Supplier', 'ReceivableDecrease', 'supplier'],
       ['Any', 'None', 'any'],
       ['Any', 'ReceivableDecrease', 'customer'], ['Any', 'ReceivableIncrease', 'customer'],
       ['Any', 'PayableDecrease', 'supplier'], ['Any', 'PayableIncrease', 'supplier'],
     ] as const)('%s + %s -> %s', (partnerType, debtEffect, expected) => {
       expect(requiredRole(info(partnerType, debtEffect))).toBe(expected);
     });
   });

   describe('partnerFitsReason', () => {
     it('an empty reason never fits', () => expect(partnerFitsReason({ isCustomer: true }, undefined)).toBe(false));
     it('a reason without partner never fits', () => expect(partnerFitsReason({ isCustomer: true, isSupplier: true }, info('None'))).toBe(false));
     it('an "any" reason fits everyone, even unknown roles', () => expect(partnerFitsReason({}, info('Any'))).toBe(true));
     it('a customer reason needs isCustomer', () => {
       expect(partnerFitsReason({ isCustomer: true }, info('Customer'))).toBe(true);
       expect(partnerFitsReason({ isSupplier: true }, info('Customer'))).toBe(false);
       expect(partnerFitsReason({}, info('Customer'))).toBe(false);
     });
     it('a supplier reason needs isSupplier', () => {
       expect(partnerFitsReason({ isSupplier: true }, info('Supplier'))).toBe(true);
       expect(partnerFitsReason({ isCustomer: true }, info('Supplier'))).toBe(false);
     });
     it('"any" with a payable effect needs a supplier', () => {
       expect(partnerFitsReason({ isCustomer: true }, info('Any', 'PayableIncrease'))).toBe(false);
       expect(partnerFitsReason({ isSupplier: true }, info('Any', 'PayableIncrease'))).toBe(true);
     });
   });

   describe('matchReasonForPartner (section-01 §4.2)', () => {
     const none = reason('none', 'None');
     const tkh = reason('tkh', 'Customer');
     const tncc = reason('tncc', 'Supplier');
     const other = reason('other', 'Any');
     const anyRecv = reason('any-recv', 'Any', 'ReceivableDecrease');
     const customer = { isCustomer: true, isSupplier: false };
     const supplier = { isCustomer: false, isSupplier: true };
     const both = { isCustomer: true, isSupplier: true };

     it('keeps a reason that fits', () =>
       expect(matchReasonForPartner(customer, 'tkh', [none, tkh, tncc, other])).toEqual({ kind: 'keep' }));
     it('keeps an "any" reason that fits', () =>
       expect(matchReasonForPartner(customer, 'other', [none, tkh, tncc, other])).toEqual({ kind: 'keep' }));
     it('switches to the first customer reason', () =>
       expect(matchReasonForPartner(customer, 'tncc', [none, tkh, tncc, other])).toEqual({ kind: 'switch', reasonId: 'tkh' }));
     it('switches to the first supplier reason', () =>
       expect(matchReasonForPartner(supplier, 'tkh', [none, tkh, tncc, other])).toEqual({ kind: 'switch', reasonId: 'tncc' }));
     it('an empty reason counts as not fitting', () =>
       expect(matchReasonForPartner(customer, '', [none, tkh, tncc])).toEqual({ kind: 'switch', reasonId: 'tkh' }));
     it('a "no partner" reason does not fit', () =>
       expect(matchReasonForPartner(customer, 'none', [none, tkh])).toEqual({ kind: 'switch', reasonId: 'tkh' }));
     it('prefers a role reason over an earlier plain "any" reason', () =>
       expect(matchReasonForPartner(customer, '', [other, tkh])).toEqual({ kind: 'switch', reasonId: 'tkh' }));
     it('counts an "any" reason with a receivable effect as a customer reason', () =>
       expect(matchReasonForPartner(customer, '', [other, anyRecv])).toEqual({ kind: 'switch', reasonId: 'any-recv' }));
     it('falls back to a plain "any" reason', () =>
       expect(matchReasonForPartner(customer, 'tncc', [tncc, other])).toEqual({ kind: 'switch', reasonId: 'other' }));
     it('a partner with both roles keeps a supplier reason and otherwise takes the first role reason', () => {
       expect(matchReasonForPartner(both, 'tncc', [tkh, tncc])).toEqual({ kind: 'keep' });
       expect(matchReasonForPartner(both, '', [tkh, tncc])).toEqual({ kind: 'switch', reasonId: 'tkh' });
     });
     it('reports none when nothing fits', () => {
       expect(matchReasonForPartner(customer, 'none', [none])).toEqual({ kind: 'none' });
       expect(matchReasonForPartner(customer, 'tncc', [none, tncc])).toEqual({ kind: 'none' });
     });
     it('a partner without roles only fits a plain "any" reason', () => {
       expect(matchReasonForPartner({}, 'tkh', [tkh, other])).toEqual({ kind: 'switch', reasonId: 'other' });
       expect(matchReasonForPartner({ isCustomer: false, isSupplier: false }, 'tkh', [tkh])).toEqual({ kind: 'none' });
       expect(matchReasonForPartner({}, 'other', [tkh, other])).toEqual({ kind: 'keep' });
     });
   });

   describe('preferredPartnerRole (the role a quick add starts with, section-01 §6.2)', () => {
     it('follows the role a reason requires', () => {
       expect(preferredPartnerRole(info('Customer'), 'supplier')).toBe('customer');
       expect(preferredPartnerRole(info('Supplier'), 'customer')).toBe('supplier');
       expect(preferredPartnerRole(info('Any', 'PayableIncrease'), 'customer')).toBe('supplier');
     });
     it('uses the voucher direction for a plain "any", a "no partner" or a missing reason', () => {
       expect(preferredPartnerRole(info('Any'), 'supplier')).toBe('supplier');
       expect(preferredPartnerRole(info('None'), 'customer')).toBe('customer');
       expect(preferredPartnerRole(undefined, 'supplier')).toBe('supplier');
     });
   });

   describe('messages', () => {
     it('partnerRolesKnown needs both flags', () => {
       expect(partnerRolesKnown({ isCustomer: true, isSupplier: false })).toBe(true);
       expect(partnerRolesKnown({ isCustomer: true })).toBe(false);
       expect(partnerRolesKnown(null)).toBe(false);
     });
     it('partnerRoleLabel', () => {
       expect(partnerRoleLabel({ isCustomer: true })).toBe('khách hàng');
       expect(partnerRoleLabel({ isSupplier: true })).toBe('nhà cung cấp');
       expect(partnerRoleLabel({ isCustomer: true, isSupplier: true })).toBe('khách hàng và nhà cung cấp');
       expect(partnerRoleLabel({})).toBe('đối tượng');
     });
     it('reasonSwitchMessage', () =>
       expect(reasonSwitchMessage('KH0001', { isCustomer: true })).toBe('Đã đổi lý do cho hợp với KH0001 (khách hàng).'));
     it('reasonMismatchMessage names the needed role', () => {
       expect(reasonMismatchMessage({ name: 'Thu tiền khách hàng', ...info('Customer') }, 'NCC001'))
         .toBe('Lý do "Thu tiền khách hàng" cần khách hàng — NCC001 không phải khách hàng');
       expect(reasonMismatchMessage({ name: 'Chi trả NCC', ...info('Supplier') }, 'KH01'))
         .toBe('Lý do "Chi trả NCC" cần nhà cung cấp — KH01 không phải nhà cung cấp');
     });
     it('reasonMismatchMessage follows the debt side of an "any" reason', () => {
       expect(reasonMismatchMessage({ name: 'Thu nợ khác', ...info('Any', 'ReceivableDecrease') }, 'NCC001'))
         .toBe('Lý do "Thu nợ khác" cần khách hàng — NCC001 không phải khách hàng');
       expect(reasonMismatchMessage({ name: 'Thu hoàn khác', ...info('Any', 'PayableIncrease') }, 'KH01'))
         .toBe('Lý do "Thu hoàn khác" cần nhà cung cấp — KH01 không phải nhà cung cấp');
     });
     it('reasonMismatchMessage is empty for "any" and "no partner" reasons', () => {
       expect(reasonMismatchMessage({ name: 'Thu khác', ...info('Any') }, 'KH01')).toBeUndefined();
       expect(reasonMismatchMessage({ name: 'Không có', ...info('None') }, 'KH01')).toBeUndefined();
     });
   });
   ```
2. **Run, expect FAIL.** `T src/features/cash-common/reason-match.test.ts` — `Failed to resolve import "./reason-match"`.
3. **Implement** `frontend/src/features/cash-common/reason-match.ts` with the contract of SUMMARY "Shared contracts". Core:
   ```ts
   export function requiredRole(reason: ReasonRoleInfo): RequiredRole {
     switch (reason.partnerType) {
       case 'None': return 'none';
       case 'Customer': return 'customer';
       case 'Supplier': return 'supplier';
       default:
         // Explicit values: a mock or old payload without debtEffect must not throw.
         if (reason.debtEffect === 'ReceivableDecrease' || reason.debtEffect === 'ReceivableIncrease') return 'customer';
         if (reason.debtEffect === 'PayableDecrease' || reason.debtEffect === 'PayableIncrease') return 'supplier';
         return 'any';
     }
   }
   export function partnerFitsReason(partner: PartnerRoles, reason: ReasonRoleInfo | undefined): boolean {
     if (!reason) return false;
     switch (requiredRole(reason)) {
       case 'none': return false;
       case 'any': return true;
       case 'customer': return partner.isCustomer === true;
       default: return partner.isSupplier === true;
     }
   }
   export function matchReasonForPartner<R extends ReasonRoleInfo & { id: string }>(
     partner: PartnerRoles, currentReasonId: string, reasons: readonly R[],
   ): ReasonMatch {
     const current = reasons.find((r) => r.id === currentReasonId);
     if (current && partnerFitsReason(partner, current)) return { kind: 'keep' };
     const withRole = reasons.find((r) => {
       const role = requiredRole(r);
       return (role === 'customer' || role === 'supplier') && partnerFitsReason(partner, r);
     });
     if (withRole) return { kind: 'switch', reasonId: withRole.id };
     const plain = reasons.find((r) => requiredRole(r) === 'any');
     return plain ? { kind: 'switch', reasonId: plain.id } : { kind: 'none' };
   }
   ```
   plus `partnerRolesKnown`, `partnerRoleLabel`, `reasonSwitchMessage` as the test reads,
   ```ts
   // Text only; the caller decides when to show it (roles known and the partner does not fit).
   export function reasonMismatchMessage(reason: ReasonRoleInfo & { name: string }, partnerCode: string): string | undefined {
     const role = requiredRole(reason);   // never reason.partnerType: an "any" reason with a debt effect needs a side role
     if (role !== 'customer' && role !== 'supplier') return undefined;
     const label = role === 'customer' ? 'khách hàng' : 'nhà cung cấp';
     return `Lý do "${reason.name}" cần ${label} — ${partnerCode} không phải ${label}`;
   }
   ```
   and
   ```ts
   // The role a quick add starts with: what the reason requires, else the voucher direction
   // (receipt / stock-out -> customer, payment / stock-in -> supplier).
   export function preferredPartnerRole(reason: ReasonRoleInfo | undefined, fallback: 'customer' | 'supplier'): 'customer' | 'supplier' {
     const role = reason ? requiredRole(reason) : 'any';
     return role === 'customer' || role === 'supplier' ? role : fallback;
   }
   ```
   Comment on `requiredRole`: "Mirrors ValidatePartner in CashVoucherService and StockVoucherService (partner type of the reason, then the side role of its debt effect)."
4. **Run, expect PASS.** `T src/features/cash-common/reason-match.test.ts`
5. **Commit.** `feat(frontend): reason match rules for a partner picked before the reason`

### Task 1.5 — `useReasonSwitchNotice`

1. **Write the failing test** `frontend/src/features/cash-common/use-reason-notice.test.ts`:
   ```ts
   import { act, renderHook } from '@testing-library/react';
   import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
   import { useReasonSwitchNotice } from './use-reason-notice';

   describe('useReasonSwitchNotice', () => {
     beforeEach(() => vi.useFakeTimers());
     afterEach(() => vi.useRealTimers());

     it('starts empty', () => {
       const { result } = renderHook(() => useReasonSwitchNotice());
       expect(result.current.notice).toBeNull();
       expect(result.current.flash).toBe(false);
     });
     it('show keeps the text and flashes for 1.6 s', () => {
       const { result } = renderHook(() => useReasonSwitchNotice());
       act(() => result.current.show('Đã đổi lý do'));
       expect(result.current.notice).toBe('Đã đổi lý do');
       expect(result.current.flash).toBe(true);
       act(() => { vi.advanceTimersByTime(1599); });
       expect(result.current.flash).toBe(true);
       act(() => { vi.advanceTimersByTime(2); });
       expect(result.current.flash).toBe(false);
       expect(result.current.notice).toBe('Đã đổi lý do');
     });
     it('a second show restarts the flash', () => {
       const { result } = renderHook(() => useReasonSwitchNotice());
       act(() => result.current.show('a'));
       act(() => { vi.advanceTimersByTime(1500); });
       act(() => result.current.show('b'));
       act(() => { vi.advanceTimersByTime(1500); });
       expect(result.current.flash).toBe(true);
       expect(result.current.notice).toBe('b');
     });
     it('clear removes the text and the flash', () => {
       const { result } = renderHook(() => useReasonSwitchNotice());
       act(() => result.current.show('a'));
       act(() => result.current.clear());
       expect(result.current.notice).toBeNull();
       expect(result.current.flash).toBe(false);
     });
   });
   ```
2. **Run, expect FAIL.** `T src/features/cash-common/use-reason-notice.test.ts` — `Failed to resolve import "./use-reason-notice"`.
3. **Implement** `use-reason-notice.ts`: state `notice`/`flash`, a `useRef` timer, `FLASH_MS = 1600`, `show` (set both, restart timer), `clear` (reset both, clear timer), cleanup in `useEffect` return; all callbacks `useCallback`ed with no dependencies.
4. **Run, expect PASS.**
5. **Commit.** `feat(frontend): shared hook for the "reason changed" notice`

### Task 1.6 — `auto-text.ts`

1. **Write the failing test** `frontend/src/features/cash-vouchers/auto-text.test.ts`:
   ```ts
   import { describe, expect, it } from 'vitest';
   import { buildAutoDescription, followAuto, followHeaderInLines, isUntouchedLine } from './auto-text';

   describe('buildAutoDescription (D2)', () => {
     it('joins the reason and the partner', () =>
       expect(buildAutoDescription('Thu tiền khách hàng', 'Công ty A')).toBe('Thu tiền khách hàng - Công ty A'));
     it('is the reason alone without a partner', () => {
       expect(buildAutoDescription('Thu tiền khách hàng', '')).toBe('Thu tiền khách hàng');
       expect(buildAutoDescription('Thu tiền khách hàng', '   ')).toBe('Thu tiền khách hàng');
       expect(buildAutoDescription('Thu tiền khách hàng', undefined)).toBe('Thu tiền khách hàng');
     });
     it('is empty without a reason', () => {
       expect(buildAutoDescription('', 'Công ty A')).toBe('');
       expect(buildAutoDescription(undefined, 'Công ty A')).toBe('');
       expect(buildAutoDescription(null, null)).toBe('');
     });
     it('trims both parts', () => expect(buildAutoDescription('  Thu  ', ' A ')).toBe('Thu - A'));
   });

   describe('followAuto (D3)', () => {
     it('fills an empty field', () => {
       expect(followAuto('', 'old', 'new')).toBe('new');
       expect(followAuto('  ', 'old', 'new')).toBe('new');
       expect(followAuto(undefined, 'old', 'new')).toBe('new');
     });
     it('follows a field that still holds the previous auto text', () =>
       expect(followAuto('old', 'old', 'new')).toBe('new'));
     it('keeps a hand-edited field', () => expect(followAuto('typed', 'old', 'new')).toBe('typed'));
   });

   describe('followHeaderInLines', () => {
     it('updates empty lines and lines equal to the old text, keeps edited ones', () => {
       const lines = [{ description: '' }, { description: 'old' }, { description: 'Hàng lỗi lô 05' }];
       const r = followHeaderInLines(lines, 'old', 'new');
       expect(r.changed).toBe(true);
       expect(r.lines.map((l) => l.description)).toEqual(['new', 'new', 'Hàng lỗi lô 05']);
       expect(lines[0].description).toBe('');   // the input is not mutated
     });
     it('returns the same array when nothing changes', () => {
       const lines = [{ description: 'typed' }];
       const r = followHeaderInLines(lines, 'old', 'new');
       expect(r.changed).toBe(false);
       expect(r.lines).toBe(lines);
     });
     it('does nothing when the text did not change', () => {
       const lines = [{ description: '' }];
       expect(followHeaderInLines(lines, 'same', 'same').changed).toBe(false);
     });
     it('keeps the other fields of a line', () => {
       const r = followHeaderInLines([{ description: '', amount: 5 }], '', 'new');
       expect(r.lines[0]).toEqual({ description: 'new', amount: 5 });
     });
   });

   describe('isUntouchedLine (section-01 §3.4)', () => {
     it('zero amount with an empty description', () => expect(isUntouchedLine({ description: '', amount: 0 }, 'Thu')).toBe(true));
     it('zero amount with the header text', () => expect(isUntouchedLine({ description: 'Thu', amount: 0 }, 'Thu')).toBe(true));
     it('an amount makes it touched', () => expect(isUntouchedLine({ description: 'Thu', amount: 5 }, 'Thu')).toBe(false));
     it('another description makes it touched', () => expect(isUntouchedLine({ description: 'Khác', amount: 0 }, 'Thu')).toBe(false));
     it('accepts string and missing amounts', () => {
       expect(isUntouchedLine({ description: '', amount: '0' }, 'Thu')).toBe(true);
       expect(isUntouchedLine({ description: '' }, 'Thu')).toBe(true);
       expect(isUntouchedLine({ description: '', amount: '1.000' }, 'Thu')).toBe(false);
     });
   });
   ```
   (`toNumberOrZero('1.000')`: if `lib/to-number.ts` reads dots as decimals the value is 1, still non-zero; the assertion holds either way. Check `lib/to-number.ts` before relying on string forms.)
2. **Run, expect FAIL.** `T src/features/cash-vouchers/auto-text.test.ts` — `Failed to resolve import "./auto-text"`.
3. **Implement** `auto-text.ts`:
   ```ts
   import { toNumberOrZero } from '@/lib/to-number';

   export function buildAutoDescription(reasonName: string | null | undefined, partnerName: string | null | undefined): string {
     const reason = (reasonName ?? '').trim();
     if (!reason) return '';
     const partner = (partnerName ?? '').trim();
     return partner ? `${reason} - ${partner}` : reason;
   }

   // A field follows the auto text while it is empty or still holds the previous auto text.
   export function followAuto(current: string | undefined, prevAuto: string, nextAuto: string): string {
     const value = current ?? '';
     return value.trim() === '' || value === prevAuto ? nextAuto : value;
   }

   export function followHeaderInLines<T extends { description?: string }>(
     lines: readonly T[], oldDescription: string, newDescription: string,
   ): { lines: T[]; changed: boolean } {
     if (oldDescription === newDescription) return { lines: lines as T[], changed: false };
     let changed = false;
     const next = lines.map((line) => {
       const value = line.description ?? '';
       if ((value.trim() === '' || value === oldDescription) && value !== newDescription) {
         changed = true;
         return { ...line, description: newDescription };
       }
       return line;
     });
     return changed ? { lines: next, changed } : { lines: lines as T[], changed: false };
   }

   export function isUntouchedLine(line: { description?: string; amount?: number | string }, headerDescription: string): boolean {
     const description = (line.description ?? '').trim();
     return toNumberOrZero(line.amount) === 0 && (description === '' || description === headerDescription.trim());
   }
   ```
4. **Run, expect PASS.**
5. **Commit.** `feat(frontend): pure auto text rules for the cash voucher descriptions`

### Task 1.7 — `enter-focus.ts`

1. **Write the failing test** `frontend/src/lib/enter-focus.test.ts` (jsdom):
   ```ts
   import { afterEach, describe, expect, it } from 'vitest';
   import { focusById, focusNextInOrder } from './enter-focus';

   function mount(html: string) { document.body.innerHTML = html; }
   afterEach(() => { document.body.innerHTML = ''; });

   describe('focusNextInOrder', () => {
     it('moves to the next enabled field, skipping disabled and missing ones', () => {
       mount('<input id="a"><input id="b" disabled><input id="c">');
       expect(focusNextInOrder(['a', 'b', 'x', 'c'], 'a', 1)).toBe('moved');
       expect(document.activeElement?.id).toBe('c');
     });
     it('moves backwards', () => {
       mount('<input id="a"><input id="b" disabled><input id="c">');
       expect(focusNextInOrder(['a', 'b', 'c'], 'c', -1)).toBe('moved');
       expect(document.activeElement?.id).toBe('a');
     });
     it('reports the end of the list without moving', () => {
       mount('<input id="a"><input id="b">');
       (document.getElementById('b') as HTMLElement).focus();
       expect(focusNextInOrder(['a', 'b'], 'b', 1)).toBe('end');
       expect(focusNextInOrder(['a', 'b'], 'a', -1)).toBe('end');
       expect(document.activeElement?.id).toBe('b');
     });
     it('reports an id that is not in the list', () => {
       mount('<input id="a">');
       expect(focusNextInOrder(['a'], 'zzz', 1)).toBe('unknown');
     });
   });

   describe('focusById', () => {
     it('selects the text', () => {
       mount('<input id="a" value="abc">');
       expect(focusById('a', 'select')).toBe(true);
       const el = document.getElementById('a') as HTMLInputElement;
       expect([el.selectionStart, el.selectionEnd]).toEqual([0, 3]);
     });
     it('puts the caret at the end', () => {
       mount('<input id="a" value="abc">');
       focusById('a', 'end');
       const el = document.getElementById('a') as HTMLInputElement;
       expect([el.selectionStart, el.selectionEnd]).toEqual([3, 3]);
     });
     it('only focuses by default', () => {
       mount('<input id="a" value="abc">');
       expect(focusById('a')).toBe(true);
       expect(document.activeElement?.id).toBe('a');
     });
     it('does nothing for a missing or disabled element', () => {
       mount('<input id="a" disabled>');
       expect(focusById('a')).toBe(false);
       expect(focusById('nope')).toBe(false);
     });
     it('does not throw for inputs without a selection range', () => {
       mount('<input id="d" type="datetime-local">');
       expect(() => focusById('d', 'end')).not.toThrow();
     });
   });
   ```
2. **Run, expect FAIL.** `T src/lib/enter-focus.test.ts` — `Failed to resolve import "./enter-focus"`.
3. **Implement** `frontend/src/lib/enter-focus.ts`:
   ```ts
   import { canTakeFocus } from '@/lib/focus';   // exists: connected and not :disabled (covers `fieldset disabled`)

   export type FocusStep = 'moved' | 'end' | 'unknown';

   /** Focuses the next (direction 1) or previous (-1) field of `ids` that exists and is not disabled. */
   export function focusNextInOrder(ids: readonly string[], currentId: string, direction: 1 | -1): FocusStep {
     const at = ids.indexOf(currentId);
     if (at === -1) return 'unknown';
     for (let i = at + direction; i >= 0 && i < ids.length; i += direction) {
       const el = document.getElementById(ids[i]);
       if (canTakeFocus(el)) {
         el.focus();
         return 'moved';
       }
     }
     return 'end';
   }

   /** Focuses an element; `select` selects its text, `end` puts the caret after it. */
   export function focusById(id: string, mode: 'keep' | 'select' | 'end' = 'keep'): boolean {
     const el = document.getElementById(id);
     if (!canTakeFocus(el)) return false;
     el.focus();
     if (el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement) {
       try {
         if (mode === 'select') el.select();
         else if (mode === 'end') el.setSelectionRange(el.value.length, el.value.length);
       } catch {
         // Inputs such as datetime-local have no selection range.
       }
     }
     return true;
   }
   ```
4. **Run, expect PASS.**
5. **Commit.** `feat(frontend): focus helpers for Enter navigation`

## Verification

- `cd "$WT/frontend" && npm run typecheck && npm run lint && T src/features/cash-common src/features/cash-vouchers/auto-text.test.ts src/lib/enter-focus.test.ts` (`typecheck` uses `tsconfig.app.json`, which excludes `*.test.ts(x)`: test files are only transpiled by vitest, not type-checked).
- `cd "$WT/backend" && dotnet build OrderMgmt.sln` and the filter of Task 1.3 step 4 plus `FullyQualifiedName~CashVoucherQueryTests|FullyQualifiedName~StockVoucherHelpersTests`.
- Leak checks of "Executor conventions" in SUMMARY.

## Exit Criteria

- Both partner search endpoints accept `activeOnly`; the old default (a reason means active only) is unchanged and tested.
- The Accountant and Warehouse roles of a fresh database hold the five grants.
- The four pure modules exist with the exact exports of the SUMMARY contracts and their tests pass.
- No screen behaves differently yet; the baseline failures are unchanged.
