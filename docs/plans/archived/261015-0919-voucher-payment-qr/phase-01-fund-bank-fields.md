# Phase 01 — Bank and account holder on funds

**Status:** [x] completed
**Complexity:** M

## Objective

- Give a bank fund a bank from the `Banks` table (`BankId`) and an account holder name (`AccountHolderName`), through one migration, the fund DTOs, the validator and `FundService` (Q3).
- Let an admin enter both on the fund form, with the hint that a complete bank fund prints a QR.

Nothing reads the new fields yet; Phase 02 does.

## Files

Backend (`backend/`):

- `src/OrderMgmt.Domain/Entities/CashDebt/Fund.cs` (modify)
- `src/OrderMgmt.Infrastructure/Persistence/Configurations/CashDebtConfiguration.cs` (modify — `FundConfiguration`)
- `src/OrderMgmt.Infrastructure/Persistence/Migrations/<timestamp>_AddFundBankAccount.cs`, `.Designer.cs`, `AppDbContextModelSnapshot.cs` (generated)
- `src/OrderMgmt.Application/CashDebt/Funds/Models/FundDtos.cs` (modify)
- `src/OrderMgmt.Application/CashDebt/Funds/Validators/FundValidators.cs` (modify)
- `src/OrderMgmt.Application/CashDebt/Funds/Services/FundService.cs` (modify)
- `tests/OrderMgmt.IntegrationTests/CashDebt/FundBankFieldsTests.cs` (new)

Frontend (`frontend/src/`):

- `features/funds/types.ts` (modify)
- `pages/funds/fund-form-dialog.tsx` (modify)
- `pages/funds/fund-form-dialog.test.tsx` (new)

## Reference files (read-only)

- `tests/OrderMgmt.IntegrationTests/CashDebt/FundCrudTests.cs` — `NewBranchClientAsync`, `NewFund`, `ToUpdate`, `ErrorKeysAsync`, `ReadDataAsync`.
- `src/OrderMgmt.Application/Payments/Services/BankLookupService.cs` and `GET api/banks` (active banks, `Id`, `Code`, `Name`, `ShortName`, `Bin`).
- `frontend/src/features/banks/hooks.ts` (`useBanks`), `pages/payment-qr/payment-qr-page.tsx` (how banks are shown), `pages/funds/fund-list-page.test.tsx` (how the fund dialog is mocked/rendered).
- Any dialog test that drives a Radix `Select` (grep `SelectTrigger` in `*.test.tsx`) for the way to open and pick an option in jsdom.

## Tasks

### Task 1.1 — Backend: columns, DTOs, validation, migration

1. **Write the failing tests** in `tests/OrderMgmt.IntegrationTests/CashDebt/FundBankFieldsTests.cs` (`[Collection(nameof(PostgresCollection))]`, inherits `CashDebtTestBase`; copy the small helpers `NewBranchClientAsync` / `NewFund` from `FundCrudTests`). Take a bank with `var bank = (await ReadDataAsync<List<BankDto>>(await _client.GetAsync("/api/banks"))).First();`.
   - `Bank_fund_stores_bank_and_holder`: create a `Bank` fund with `BankId = bank.Id`, `AccountNumber = "0123456789"`, `AccountHolderName = "CONG TY ABC"`; the response and `GET /api/funds/{id}` both carry `BankId`, `AccountHolderName` and `BankShortName == (bank.ShortName ?? bank.Code)`.
   - `Update_changes_bank_and_holder`: update to another bank and another holder name; the response shows the new values.
   - `Other_kind_stores_no_bank_fields`: creating a `Cash` fund with a `BankId` and a holder name returns both as `null`; updating a bank fund to `Cash` (a fund without vouchers) clears both.
   - `Unknown_bank_is_rejected`: `BankId = Guid.NewGuid()` → 400 with detail key `bankId`.
   - `Inactive_bank_is_rejected`: set a bank inactive in the database (`InDbAsync(db => db.Banks.Where(b => b.Id == other.Id).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, false)))`, using a second bank, and set it back at the end of the test) → 400 `bankId`.
   - `Account_number_rules_apply_with_a_bank`: with a `BankId`, `"12 34"`, `"AB-12"` and a 20-character `"1234567890123456789A"` → 400 key `accountNumber`; a missing account number with a `BankId` → 400 `accountNumber`; a 19-character alphanumeric value is accepted; the same 20-character text without a `BankId` is accepted (legacy free text, up to 50).
   - `Holder_name_is_limited`: 256 characters → 400 key `accountHolderName`; 255 is accepted.
