# Phase 02d — Lane D: frontend

**Status:** [ ] pending
**Complexity:** M
**Runs in parallel with:** 02a, 02b, 02c · **Worktree:** `.claude/worktrees/sd-fe` on `feat/branch-sample-data-fe` from `$P01` (run `npm ci` in `frontend/` first)

## Objective

- **Create dialog:** the **"Tạo kèm dữ liệu mẫu để đào tạo"** checkbox.
- **Branches page:**
  - a **Loại** column with the Demo / progress / error badge;
  - polling while a branch is seeding;
  - for demo branches, a **"Xóa chi nhánh demo"** action whose dialog shows what will be deleted and requires typing the branch code.
- **Markers:** "(demo)" after demo branches in the header branch switcher and in the executive dashboard scope select.

The backend contract is in SUMMARY "Frontend contract" and "Endpoints". This lane mocks the API, so it does not wait for Lanes B and C.

## Files (this lane only — touch nothing else)

- `frontend/src/features/branches/types.ts`, `api.ts`, `hooks.ts` (modify)
- `frontend/src/features/branches/sample-data.ts`, `sample-data.test.ts` (new)
- `frontend/src/pages/settings/branch-form-dialog.tsx` (modify), `branch-form-dialog.test.tsx` (new)
- `frontend/src/pages/settings/branches-page.tsx`, `branches-page.test.tsx` (modify)
- `frontend/src/pages/settings/demo-branch-delete-dialog.tsx`, `demo-branch-delete-dialog.test.tsx` (new)
- `frontend/src/components/layout/header/header-branch-switcher.tsx`, `__tests__/header-branch-switcher.test.tsx` (modify)
- `frontend/src/pages/executive-dashboard/components/scope-select.tsx`, `scope-select.test.tsx` (modify)

## Reference files (read-only)

- `frontend/src/pages/settings/branches-page.test.tsx`:
  - hooks and api modules are mocked wholesale. **Add every new hook and api method to those `vi.mock` factories**, or the page crashes with "not a function";
  - auth store mock; `renderPage()`.
- `frontend/src/pages/funds/fund-form-dialog.tsx:152-158` — the native checkbox with `form.register` (no Checkbox UI component exists).
- `frontend/src/pages/funds/fund-list-page.tsx:55,70-74` — `<Badge variant="info">` in a cell; Badge variants `info | warning | destructive | success | …`.
- `frontend/src/components/ui/dialog.tsx` (Dialog, DialogContent, DialogHeader, DialogFooter, DialogTitle, DialogDescription); `frontend/src/components/ui/confirm-dialog.tsx` (shape to copy; it has no slot for an input).
- `frontend/src/features/notifications/hooks.ts:9-18` — the only `refetchInterval` today. TanStack Query v5 accepts `refetchInterval: (query) => number | false`.
- `frontend/src/lib/api-client.ts` — `apiGet`, `apiDelete<T>`, `getErrorMessage`, `formatApiErrorDetails`, `getApiError`.
- `frontend/src/lib/use-toast.ts` — `toast({ variant: 'success' | 'destructive', title, description })`.

## UI text (exact)

**Create dialog checkbox** (create mode only, under Địa chỉ):
- label: "Tạo kèm dữ liệu mẫu để đào tạo";
- hint (`text-xs text-muted-foreground`): "Tạo danh mục mẫu, tồn kho, công nợ, số dư quỹ đầu kỳ và khoảng 3 tháng chứng từ. Chi nhánh được đánh dấu Demo và xóa được bằng nút \"Xóa chi nhánh demo\"."

**Toast after creating with the flag:** title "Đã tạo chi nhánh demo", description "Dữ liệu mẫu đang được tạo, có thể mất khoảng một phút." Without the flag the toast is unchanged ("Đã tạo chi nhánh").

**Loại column** (between Địa chỉ and Khóa sổ đến):
- normal branch: empty;
- demo `Ready`: `<Badge variant="info">Demo</Badge>`;
- demo `Running`: Demo + `<Badge variant="warning">Đang tạo dữ liệu {progress}%</Badge>`;
- demo `Failed`: Demo + `<Badge variant="destructive" title={sampleDataError}>Lỗi tạo dữ liệu</Badge>`.

