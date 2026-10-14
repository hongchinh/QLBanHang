# Phase 01 — Company profile, amount in words, PDF converter rename

**Status:** [x] complete
**Complexity:** M

## Objective

Deliver the three independent building blocks the forms need:

- `VndAmountInWords`: a VND amount read in Vietnamese words (used by the reconciliation record in Phase 03).
- `CompanyProfile`: one row with the company's name, address, tax code and signers, with `GET/PUT api/settings/company`, permission `settings.company` and the settings page **Thông tin đơn vị**.
- `ISpreadsheetPdfConverter`: the PDF converter interface under a name that no longer says "Quotation", and a converter that kills LibreOffice when a conversion times out (T23).

## Files

Backend:
- `backend/src/OrderMgmt.Application/Common/VndAmountInWords.cs` (new)
- `backend/src/OrderMgmt.Domain/Entities/Organization/CompanyProfile.cs` (new)
- `backend/src/OrderMgmt.Domain/Constants/Permissions.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/CompanyProfileConfiguration.cs` (new)
- `backend/src/OrderMgmt.Infrastructure/Persistence/AppDbContext.cs`, `backend/src/OrderMgmt.Application/Common/Interfaces/IAppDbContext.cs` (modify — `DbSet<CompanyProfile> CompanyProfiles`)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/*_AddCompanyProfile.cs` (+ `.Designer.cs`, `AppDbContextModelSnapshot.cs`) (generated)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs` (modify — permission row, ACCOUNTANT default list)
- `backend/src/OrderMgmt.Application/Organization/Company/Interfaces/ICompanyProfileService.cs` (new)
- `backend/src/OrderMgmt.Application/Organization/Company/Models/CompanyProfileDtos.cs` (new)
- `backend/src/OrderMgmt.Application/Organization/Company/Services/CompanyProfileService.cs` (new)
- `backend/src/OrderMgmt.Application/Organization/Company/Validators/CompanyProfileValidators.cs` (new)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` (modify)
- `backend/src/OrderMgmt.WebApi/Controllers/CompanyProfileController.cs` (new)
- `backend/src/OrderMgmt.Application/Common/Interfaces/ISpreadsheetPdfConverter.cs` (new, replaces `Sales/Quotations/Interfaces/IQuotationSpreadsheetPdfConverter.cs`, which is deleted)
- `backend/src/OrderMgmt.Infrastructure/Excel/LibreOfficeSpreadsheetPdfConverter.cs`, `backend/src/OrderMgmt.Infrastructure/DependencyInjection.cs`, `backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/Quotations/QuotationExportTests.cs`, `HandoverExportTests.cs` (modify — interface name)
- `backend/tests/OrderMgmt.IntegrationTests/Tt133/Unit/VndAmountInWordsTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Tt133/Unit/LibreOfficeConverterTimeoutTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Organization/CompanyProfileTests.cs` (new)

Frontend:
- `frontend/src/features/company-profile/api.ts`, `hooks.ts`, `keys.ts`, `types.ts` (new)
- `frontend/src/pages/settings/company-profile-page.tsx`, `company-profile-page.test.tsx` (new)
- `frontend/src/pages/settings/settings-hub-page.tsx`, `settings-hub-page.test.tsx` (modify)
- `frontend/src/App.tsx`, `frontend/src/lib/permissions.ts`, `frontend/src/lib/route-permissions.ts` (modify)

## Reference files (read-only)

- `backend/src/OrderMgmt.Application/CashDebt/Settings/` and `backend/src/OrderMgmt.WebApi/Controllers/CashSettingsController.cs` — the one-row settings pattern (service, DTOs, validator, controller)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/CashDebtConfiguration.cs` (`CashDebtSettingsConfiguration`) — `HasData` for the single row
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/CashSettingsTests.cs` — test shape
- `frontend/src/features/cash-settings/`, `frontend/src/pages/settings/cash-settings-page.tsx` and its test — page shape

## Contracts

```csharp
// Application/Common/VndAmountInWords.cs
public static class VndAmountInWords { public static string Read(decimal amount); }

// Domain/Entities/Organization/CompanyProfile.cs — one row, Id = 1, not a BaseEntity (like CashDebtSettings)
public class CompanyProfile
{
    public int Id { get; set; }
    public string? Name { get; set; }                      // max 255
    public string? Address { get; set; }                   // max 500
    public string? TaxCode { get; set; }                   // max 20
    public string? ChiefAccountantName { get; set; }       // max 150
    public string? LegalRepresentativeName { get; set; }   // max 150
    public string? AccountingServiceNote { get; set; }     // max 500
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

// Permissions.cs
public static class CompanySettings { public const string Manage = "settings.company"; }

// Application/Organization/Company/Models/CompanyProfileDtos.cs
public class CompanyProfileDto { Name, Address, TaxCode, ChiefAccountantName, LegalRepresentativeName, AccountingServiceNote (string?), DateTimeOffset UpdatedAt }
public class UpdateCompanyProfileRequest { the six string? fields }

// ICompanyProfileService
Task<CompanyProfileDto> GetAsync(CancellationToken ct = default);
Task<CompanyProfileDto> UpdateAsync(UpdateCompanyProfileRequest request, CancellationToken ct = default);
```

| Method | Route | Auth | Response |
|---|---|---|---|
| GET | `api/settings/company` | `[Authorize]` (any signed-in user, T13) | `CompanyProfileDto` |
| PUT | `api/settings/company` | `[HasPermission(Permissions.CompanySettings.Manage)]` | `CompanyProfileDto` |

Table `company_profile`, key `Id` with `ValueGeneratedNever()`, `HasData(new { Id = 1, UpdatedAt = DateTimeOffset.UnixEpoch })`.

Update rules: every string is trimmed and an empty string is saved as null; `UpdatedAt = clock.UtcNow`, `UpdatedBy = currentUser.UserId`.

Validator (`UpdateCompanyProfileRequestValidator`):
- `Name`: not empty → "Vui lòng nhập tên đơn vị."; max 255 → "Tên đơn vị tối đa 255 ký tự."
- `Address`: max 500 → "Địa chỉ tối đa 500 ký tự."
- `TaxCode`: when not empty, matches `^\d{10}(-\d{3})?$` → "Mã số thuế gồm 10 số, hoặc 10 số, dấu gạch và 3 số."
- `ChiefAccountantName`, `LegalRepresentativeName`: max 150 → "Họ tên tối đa 150 ký tự."
- `AccountingServiceNote`: max 500 → "Ghi chú tối đa 500 ký tự."

### `VndAmountInWords.Read` rules (T17)

1. `n = Math.Round(Math.Abs(amount), 0, MidpointRounding.AwayFromZero)`. `n == 0` → `"Không đồng"`.
2. Split `n` into chunks of 9 digits from the right. Chunk `i` (0 = lowest) is followed by the word "tỷ" repeated `i` times. A chunk equal to 0 is skipped with its "tỷ" words.
3. Inside a chunk, split into three groups of 3 digits: triệu, nghìn, units. A group equal to 0 is skipped with its unit word.
4. A 3-digit group `h t u` is read **in full** when it is not the first group read of the whole number, otherwise without leading zeros:
   - hundreds: `h > 0` → "{h} trăm"; `h == 0` and in full → "không trăm"; else nothing;
   - tens: `t == 0` → "linh" when `u > 0` and the hundreds were read, else nothing; `t == 1` → "mười"; `t >= 2` → "{t} mươi";
   - units: `u == 0` → nothing; `u == 1` → "mốt" when `t >= 2`, else "một"; `u == 5` → "lăm" when `t >= 1`, else "năm"; otherwise the digit word (4 is "bốn").
5. Digit words: không, một, hai, ba, bốn, năm, sáu, bảy, tám, chín. Join with single spaces, capitalise the first letter, append " đồng".

## Tasks

### Task 1.1 — `VndAmountInWords`

1. **Write the failing test** `Tt133/Unit/VndAmountInWordsTests.cs` (namespace `OrderMgmt.IntegrationTests.Tt133.Unit`, no collection): a `[Theory]` `Reads_amount(string amount, string expected)` parsing `amount` with `decimal.Parse(amount, CultureInfo.InvariantCulture)`:

   | Amount | Expected |
   |---|---|
   | 0 | Không đồng |
   | 5 | Năm đồng |
   | 10 | Mười đồng |
   | 11 | Mười một đồng |
   | 15 | Mười lăm đồng |
   | 20 | Hai mươi đồng |
   | 21 | Hai mươi mốt đồng |
   | 24 | Hai mươi bốn đồng |
   | 25 | Hai mươi lăm đồng |
   | 101 | Một trăm linh một đồng |
   | 105 | Một trăm linh năm đồng |
   | 110 | Một trăm mười đồng |
   | 115 | Một trăm mười lăm đồng |
   | 1000 | Một nghìn đồng |
   | 1500 | Một nghìn năm trăm đồng |
   | 1000005 | Một triệu không trăm linh năm đồng |
   | 1005000 | Một triệu không trăm linh năm nghìn đồng |
   | 1500000 | Một triệu năm trăm nghìn đồng |
   | 2000000000 | Hai tỷ đồng |
   | 2000000005 | Hai tỷ không trăm linh năm đồng |
   | 1000000000000 | Một nghìn tỷ đồng |
   | 1000000000000000000 | Một tỷ tỷ đồng |
   | -105 | Một trăm linh năm đồng |
   | 1499.5 | Một nghìn năm trăm đồng |
2. **Run the test to verify it fails:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~VndAmountInWordsTests"`. Expected: FAIL — build error, `VndAmountInWords` does not exist.
3. **Write the minimal implementation** of `VndAmountInWords.Read` following rules 1–5.
4. **Run the test to verify it passes:** same filter. Expected: PASS (24 cases).
5. **Commit:** `git add backend/src/OrderMgmt.Application/Common/VndAmountInWords.cs backend/tests/OrderMgmt.IntegrationTests/Tt133/Unit/VndAmountInWordsTests.cs && git commit -m "feat(tt133): VND amount in words"`

### Task 1.2 — Company profile: entity, migration, GET/PUT

1. **Write the failing tests** `Organization/CompanyProfileTests.cs` (`[Collection(nameof(PostgresCollection))]`, inherits `InventoryTestBase`):
   - `Profile_is_empty_by_default_and_update_persists`: `GET /api/settings/company` as a SALES client (`CreateClientForRoleAsync("cp_sales", RoleCodes.Sales)`) → 200, every text field null. `PUT` as admin (`_client`) with `Name = "  Công ty TNHH ABC  "`, `Address = "12 Lê Lợi, Hà Nội"`, `TaxCode = "0101234567"`, `ChiefAccountantName = "Trần B"`, `LegalRepresentativeName = "Nguyễn A"`, `AccountingServiceNote = ""` → 200 with `Name == "Công ty TNHH ABC"` (trimmed) and `AccountingServiceNote == null`. `GET` again returns the same values and an `UpdatedAt` after the Unix epoch. In a `finally`, reset the row through `InDbAsync` (all six fields null), because the template clone is shared inside the test class.
   - `Profile_has_exactly_one_row`: `InDbAsync(db => db.CompanyProfiles.CountAsync())` is 1 before and after a `PUT`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CompanyProfileTests"`. Expected: FAIL — build error, `CompanyProfileDto` / `CompanyProfiles` do not exist.
3. **Write the minimal implementation:**
   - the entity, `CompanyProfileConfiguration` (table, key, max lengths, `HasData`), the two `DbSet` declarations;
   - `Permissions.CompanySettings.Manage`, and in `DbSeeder` the permission row `(Permissions.CompanySettings.Manage, Permissions.SystemModule, "Thông tin đơn vị")` next to `Permissions.System.ManageSettings` (ADMIN and MANAGER receive it through "all permissions"), and `Permissions.CompanySettings.Manage` added to the default list of `RoleCodes.Accountant` (T13; an existing ACCOUNTANT role receives it once through the seeder's "codes introduced by this release" step);
   - the migration: `dotnet ef migrations add AddCompanyProfile --project src/OrderMgmt.Infrastructure --startup-project src/OrderMgmt.WebApi -o Persistence/Migrations`; open the generated file and check it only creates `company_profile` and inserts the row with `id = 1`;
   - `ICompanyProfileService` / `CompanyProfileService` (the `CashDebtSettingsService` shape, with the trim rule), DTOs, an **empty** `UpdateCompanyProfileRequestValidator` class (its rules are Task 1.3), DI registration next to `ICashDebtSettingsService`;
   - `CompanyProfileController` (`[Route("api/settings")]`, actions `[HttpGet("company")]` and `[HttpPut("company")]` as in the table).
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git add <the backend files above> && git commit -m "feat(settings): company profile entity and API"`

### Task 1.3 — Company profile: validation and permission

1. **Write the failing tests** in `CompanyProfileTests.cs`:
   - `Update_validates_fields` (each case → 400 with the given `details` key): empty `Name` → `name`; `Name` of 256 characters → `name`; `TaxCode = "12345"` → `taxCode`; `TaxCode = "0101234567-01"` → `taxCode`; `ChiefAccountantName` of 151 characters → `chiefAccountantName`. `TaxCode = "0101234567-001"` with a valid name → 200.
   - `Update_requires_the_company_settings_permission`: a client with only `Permissions.Cash.Settings` (`CreateClientWithPermissionsAsync("cp_cash_only", null, Permissions.Cash.Settings)`) → `PUT` 403; a client with only `Permissions.CompanySettings.Manage` → 200.
   - `Seeded_roles`: through `InDbAsync`, the ADMIN, MANAGER and ACCOUNTANT roles hold `settings.company`; SALES and WAREHOUSE do not.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CompanyProfileTests"`. Expected: FAIL — the invalid requests return 200 (empty validator). The permission and seed tests may already pass; that is fine.
3. **Write the minimal implementation:** the validator rules from the contract; call `ValidateAndThrowAsync` in the `PUT` action if Task 1.2 left it out.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(settings): validate company profile"` (after `git add` of the validator, controller and test files).

### Task 1.4 — Rename the PDF converter interface

1. **Write the failing test (compile-level):** in `Quotations/QuotationExportTests.cs` and `Quotations/HandoverExportTests.cs`, replace every `IQuotationSpreadsheetPdfConverter` with `ISpreadsheetPdfConverter` and add `using OrderMgmt.Application.Common.Interfaces;` where it is missing.
2. **Run the tests to verify they fail:** `dotnet build OrderMgmt.sln`. Expected: FAIL — build error, `ISpreadsheetPdfConverter` does not exist.
3. **Write the minimal implementation:** create `Application/Common/Interfaces/ISpreadsheetPdfConverter.cs` with the same single method; delete `Sales/Quotations/Interfaces/IQuotationSpreadsheetPdfConverter.cs`; update `LibreOfficeSpreadsheetPdfConverter`, `Infrastructure/DependencyInjection.cs` and `QuotationService` (field and constructor parameter types only). Do not change `QuotationExportOptions` or its section name.
4. **Run the tests to verify they pass:** `dotnet build OrderMgmt.sln`, then `--filter "FullyQualifiedName~QuotationExportTests|FullyQualifiedName~HandoverExportTests"`. Expected: the same pass/fail set as `BASELINE.md` for these two classes (their PDF tests may be baseline failures; no test changes state).
5. **Commit:** `git add -u backend/src backend/tests && git add backend/src/OrderMgmt.Application/Common/Interfaces/ISpreadsheetPdfConverter.cs && git commit -m "refactor(export): rename the PDF converter interface to ISpreadsheetPdfConverter"` (check `git status` first: only the files of this task may be staged).

### Task 1.5 — The converter kills LibreOffice on timeout (T23)

Today `LibreOfficeSpreadsheetPdfConverter` stops waiting when `ConversionTimeoutSeconds` passes, but the `soffice` process keeps running and keeps the default user profile, so the conversions after it can hang. The bulk forms of this plan make a timeout likely; fix it before they exist.

1. **Write the failing test** `Tt133/Unit/LibreOfficeConverterTimeoutTests.cs` (namespace `OrderMgmt.IntegrationTests.Tt133.Unit`, no collection), test `Timeout_kills_the_process`:
   - create a temp folder and, in it, a script that ignores its arguments, waits about 5 seconds and then writes a marker file: on Windows `slow.cmd` (`@echo off`, `ping -n 6 127.0.0.1 > nul`, `echo done > "<folder>\marker.txt"`), elsewhere `slow.sh` (`#!/bin/sh`, `sleep 5`, `echo done > "<folder>/marker.txt"`, made executable with `File.SetUnixFileMode`); choose with `OperatingSystem.IsWindows()`;
   - `new LibreOfficeSpreadsheetPdfConverter(Options.Create(new QuotationExportOptions { LibreOfficePath = <script>, ConversionTimeoutSeconds = 1 }))`;
   - `ConvertAsync(new byte[] { 1 })` throws an `OperationCanceledException` (use `Assert.ThrowsAnyAsync`) in less than 4 seconds;
   - after `await Task.Delay(TimeSpan.FromSeconds(7))` the marker file does not exist (the process tree was killed before it could write it);
   - delete the temp folder in a `finally`.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~LibreOfficeConverterTimeoutTests"`. Expected: FAIL — the marker file exists (the script outlived the timeout).
3. **Write the minimal implementation:** in `ConvertAsync`, wrap `await process.WaitForExitAsync(cts.Token)` in a `try` / `catch (OperationCanceledException)` that calls `process.Kill(entireProcessTree: true)` inside its own `try` / `catch` (the process may have just exited) and rethrows. Nothing else changes: no profile per conversion and no semaphore (review S3 stays rejected).
4. **Run the test to verify it passes:** same filter, then `--filter "FullyQualifiedName~QuotationExportTests|FullyQualifiedName~HandoverExportTests"`. Expected: PASS for the new test; the two quotation classes as in `BASELINE.md`.
5. **Commit:** `git add backend/src/OrderMgmt.Infrastructure/Excel/LibreOfficeSpreadsheetPdfConverter.cs backend/tests/OrderMgmt.IntegrationTests/Tt133/Unit/LibreOfficeConverterTimeoutTests.cs && git commit -m "fix(export): kill LibreOffice when a PDF conversion times out"`

### Task 1.6 — Frontend: Thông tin đơn vị page

1. **Write the failing tests:**
   - `pages/settings/company-profile-page.test.tsx` (mock `@/features/company-profile/hooks`):
     - renders the six fields with the loaded values under the labels "Tên đơn vị", "Địa chỉ", "Mã số thuế", "Kế toán trưởng", "Người đại diện theo pháp luật", "Ghi chú dịch vụ kế toán";
     - submitting calls the update mutation with the edited values and shows the toast "Đã lưu thông tin đơn vị";
     - an empty "Tên đơn vị" shows "Vui lòng nhập tên đơn vị" and does not call the mutation;
     - a server error with `details.taxCode` shows that message under the tax code field.
   - `pages/settings/settings-hub-page.test.tsx`: a user with `settings.company` sees the card "Thông tin đơn vị" linking to `/settings/company`; a user without it does not.
   - a `lib/route-permissions` test (add to the existing test file of that module, or create `lib/route-permissions.test.ts` if none exists): `/settings/company` needs `settings.company`.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/settings src/lib`. Expected: FAIL — `company-profile-page` cannot be imported.
3. **Write the minimal implementation:**
   - `features/company-profile/types.ts` (`CompanyProfile` with the six `string | null` fields and `updatedAt?: string`; `UpdateCompanyProfileRequest = Omit<CompanyProfile, 'updatedAt'>`), `api.ts` (`apiGet`/`apiPut` on `/settings/company`), `keys.ts` (`companyProfileKeys = { all: ['company-profile'] as const }`, a global singleton outside the `['cash']` root), `hooks.ts` (`useCompanyProfile`, `useUpdateCompanyProfile` invalidating `companyProfileKeys.all`);
   - `pages/settings/company-profile-page.tsx` in the shape of `cash-settings-page.tsx`: title "Thông tin đơn vị", description "Tên, địa chỉ, mã số thuế và người ký in trên các sổ theo mẫu TT133", React Hook Form + Zod (name required, tax code pattern `^\d{10}(-\d{3})?$`, the max lengths of the backend), server `details` mapped to the fields with `getApiError`;
   - `'settings.company'` in `lib/permissions.ts`; the rule `{ pattern: /^\/settings\/company$/, permission: 'settings.company' }` in `lib/route-permissions.ts`; the route `settings/company` in `App.tsx` inside `<ProtectedRoute permission="settings.company">`; the hub card (`to: '/settings/company'`, `permission: 'settings.company'`, icon `Building2`, title "Thông tin đơn vị", description "Tên, mã số thuế, người ký trên sổ sách") as the first card.
4. **Run the tests to verify they pass:** `npx vitest run src/pages/settings src/lib`, then `npm run typecheck`. Expected: PASS.
5. **Commit:** `git add <the frontend files above> && git commit -m "feat(frontend): company profile settings page"`

### Task 1.7 — Phase checkpoint

1. Run `dotnet test OrderMgmt.sln --logger "trx;LogFileName=current.trx"` and compare the failures with `BASELINE.md`; run `npm run lint`, `npm run test`, `npm run build` and compare.
2. If a new failure appears, write a focused failing test for it, fix it, re-run.
3. Commit only if a fix was needed: `git commit -m "fix(settings): <what>"`.

## Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~VndAmountInWordsTests|FullyQualifiedName~CompanyProfileTests|FullyQualifiedName~LibreOfficeConverterTimeoutTests"
dotnet test OrderMgmt.sln   # compare with BASELINE.md
cd ../frontend
npm run typecheck && npm run lint && npm run test && npm run build
```

## Exit Criteria

- `VndAmountInWords.Read` passes the 24 cases.
- `GET/PUT api/settings/company` work with the validation and permission rules; the migration creates one table with one row; ADMIN, MANAGER and ACCOUNTANT hold `settings.company`.
- No type named `IQuotationSpreadsheetPdfConverter` remains (`grep -rn IQuotationSpreadsheetPdfConverter backend/src backend/tests` is empty).
- A PDF conversion that times out leaves no LibreOffice process behind.
- The Thông tin đơn vị page saves the profile and is reachable from the settings hub.
- No new failures against `BASELINE.md`.
