# Execution report: Executive dashboard (shared base and Director view)

- **Plan:** [SUMMARY.md](SUMMARY.md). Brainstorm: [`docs/brainstorms/261013-2247-executive-dashboard/SUMMARY.md`](../../../brainstorms/261013-2247-executive-dashboard/SUMMARY.md).
- **Branch:** `feat/executive-dashboard`, local only (not pushed). It was forked from `main` at `a76ce85`; `main` at `8487d5e` (TT133 report forms) was merged in.
- **Executed:** 2026-10-14, from 00:15 to about 15:45. The user paused the work from 08:25 to 09:15 for a machine shutdown, and the controller session restarted once at about 14:58.
- **Tracks:** from 09:15 the phases ran in three parallel tracks at the user's request ("tăng tốc"): backend, real-time and frontend. Each track had its own worktree and its own test database. The tracks were merged through `feat/executive-dashboard-int`.
- **Implementers:** opus implementers did the work. Each phase had a two-stage review (spec compliance, then quality with mutation analysis). The whole feature then went through a final review workflow: seven lenses, an adversarial verifier per lens, and a completeness critic.

## Result

All nine phases are complete, and the branch is ready for review.

- **Backend:** the module `Application/Dashboards/Executive` exposes 13 read-only routes under `api/dashboard/executive`. They cover sales, cash, debt, inventory, branch comparison, attention items and recent documents, scoped to the whole company or one branch and gated by permission.
- **Real-time:** `[DashboardSignal]` on ten document controllers pushes `DashboardChanged { branchId, domains }` to the `dashboard-all` and `dashboard-branch-{id}` groups.
- **Frontend:** `/dashboard/executive` has a KPI band, a Tổng quan tab and four deep-dive tabs (Kinh doanh, Tiền, Công nợ, Kho). The page has range presets, a branch filter and live refresh (signals batched for one second, with a 60-second fallback).

Verification matches `BASELINE.md`: no new failing test, and lint, typecheck and build are unchanged (see below). None of the manual browser checks were done. Four business questions remain open; the two about gross profit must be answered before the gross profit figures go to users (see Residual risks).

## Phases and commits

| Phase | Content | Commits |
|---|---|---|
| 00 | Workspace, plan commit, failure baseline (`BASELINE.md`: 19 backend and 3 frontend known failures) | `39290d3`, `8ce18a2`, `f2766dd` |
| 01 | Permission `dashboard.executive`; period, delta and series helpers; scope resolver and validation; sales summary, series and top lists with cost, gross profit and the provisional flag | `e28faf5` `8020c37` `9af7b4a` `62e18bd` `4220c18` `c0098ff` `d0e47bf`; review fixes `4eddc56` `6b910c1` `d5aa57b` `2c03527` `b2b539d` |
| 02 | Cash summary, series and breakdown; debt position math; debt summary and breakdown | `4f4b9fe` `3a7e9ef` `9cae03b` `431961b` `83058ae` `32d4cdc`; review fixes `7136c29` `98d3c3d` |
| 03 | Stock value split shared with the reports; inventory summary and breakdown; branch comparison; attention; recent documents | `78282f0` `6259ad4` `c40c1c4` `5021f45` `5c9b1b5` `1b02581`; review fixes `e47dd08` |
| 04 | Dashboard domains and hub groups; notifier; `[DashboardSignal]` on the ten document controllers; hub group join per branch; hardening | `014adad` `72dce30` `b68ca77` `6fe356c` `738b033` |
| 05 | Route and sidebar item; week and year presets; compact formats; data layer; hub signal on the client; live refresh; URL state, scope and access; tile kit | `a636ad6` `d68a227` `10607c3` `de8e883` `0a23793` `119473e` `d5c2274` `edba184`; review fixes `781590b` |
| 06 | Links and period helpers; page shell; KPI row; table and document link; charts and tables; attention list and recent feed; Tổng quan tab | `43f2ca1` `7d94c31` `c5fe69b` `41a1148` `492e7ab` `ab2dfc5` `dc31b88`; review fixes `e4764c7` `2039f15` |
| 07 | Kinh doanh, Tiền, Công nợ and Kho tabs; tab bar | `2e56464` `cb4dfc3` `2d66e45` `710b25c` `2f7d710` |
| 08 | Project docs, final review fixes, verification, this report | docs `18d1793` `cc48750` `e866288` `c7f9fdc`; final review fixes `f82b2b0` `b435cd5` `8c00161` `69ae297` (frontend), `2c55552` `417c7c7` (backend); this report |

Merges:
- into `feat/executive-dashboard-int`: `7d4b057` (real-time), `f354677` (`main`), `2d52244` (frontend at Phase 06), `cf218a0` (backend), `79a4486` (frontend);
- into `feat/executive-dashboard`: `b5abe9a` (int), `bd61b2d` (backend fixes), `287d3e5` (frontend fixes).

## Reviews