**Actions for a demo branch:** the Trash button has aria-label "Xóa chi nhánh demo" and opens the demo dialog instead of the normal confirm. While `Running` it is disabled with `title="Đang tạo dữ liệu mẫu"`. Normal branches keep "Xóa chi nhánh" and the existing `ConfirmDialog`.

**Demo delete dialog:**
- **Title:** "Xóa chi nhánh demo?"
- **Body:**
  - while the preview loads: "Đang kiểm tra dữ liệu...";
  - then: "Toàn bộ dữ liệu của chi nhánh **{code} — {name}** sẽ bị xóa vĩnh viễn: {documentCount} chứng từ, tồn kho và công nợ đầu kỳ, kho, quỹ. Chứng từ do người dùng tự nhập vào chi nhánh này cũng bị xóa.";
  - when `reassignedUserCount > 0`, a second paragraph: "{n} người dùng đang có chi nhánh mặc định là chi nhánh này sẽ được chuyển về Chi nhánh chính.";
  - a preview error shows `getErrorMessage(err)` in the destructive box style of the page.
- **Input:** label "Nhập mã chi nhánh ({code}) để xác nhận", id `demo-delete-code`.
- **Buttons:**
  - "Hủy" (outline);
  - "Xóa vĩnh viễn" (destructive), disabled until the trimmed input equals `code` (case-sensitive) and the preview has loaded; while deleting the label is "Đang xóa..." and both buttons are disabled.
- **Success toast:**
  - title "Đã xóa chi nhánh demo";
  - when `keptSampleProductCount + keptSamplePartnerCount > 0`, description "Giữ lại {p} hàng hóa và {q} đối tượng mẫu vì đang được dùng ở nơi khác.".

  Then close, and call `onDeleted(branch.id)`.
- **Error toast:** title "Không thể xóa", description `formatApiErrorDetails(err)`.

**Markers:**
- header switcher items: `{code} — {name}`, plus `" (demo)"` when `isDemo`;
- executive scope select items: the branch label it shows today, plus `" (demo)"` when `isDemo`.

## Tasks

### Task D.1 — Types, api, hooks, polling rule

1. **Write the failing test** `features/branches/sample-data.test.ts`:
   - `hasRunningSampleData(undefined)` is false;
   - with `[{ sampleDataStatus: 'Ready' }, { sampleDataStatus: 'None' }]` it is false;
   - with one `'Running'` it is true;
   - `branchesRefetchInterval(branches)` returns `2000` when one is running, else `false`.
2. **Run the test to verify it fails:** `npx vitest run src/features/branches/sample-data.test.ts`. Expected: FAIL — module `./sample-data` not found.
3. **Write the minimal implementation:**
   - `sample-data.ts` with the two functions.
   - The contract types in `types.ts`.
   - `api.ts`:
     - `deletionPreview: (id: string) => apiGet<DemoBranchDeletionPreview>(`/branches/${id}/demo-deletion`)`;
     - `removeDemo: (id: string) => apiDelete<DemoBranchDeletionResult>(`/branches/${id}/demo`)`.
   - `hooks.ts`:
     - `useBranches()` gains `refetchInterval: (query) => branchesRefetchInterval(query.state.data)` and `refetchIntervalInBackground: false`;
     - `useDemoBranchDeletionPreview(id: string | null)` (`queryKey: [...branchKeys.all, 'demo-deletion', id]`, `enabled: !!id`, `staleTime: 0`);
     - `useDeleteDemoBranch()` (`mutationFn: branchesApi.removeDemo`, `onSuccess: invalidateBranches`).
4. **Run the test to verify it passes:** same command, then `npm run typecheck`. Expected: PASS.
5. **Commit:** `git add frontend/src/features/branches && git commit -m "feat(frontend): branch sample data types, api and polling"`

### Task D.2 — Checkbox in the create dialog