2. **Run the tests to verify they fail:** from `backend/`: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~FundBankFieldsTests"`. Expected: FAIL — build error, `CreateFundRequest` has no `BankId` / `AccountHolderName`.
3. **Write the minimal implementation:**
   - `Fund`: add `BankId`, `Bank`, `AccountHolderName` (contract in SUMMARY). `using OrderMgmt.Domain.Entities.Payments;`.
   - `FundConfiguration`: `b.Property(x => x.AccountHolderName).HasMaxLength(255);` `b.HasOne(x => x.Bank).WithMany().HasForeignKey(x => x.BankId).OnDelete(DeleteBehavior.Restrict);` `b.HasIndex(x => x.BankId);`.
   - `FundDtos.cs`: the new members on `FundDto`, `CreateFundRequest`, `UpdateFundRequest`.
   - `FundValidators.cs`: in both validators `RuleFor(x => x.AccountHolderName).MaximumLength(255);` and
     ```csharp
     RuleFor(x => x.AccountNumber)
         .Must(a => !string.IsNullOrEmpty(a) && System.Text.RegularExpressions.Regex.IsMatch(a, "^[0-9A-Za-z]{1,19}$"))
         .When(x => x.Kind == FundKind.Bank && x.BankId != null)
         .WithMessage("Số tài khoản chỉ gồm chữ và số, tối đa 19 ký tự.");
     ```
     (the `MaximumLength(50)` rule stays for the other cases).
   - `FundService`:
     - `ListAsync` / `GetAsync`: `.Include(f => f.Bank)` so `ToDto` can fill `BankShortName` (`f.Bank == null ? null : f.Bank.ShortName ?? f.Bank.Code`), `BankId`, `AccountHolderName`. `CreateAsync` and `UpdateAsync` already end in `GetAsync`.
     - A private `ResolveBankAsync(FundKind kind, Guid? bankId, CancellationToken ct)` returns `null` when the kind is not `Bank` or `bankId` is null; otherwise it reads `_db.Banks.AsNoTracking().AnyAsync(b => b.Id == bankId && b.IsActive)` and throws `Invalid("bankId", "Ngân hàng không tồn tại hoặc đã ngừng sử dụng.")` when it is false; it returns `bankId`. Call it inside the transaction of `CreateAsync` and `UpdateAsync` before the fund is changed.
     - Assign `fund.BankId = bankId` and `fund.AccountHolderName = kind == FundKind.Bank ? Normalize(request.AccountHolderName) : null` in both methods.
   - Migration, from `backend/`: `dotnet ef migrations add AddFundBankAccount -p src/OrderMgmt.Infrastructure -s src/OrderMgmt.WebApi`. Open the generated `Up`: it must contain only `AddColumn account_holder_name`, `AddColumn bank_id` on `funds`, `CreateIndex` on `bank_id` and `AddForeignKey` to `banks` with `ReferentialAction.Restrict`. If it contains anything else (drift from another change), **stop and report**. Do not edit the snapshot by hand.
4. **Run the tests to verify they pass:** the filter of step 2, then `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Fund"` (every existing fund test still passes: the new request members are optional). Expected: PASS.
5. **Commit:** `git add backend && git commit -m "feat(funds): bank and account holder on bank funds"`

### Task 1.2 — Frontend: types and fund form