| Scope | Verdict | Outcome |
|---|---|---|
| Phase 01 | with fixes | Five follow-up commits: undefined enum numbers return 400 (measured; an implementer's unverified claim had said otherwise); discounts, VAT and previous-period cost; a total order for top lists; route guards. |
| Phase 02 | spec ✅, with fixes | A test as at an early date, non-zero NotDue, a validation theory over every route, shared helpers, and a five-way tie for the top-debtor order. |
| Phase 03 | spec ✅, with fixes | Sold-out product groups and products worth 0 or less are dropped from the lists. Added tests for count rules, ordering and mask-then-drop, plus the recent default limit. Branch names now come through `ExecutiveLookups`. |
| Phase 04 | ready to merge | Hardening in `738b033`. |
| Phase 05 | spec ✅, with fixes | Hub restart after `onclose`, no rebuild on token refresh, `keepPreviousData`, private `DEFAULT_PRESETS`. |
| Phase 06 | two spec deviations, with fixes | KPI group headings under partial access (subgrid), granularity reset A→B→A (shared `usePeriodGranularity`), stale KPI tiles, shared label helpers, segmented toggle, and a float rounding bug in the margin change. |
| Phase 07 and the whole feature | final review workflow, 15 agents | 32 findings. The verifiers refuted 5; 27 survived and the critic added 1. None critical. All were fixed except `realtime-02`, which is a follow-up (below). |

The final review's important findings and their fixes:
- **Grid blowout:** below 1024 px a wide table made every tile, and the page, scroll sideways. `TileCard` now has `min-w-0`, and every tab grid starts at `grid-cols-1`.
- **Frozen default period:** the default period was fixed when the page mounted, so a page left open past midnight kept querying the previous day. The new `useVnToday` re-renders at VN midnight.
- **Invalid custom ranges:** a custom range the API rejects filled the page with "Dữ liệu không hợp lệ.". `periodError` now mirrors the validator: "Áp dụng" is disabled, and a URL with an invalid period falls back to the default.
- **Test gap:** no test showed that other stock reasons stay out of revenue and cost. `Other_stock_reasons_are_not_sales` now covers system NKH, XKH and XTL and two user-created reasons.

The minor fixes:
- The branch list failing for an access_all user now leaves the scope unknown, with a retry message.
- The sales tab's `canViewCost` falls back to the loaded data.
- Chart labels follow the granularity of the data shown.
- The credit-limit links carry the working-branch hint.
- The custom range popover handles collisions at 375 px.
- KPI hints wrap.
- Charts have text alternatives.
- A margin change that rounds to zero shows "±0 điểm".
- A hub connection whose default branch cannot be read is closed, so the client shows "Mất kết nối" and retries instead of claiming "Trực tiếp".
- The docs were corrected.

## Deviations from the plan

- **Three parallel tracks** replaced sequential phases (SUMMARY P19), at the user's request. They merged without code conflicts. Merging `main` conflicted in two files:
  - `DependencyInjection.cs`: both registrations kept;
  - `InventoryReportService.cs`: main's stock summary and stock card targets kept; `SplitByQuantity` now lives in `Inventory/Common/StockValueSplit.cs`.
  
  In `nav-search.test.ts` both sides added a report item, so the count is now 16.
- **Phase 04 Task 4.5** (the full backend suite) moved to after the merge of the tracks.
- **Phase 03:**
  - Interface methods were added in the task that implements them, not grouped.
  - The test stock scenario is shared in `ExecutiveDashboardTestBase`.
  - Warehouse and product-group rows are masked first and then dropped when empty, as in the stock-on-hand report.
  - The top list holds only products with a value above 0.
- **Frontend contract amendments:**
  - `BranchDocumentLink` takes `workingBranchCode` as a prop.
  - `RecentFeed { items; workingBranchCode; showBranch }`.
  - `AttentionList.linkTitle`.
  - `DEFAULT_PRESETS` is module-private.
  - The X-axis label rule lives in `period.ts`, which also exports `periodError` and `seriesSpan`.
  - `usePeriodGranularity` lives in `features/executive-dashboard/series.ts`.
  - `StatTile` has `stale`, is a named `group` when it has no link, and its hint wraps.
  - The margin change is rounded in hundredths, half away from zero.
  - The notification hub is keyed on the user id, not on the token string.
  - `useExecutiveScope` returns `unknown` and `retry`.
  - The charts take `labelGranularity`.
- **Shared components changed for the other dashboards too:**
  - `useDashboardParams` (also used by the quotation, admin, revenue and sales-performance dashboards): the default period now follows the VN date, and it takes an optional `isValid`.
  - `RangePicker`: refuses a range whose "from" date is after its "to" date, and its custom range is a Radix popover with `aria-pressed` chips.
  - `TileCard`: has `min-w-0`.
  
  Their existing tests pass.
- **Credit-limit links** now carry the working-branch hint, reversing a Phase 06 choice that assumed the credit limits screen is not branch-scoped; it is (`CreditLimitService.ListAsync`).

## Verification against `BASELINE.md`

| Check | Result |
|---|---|
| Backend, full suite on `e866288` (final backend code) | 1281 tests, 1262 passed, 19 failed (identical to the 19 in `BASELINE.md`), 30 min 9 s |
| Backend, full suite on `b5abe9a` (before the final review fixes) | 1279 tests, 1260 passed, 19 failed (identical to the 19 in `BASELINE.md`), 40 min 45 s |
| Backend, final review fixes (targeted) | `ExecutiveDashboard` plus `Notifications`: 301 passed, 0 failed |
| Frontend typecheck | exit 0 |
| Frontend lint | 17 problems (3 errors, 14 warnings), the baseline; none in a file of this plan |
| Frontend tests | 1703 tests, 1700 passed, 3 failed: exactly the three in `BASELINE.md` (bank-accounts "adds a new account via the form", useNotificationHub "invalidates unread-count query on NewNotification", payment-qr "generates and displays a QR…"). The frontend files of `feat/executive-dashboard` are identical to those of the run (`git diff` empty). |
| Frontend build | exit 0; tracked `tsbuildinfo` restored |

After the report, `qlbanhang/main` @ `fa51918` (PR #13, stock voucher form fixes, frontend and docs only) was merged in as `76f6715` without conflicts, because the remote branch of the same name had been created at that commit. Re-checked on the merged tree: frontend typecheck exit 0; tests 1961, 1958 passed, the same 3 baseline failures; lint 16 problems (3 errors, 13 warnings; PR #13 removed one baseline warning), none in a file of this plan; build exit 0. The backend did not change.

The baseline counts grew because the merge of `main` brought the TT133 tests, and this plan added its own: 841 → 1281 backend tests, 948 → 1703 frontend tests.

## Manual checks (Task 8.3)

None was done, because this execution had no browser and no real-like data. Each check is still owed before release:

1. **Layout** at 1366×768 and at 375 px. Check that nothing scrolls sideways, not only that the page is one column. The final review found and fixed a grid blowout that jsdom cannot show. The subgrid KPI row with all domains and with partial domains, and the tab bar scrolling at 375 px, were checked only by class assertions.
2. **Live refresh:** save a receipt in another browser; the figures should update within about two seconds and the status read "Trực tiếp".
3. **Disconnect:** stop the API; the status should read "Mất kết nối", and the figures should refresh after the API returns.
4. **No cost permission:** for a user without `inventory.view_cost`, Lãi gộp and Giá trị tồn kho read "—".
5. **No company access:** a user without `branches.access_all` sees no branch select and only the own branch.
6. **Branch choice:** choosing a branch updates the header switcher, and report links open that branch's report.
7. **Response times** of the 13 `dashboard/executive` requests; record the slowest three.
8. **Figures against reports,** on the same branch and date: Tồn quỹ = Sổ quỹ, Phải thu = Số dư công nợ, Giá trị tồn kho = Tồn kho.

## Residual risks and follow-ups

- **Business questions** (plan, Open questions). The first two must be answered before gross profit is shown to users:
  - Gross profit on returns: returns re-enter stock at the selling price.
  - Input VAT inside cost, with `PurchaseCostIncludesVat = true`.
  - Whether user-created stock reasons count as sales. Today they do not; this is tested.
  - Revenue before or after VAT, and freight.
- **Accepted behaviour worth a product decision:**
  - "Vừa phát sinh" shows purchase totals to a user without `inventory.view_cost`. Only ADMIN and MANAGER hold `dashboard.executive`.
  - The dashboard's inventory value-in excludes opening stock, while the stock summary report merged from `main` counts it.
  - Under branch costing, warehouse values may not add up to the branch stock value when quantities cancel out. The stock-on-hand report behaves the same way.
- **Follow-up `realtime-02`:** an open hub connection keeps its old dashboard group after the user's default branch or permissions change.
  - **Cause:** `NotificationHub.OnConnectedAsync` picks the group once. `AdminUserService.UpdateAsync` and `AdminRoleService.UpdatePermissionsAsync` do not tell the hub, and SignalR moves groups only by connection id, which the app does not track.
  - **Risk (minor, after a rare admin action):**
    - The connection receives another branch's `{branchId, domains}` and refetches its own figures for nothing.
    - Writes in its new branch do not reach it; the 60-second poll still refreshes the figures while the label says "Trực tiếp".
  - **Options:**
    - (a) a registry of each user's connections, moved on admin changes;
    - (b) a `SessionChanged` event to `user-{id}` that makes the client restart its connection — the smallest end to end;
    - (c) `CloseOnAuthenticationExpiration`, which caps the window at the token lifetime.
- **Response times** were not measured (manual check 7). A route above one second is a follow-up. SUMMARY's risk table gives the Phase 01 measurement and the point at which sales would pass one second (about 30,000 sales vouchers a year).
- **Next sub-projects** of the brainstorm: the accountant, warehouse and sales dashboards.