1. **Write the failing tests** `pages/settings/branch-form-dialog.test.tsx`. Mock `@/features/branches/hooks` (`useCreateBranch`, `useUpdateBranch`, `useSetBranchOpeningDate`) and `@/lib/use-toast`; render inside `QueryClientProvider`.
   - `create mode shows the sample data checkbox, unchecked`: `getByLabelText('Tạo kèm dữ liệu mẫu để đào tạo')` is not checked; the hint text is visible.
   - `creating with the box ticked sends withSampleData and shows the demo toast`:
     - fill code `CN09` and name, tick, submit;
     - `createMock` called with `{ code: 'CN09', name: …, address: null, withSampleData: true }`;
     - toast title "Đã tạo chi nhánh demo".
   - `creating without the box sends withSampleData false`: the toast title stays "Đã tạo chi nhánh".
   - `edit mode has no checkbox`: render with `initial` → `queryByLabelText('Tạo kèm dữ liệu mẫu để đào tạo')` is null.

   Also update the existing create assertion in `branches-page.test.tsx` (`createMock` called with `{ code, name, address }`) to include `withSampleData: false`.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/settings`. Expected: FAIL — the checkbox label is not found, and the page test's payload differs.
3. **Write the minimal implementation:**
   - `withSampleData: z.boolean()` in `editSchema` (default `false` in `toDefaults`);
   - the native checkbox block inside `{!isEdit && (…)}`;
   - send `withSampleData` only on create;
   - the toast text per the flag.
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): create a branch with sample data"` (after `git add` of the dialog and the two test files).

### Task D.3 — Loại column and demo actions on the branches page

1. **Write the failing tests** in `branches-page.test.tsx`:
   - Extend the `BRANCHES` fixture with:
     - `{ id: 'b3', code: 'DEMO1', name: 'CN demo', isDemo: true, sampleDataStatus: 'Ready', sampleDataProgress: 100 }`;
     - `{ id: 'b4', code: 'DEMO2', …, isDemo: true, sampleDataStatus: 'Running', sampleDataProgress: 45 }`;
     - `{ id: 'b5', code: 'DEMO3', …, isDemo: true, sampleDataStatus: 'Failed', sampleDataError: 'XBH-0012: lỗi' }`.
   - Add `useDemoBranchDeletionPreview` and `useDeleteDemoBranch` to the hooks mock.
   - Tests:
     - `shows the demo badges`:
       - the `DEMO1` row has "Demo";
       - the `DEMO2` row has "Đang tạo dữ liệu 45%";
       - the `DEMO3` row has "Lỗi tạo dữ liệu" with `title="XBH-0012: lỗi"`;
       - a normal branch row has no "Demo".
     - `demo rows use the demo delete action`: the `DEMO1` row has a button "Xóa chi nhánh demo" and no "Xóa chi nhánh". Clicking it opens a dialog titled "Xóa chi nhánh demo?".
     - `demo delete is disabled while seeding`: the `DEMO2` row's "Xóa chi nhánh demo" is disabled.
     - `normal rows keep the normal delete`: the normal row still has "Xóa chi nhánh" and opens "Xóa chi nhánh?".
2. **Run the tests to verify they fail:** `npx vitest run src/pages/settings/branches-page.test.tsx`. Expected: FAIL — no "Demo" text.
3. **Write the minimal implementation:**
   - the column (rules in "UI text");
   - the per-row action choice;
   - a `pendingDemoDelete: Branch | null` state rendering `<DemoBranchDeleteDialog branch={pendingDemoDelete} onClose={…} onDeleted={(id) => void leaveDeletedWorkingBranch(id)} />`;
   - for this task, create `demo-branch-delete-dialog.tsx` with only the title and a Hủy button (its behaviour is D.4).
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): demo badges and demo delete action on the branches page"` (after `git add`).

### Task D.4 — Demo delete dialog