1. **Write the failing tests** in `pages/funds/fund-form-dialog.test.tsx` (mock `@/features/funds/hooks` `useCreateFund` / `useUpdateFund` with `mutateAsync` spies and `@/features/banks/hooks` `useBanks` returning two banks `{ id: 'b1', code: 'VCB', name: 'Ngân hàng Vietcombank', shortName: 'Vietcombank', bin: '970436' }` and `{ id: 'b2', code: 'TCB', name: 'Techcombank', bin: '970407' }`; `Dialog` renders in jsdom as in the other dialog tests):
   - `shows_bank_fields_only_for_a_bank_fund`: opening with a `Cash` fund shows no "Ngân hàng (cho QR)" and no "Chủ tài khoản"; with a `Bank` fund both show, with the hint "Có đủ ngân hàng, số tài khoản và chủ tài khoản thì phiếu thu/xuất in được QR."
   - `bank_options_show_short_name_and_bin`: opening the select lists "Vietcombank · 970436" and "TCB · 970407" (short name, else code).
   - `submits_bank_and_holder`: editing a `Bank` fund (`initial` with `bankId: 'b1'`, `accountNumber: '0123456789'`, `accountHolderName: 'CONG TY ABC'`) and pressing "Cập nhật" calls `update.mutateAsync` with `data` containing `bankId: 'b1'`, `accountHolderName: 'CONG TY ABC'`, `accountNumber: '0123456789'`.
   - `cash_fund_sends_no_bank_fields`: a `Cash` fund submits `bankId: null`, `accountHolderName: null`, `bankName: null`, `accountNumber: null`.
   - `rejects_a_bad_account_number_with_a_bank`: a `Bank` fund with `bankId` and `accountNumber: '12 34'` shows "Số tài khoản chỉ gồm chữ và số, tối đa 19 ký tự." and does not call `update`.
2. **Run the tests to verify they fail:** from `frontend/`: `npm run test -- fund-form-dialog`. Expected: FAIL — the labels are not found.
3. **Write the minimal implementation:**
   - `features/funds/types.ts`: `Fund` gets `bankId?: string | null; bankShortName?: string | null; accountHolderName?: string | null`; `CreateFundRequest` and `UpdateFundRequest` get `bankId?: string | null; accountHolderName?: string | null`.
   - `fund-form-dialog.tsx`: the schema gets `bankId: z.string()` (empty = none) and `accountHolderName: z.string().trim().max(255, 'Tối đa 255 ký tự')`; a `superRefine` (or a `.refine`) on the edit and create schemas: when `kind === 'Bank'` and `bankId !== ''`, `accountNumber` must match `/^[0-9A-Za-z]{1,19}$/` (issue on `accountNumber`, message "Số tài khoản chỉ gồm chữ và số, tối đa 19 ký tự."). `FIELDS` gets the two names. Inside `{isBank && …}` add the "Ngân hàng (cho QR)" `Select` (value `bankId` or a "Chưa chọn" item with value `none`, options from `useBanks().data`, label `` `${bank.shortName ?? bank.code} · ${bank.bin}` ``) and the "Chủ tài khoản" `Input` (`id="accountHolderName"`), then the hint as a `<p className="text-xs text-muted-foreground">`. `onSubmit` sends `bankId: bank && values.bankId ? values.bankId : null` and `accountHolderName: bank ? values.accountHolderName.trim() || null : null`. `toDefaults` maps `bankId ?? ''` and `accountHolderName ?? ''`. Use no new icons.
4. **Run the tests to verify they pass:** the filter of step 2, then `npm run test -- funds` and `npm run typecheck && npm run lint`. Expected: PASS.
5. **Commit:** `git add frontend && git commit -m "feat(funds): bank and account holder fields on the fund form"`

## Verification

- `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Fund"` passes.
- From `frontend/`: `npm run typecheck`, `npm run lint`, `npm run test -- funds` pass.
- The new migration's `Up` holds only the two columns, the index and the foreign key (inspect it); `git diff --stat` shows no other generated file than the migration, its designer and the snapshot.
- Run the application tests against the template database once (the fixture migrates `qldonhang_integtest_vqr_tpl`): any test passing proves the migration applies.

## Exit Criteria

- A bank fund stores and returns `BankId`, `BankShortName` and `AccountHolderName`; invalid banks and account numbers are refused with field-level 400s; other kinds store nothing.
- The fund form shows and submits the two fields only for bank funds.
- The existing fund tests and the TT133/cash tests that read funds still pass.