1. **Write the failing tests** `pages/settings/demo-branch-delete-dialog.test.tsx`:
   - Mock `@/features/branches/hooks`:
     - `useDemoBranchDeletionPreview` returns `{ data: { documentCount: 250, reassignedUserCount: 2 }, isLoading: false, isError: false }`;
     - `useDeleteDemoBranch` returns `{ mutateAsync: deleteMock, isPending: false }`.
   - Tests:
     - `shows what will be deleted`: the text contains "250 chứng từ" and "2 người dùng đang có chi nhánh mặc định là chi nhánh này sẽ được chuyển về Chi nhánh chính.".
     - `confirm needs the exact code`:
       - "Xóa vĩnh viễn" is disabled;
       - typing `demo1` keeps it disabled (case-sensitive);
       - typing `DEMO1` (with surrounding spaces, trimmed) enables it.
     - `deleting calls the mutation, toasts and reports back`:
       - `deleteMock` resolves `{ reassignedUserCount: 2, keptSampleProductCount: 1, keptSamplePartnerCount: 0 }`;
       - after confirm, `deleteMock` was called with `'b3'`;
       - toast `{ variant: 'success', title: 'Đã xóa chi nhánh demo', description: 'Giữ lại 1 hàng hóa và 0 đối tượng mẫu vì đang được dùng ở nơi khác.' }`;
       - `onDeleted` called with `'b3'`, `onClose` called.
     - `no description when nothing is kept`: kept counts 0 → toast without `description`.
     - `server error toasts and keeps the dialog`: `deleteMock` rejects with the 409 shape of `branches-page.test.tsx:150-154`, code `SAMPLE_DATA_RUNNING` → toast "Không thể xóa"; the dialog stays open.
     - `loading preview`: `isLoading: true` → "Đang kiểm tra dữ liệu..." and the confirm button stays disabled even with the right code.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/settings/demo-branch-delete-dialog.test.tsx`. Expected: FAIL — the body text is not rendered (D.3 left only the title).
3. **Write the minimal implementation** of the dialog per "UI text", built from `components/ui/dialog.tsx`, with an `Input` and `Label` (`showClose={false}`).
4. **Run the tests to verify they pass:** same command, then `npx vitest run src/pages/settings`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): type-the-code dialog to delete a demo branch"` (after `git add`).

### Task D.5 — "(demo)" markers

1. **Write the failing tests:**
   - `components/layout/header/__tests__/header-branch-switcher.test.tsx`: with `useMyBranches` returning a demo branch `{ code: 'DEMO1', name: 'CN demo', isDemo: true }` and a normal one, opening the switcher shows the item "DEMO1 — CN demo (demo)", and the normal item has no "(demo)". Follow the file's existing mocks.
   - `pages/executive-dashboard/components/scope-select.test.tsx`: with the scope's branches holding a demo branch, its option text ends with "(demo)"; "Toàn công ty" and normal branches are unchanged.
2. **Run the tests to verify they fail:** `npx vitest run src/components/layout/header src/pages/executive-dashboard/components/scope-select.test.tsx`. Expected: FAIL — no "(demo)".
3. **Write the minimal implementation:** append `{b.isDemo ? ' (demo)' : ''}` in both item renderers.
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): mark demo branches in the branch pickers"` (after `git add`).

### Task D.6 — Lane checkpoint

1. From `frontend/`: `npm run typecheck`, `npm run lint`, `npm run test -- --testTimeout=30000`, `npm run build`. Then `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo` (skip untracked ones). Compare the failures and lint counts with `BASELINE.md`.
2. Fix a new failure with a focused failing test first; commit `fix(frontend): <what>`.

## Verification

```bash
cd /D/Projects/QLBanHang/.claude/worktrees/sd-fe/frontend
npx vitest run src/features/branches src/pages/settings src/components/layout/header src/pages/executive-dashboard/components/scope-select.test.tsx
npm run typecheck && npm run lint && npm run test -- --testTimeout=30000 && npm run build
```

## Exit Criteria

- The checkbox sends `withSampleData`; the toast tells the user the data is being created.
- The branches page shows the demo badges and refreshes every 2 s while any branch is `Running`, and only then.
- Demo branches are deleted only through the type-the-code dialog, which shows the preview counts and the kept-catalog note.
- Both branch pickers mark demo branches.
- No new failures or lint findings against `BASELINE.md`; only the files listed above changed.
