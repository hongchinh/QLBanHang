# Documentation Summary

**QLBanHang** — phần mềm quản lý báo giá, khách hàng, hàng hóa, dashboard và báo cáo doanh thu theo nhân viên kinh doanh. Backend dùng .NET 9 Clean Architecture + EF Core/Npgsql/PostgreSQL; frontend dùng React 18 + Vite + TypeScript + TanStack Query/Table.

## Architecture

System design, component interactions, data flows, deployment, and external integrations.

| File | Description |
| ---- | ----------- |
| [architecture/system-architecture.md](architecture/system-architecture.md) | Clean Architecture layering, auth/refresh-token flow, RBAC/permission matrix, quotation ownership, dashboard/report/export flow (including the TT133 report form exports, branch/company report scope and the company profile), the executive dashboard (metrics services, scope, routes and permissions, which figures are tested against which report, the real-time `DashboardChanged` SignalR signal), demo branches and sample data (create flow through the queue, worker, runner and business services, acting identity, status and start-up recovery, hard delete order and sample catalog cleanup, company scope excluding demo branches, one dashboard signal per seed), API response, soft-delete/audit, migration/seed, logging and frontend layering |
| [architecture/pwa-mobile.md](architecture/pwa-mobile.md) | PWA platform matrix (Android, desktop, iOS before and after install), iOS limits and safe-area rules, web push flow from NotificationService to the service worker and the NAVIGATE message, push prompt states, API cache allow-list, hosting (Caddy no-cache, API-origin icons, VAPID) and the manual test checklist |

## Codebase

Directory structure, entry points, API patterns, and key modules.

| File | Description |
| ---- | ----------- |
| [codebase/directory-structure.md](codebase/directory-structure.md) | Current backend/frontend tree, feature modules (including inventory, cash & debt, the company profile, the TT133 report forms and voucher prints, the executive dashboard, the real-time signal and the demo branches with `Application/Organization/SampleData` and `WebApi/BackgroundJobs`), API controllers, entry points, runtime artifacts and production configuration keys |

## Code Standard

Conventions, naming rules, tech stack versions, and development workflows.

| File | Description |
| ---- | ----------- |
| [code-standard/conventions.md](code-standard/conventions.md) | Backend and frontend coding conventions, module patterns, validation, permission guards, migrations, tests, deployment notes, and the inventory (Round 1), cash & debt (Round 2A), TT133 report form and voucher print (`IVoucherPrintService`, `Tt133ExportSupport`, nullable list bodies, selection through table meta) and executive dashboard patterns (metrics services, report comparison tests, `[DashboardSignal]` on write controllers, executive query keys, tile kit, range presets), demo branch and background job patterns (`IActingIdentity` only in background work, `Channel` queue + `BackgroundService` with start-up recovery, the demo hard delete as the soft-delete exception, company-wide queries skip `IsDemo` branches), test doubles through `WithWebHostBuilder` + `ConfigureTestServices`, and the frontend icon registry (`AppIcon`, tones, the lucide import ban) |

## Project PDR

Product goals, use cases, business rules, and constraints.

| File | Description |
| ---- | ----------- |
| [project-pdr/product-goals.md](project-pdr/product-goals.md) | Product scope around quotation lifecycle, target users, business rules, current modules (inventory, cash & debt, debt reports, the TT133 report forms, the TT133 voucher print forms, the executive dashboard and demo branches with sample data, with their limits), planned Round 3 scope and role dashboards, and non-goals |

## Other

Repository-specific docs outside the standard topic folders.

| File | Description |
| ---- | ----------- |
| [bd/index.html](bd/index.html) | BD entry page for business-design artifacts |
| [bd/phan-tich-hanh-vi-4-2-ma-doi-tuong.md](bd/phan-tich-hanh-vi-4-2-ma-doi-tuong.md) | Business behavior analysis for object-code rule 4.2 |
| [bd/ui_form_them_moi_phieu_thu_html.html](bd/ui_form_them_moi_phieu_thu_html.html) | Static UI reference for receipt form |
| [brainstorms/260513-0859-quotation-customer-autocomplete/SUMMARY.md](brainstorms/260513-0859-quotation-customer-autocomplete/SUMMARY.md) | Brainstorm for quotation customer autocomplete |
| [brainstorms/260514-1547-per-user-quotation-scoping/SUMMARY.md](brainstorms/260514-1547-per-user-quotation-scoping/SUMMARY.md) | Brainstorm for per-user quotation ownership and scoping |
| [brainstorms/260515-1249-quotation-only-pivot/SUMMARY.md](brainstorms/260515-1249-quotation-only-pivot/SUMMARY.md) | Brainstorm for pivoting product scope to quotation-first workflow |
| [brainstorms/260515-1329-dashboard-redesign/SUMMARY.md](brainstorms/260515-1329-dashboard-redesign/SUMMARY.md) | Brainstorm for dashboard redesign |
| [brainstorms/260519-2217-quotation-form-sticky-actions-options.html](brainstorms/260519-2217-quotation-form-sticky-actions-options.html) | Visual options for the quotation form sticky action bar |
| [brainstorms/260519-2239-quotation-form-icon-color-options.html](brainstorms/260519-2239-quotation-form-icon-color-options.html) | Visual options for quotation form icon colors |
| [brainstorms/260522-2326-zalo-oa-send-quotation/SUMMARY.md](brainstorms/260522-2326-zalo-oa-send-quotation/SUMMARY.md) | Brainstorm for sending quotation files to a Zalo group via Zalo OA |
| [brainstorms/260523-0002-bien-ban-ban-giao-print/SUMMARY.md](brainstorms/260523-0002-bien-ban-ban-giao-print/SUMMARY.md) | Brainstorm for printing the handover record / stock-out slip |
| [brainstorms/260523-1430-sales-revenue-drilldown/SUMMARY.md](brainstorms/260523-1430-sales-revenue-drilldown/SUMMARY.md) | Brainstorm for drilling down the sales revenue report by salesperson |
| [brainstorms/260523-1500-pwa-progressive-web-app/SUMMARY.md](brainstorms/260523-1500-pwa-progressive-web-app/SUMMARY.md) | Brainstorm for PWA — installable, offline cache, push notifications |
| [brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md](brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md) | Brainstorm for cloning legacy stock in/out vouchers (PhieuNhapXuat) — inventory ledger, costing, cash vouchers & debt in 3 rounds |
| [brainstorms/261007-2252-cash-debt-round2/SUMMARY.md](brainstorms/261007-2252-cash-debt-round2/SUMMARY.md) | Brainstorm for Round 2 — cash vouchers, funds (multiple bank accounts) and fund transfers, receivables/payables with document matching and returns, credit limits, debt offset and debt/cash reports (reviewed) |
| [brainstorms/261007-2304-pwa-mobile-learn-from-crm/SUMMARY.md](brainstorms/261007-2304-pwa-mobile-learn-from-crm/SUMMARY.md) | Brainstorm for mobile improvements learned from CRMProjects — SW push handler, mobile card view, iOS PWA support, platform-limits docs |
| [brainstorms/261012-2135-sidebar-navigation-redesign/SUMMARY.md](brainstorms/261012-2135-sidebar-navigation-redesign/SUMMARY.md) | Brainstorm for the left sidebar redesign — scroll fix, task-based groups (Bán hàng/Kho/Thu chi/Công nợ/Báo cáo/Danh mục/Hệ thống), collapsible groups, menu search, per-user pins, collapsed flyouts (reviewed) |
| [brainstorms/261012-2135-sidebar-navigation-redesign/section-01-ui-design.md](brainstorms/261012-2135-sidebar-navigation-redesign/section-01-ui-design.md) | Sidebar UI design (style C: colored group blocks, pinned chips) — color tokens, states (pins, search, collapsed flyout, dark mode), components, edge cases, a11y, tests; HTML mockups alongside |
| [brainstorms/261012-2237-tt133-report-forms/SUMMARY.md](brainstorms/261012-2237-tt133-report-forms/SUMMARY.md) | Brainstorm for TT133 report forms — Excel/PDF exports of S04a/S05 (funds), S06/S07/S08 (inventory), S12 + debt summary + reconciliation record, company profile, shared ClosedXML form builder, branch/company scope (reviewed) |
| [brainstorms/261012-2237-tt133-report-forms/section-01-form-specs.md](brainstorms/261012-2237-tt133-report-forms/section-01-form-specs.md) | Per-form specs — shared frame, column-to-source mapping, debit/credit rules, S07 invariants, amount-in-words test cases |
| [brainstorms/261012-2237-tt133-report-forms/section-02-review-decisions.md](brainstorms/261012-2237-tt133-report-forms/section-02-review-decisions.md) | BA/SA review — branch vs company scope, CostingScope=Warehouse at go-live, report DTO additions, rejected proposals and recorded risks |
| [brainstorms/261013-0615-stock-voucher-form-tabs/SUMMARY.md](brainstorms/261013-0615-stock-voucher-form-tabs/SUMMARY.md) | Brainstorm for the stock in/out voucher form redesign — main fields + tabs (T2), full-height line grid with optional/sticky columns, per-pricing-mode Enter path configurable per user, sticky totals bar with receivable/payable and after-voucher debt; HTML mockups alongside |
| [brainstorms/261013-2042-tt133-voucher-print-forms/SUMMARY.md](brainstorms/261013-2042-tt133-voucher-print-forms/SUMMARY.md) | Brainstorm for TT133 voucher print forms — Excel/PDF of 01-TT/02-TT (receipt/payment, A5 landscape) and 01-VT/02-VT (stock in/out, A4 portrait), single and bulk print from form and list pages, three price modes for stock vouchers (cost read from the inventory ledger, VAT row that adds up to the total); builds on the merged TT133 report forms plan (reviewed against code; implemented, see the plan) |
| [brainstorms/261013-2247-executive-dashboard/SUMMARY.md](brainstorms/261013-2247-executive-dashboard/SUMMARY.md) | Brainstorm for the executive dashboard — shared dashboard base (tile kit, SignalR "domain changed" signal) and the director view: revenue/cost/gross profit from stock-out vouchers, cash, debt, inventory, "Cần chú ý" column, company-wide with branch filter, deep-dive tabs; first of five role-dashboard sub-projects; HTML mockups alongside |
| [brainstorms/261014-0618-mobile-manager-hub/SUMMARY.md](brainstorms/261014-0618-mobile-manager-hub/SUMMARY.md) | Brainstorm for the manager mobile hub — bottom tab bar, operational overview, card reports, document event stream with fund and bank balance notifications (app push, Telegram, Google Chat, Zalo), post-posting review, SePay bank feed; merges the 261007 PWA mobile brainstorm; 6 rounds, native app later; HTML mockups alongside |
| [brainstorms/261014-0618-mobile-manager-hub/section-01-mobile-shell-and-screens.md](brainstorms/261014-0618-mobile-manager-hub/section-01-mobile-shell-and-screens.md) | Rounds 1–2 — PWA foundation carried over from 261007, mobile tab bar rules, overview tiles and their sources, report index, five card reports, `ResponsiveTable` |
| [brainstorms/261014-0618-mobile-manager-hub/section-02-events-and-notifications.md](brainstorms/261014-0618-mobile-manager-hub/section-02-events-and-notifications.md) | Round 3 (Zalo in round 6) — document events, fund balance movements taken from the fund guard, outbox and dispatcher, recipients and preferences, group destinations and channels, message format, edge cases |
| [brainstorms/261014-0618-mobile-manager-hub/section-03-review.md](brainstorms/261014-0618-mobile-manager-hub/section-03-review.md) | Round 4 — post-posting review: scope, state transitions, rules (self-approval allowed), data, API, screens, notifications |
| [brainstorms/261014-0618-mobile-manager-hub/section-04-bank-feed.md](brainstorms/261014-0618-mobile-manager-hub/section-04-bank-feed.md) | Round 5 — SePay webhook bank feed: provider comparison, webhook flow and payload mapping, security, bank accounts, visibility, product-goals change |
| [brainstorms/261014-1601-branch-sample-data/SUMMARY.md](brainstorms/261014-1601-branch-sample-data/SUMMARY.md) | Brainstorm for training demo branches — "create with sample data" checkbox on new branch, shared `DM-` sample catalog, background seeder calling the business services in date order (opening balances + 3 months of documents), demo-branch hard delete, company-scope reports exclude demo branches, parallel-agent split |
| [brainstorms/261014-1601-branch-sample-data/section-01-dataset-spec.md](brainstorms/261014-1601-branch-sample-data/section-01-dataset-spec.md) | Sample dataset content — code conventions, 30 insulation products, customers/suppliers with payment habits, opening balances, monthly document mix and rules, expected end state, volume fallbacks |
| [plans/261014-2227-pwa-push-ios-foundation/SUMMARY.md](plans/261014-2227-pwa-push-ios-foundation/SUMMARY.md) | Plan for round 1 of the manager mobile hub — service worker push and notificationclick with SPA navigation, 180px apple-touch-icon and API-origin favicon, iOS meta and safe-area utilities, iOS install hint, Caddy no-cache for sw.js and the manifest, VAPID key in the frontend build, pwa-mobile.md; 3 phases |
| [plans/archived/261013-2313-executive-dashboard/SUMMARY.md](plans/archived/261013-2313-executive-dashboard/SUMMARY.md) | Archived plan (executed; report in `EXECUTION-REPORT.md`) for the executive dashboard — permission `dashboard.executive`, read-only `Dashboards/Executive` module (sales, cash, debt, inventory metrics, branch comparison, attention, recent documents), `[DashboardSignal]` SignalR signal, `/dashboard/executive` page with overview and four deep-dive tabs |
| [plans/archived/261013-0622-stock-voucher-form-tabs/SUMMARY.md](plans/archived/261013-0622-stock-voucher-form-tabs/SUMMARY.md) | Archived plan (executed, PR #8) for the stock voucher form redesign — backend PayableBalance, pure navigation/prefs/debt logic, T2 layout with tabs and debt strip, grid columns and keyboard |
| [plans/archived/260517-0749-quotation-list-totals-footer/SUMMARY.md](plans/archived/260517-0749-quotation-list-totals-footer/SUMMARY.md) | Archived plan for quotation-list totals footer |
| [plans/archived/260517-0833-quotation-list-owner-filter/SUMMARY.md](plans/archived/260517-0833-quotation-list-owner-filter/SUMMARY.md) | Archived plan for quotation-list owner filter |
| [plans/260522-2326-zalo-send-quotation/SUMMARY.md](plans/260522-2326-zalo-send-quotation/SUMMARY.md) | Plan for sending quotation files to a Zalo group via Zalo OA |
| [plans/archived/260523-1530-pwa-progressive-web-app/SUMMARY.md](plans/archived/260523-1530-pwa-progressive-web-app/SUMMARY.md) | Implementation plan for PWA — 4 phases: installable, API cache, push backend, push frontend |
| [plans/260528-1833-product-form-dialog/SUMMARY.md](plans/260528-1833-product-form-dialog/SUMMARY.md) | Plan for create/edit actions in the product catalog dialog |
| [plans/archived/261014-1616-branch-sample-data/SUMMARY.md](plans/archived/261014-1616-branch-sample-data/SUMMARY.md) | Archived plan (executed; report in `EXECUTION-REPORT.md`, decisions pending the user's confirmation listed there) for demo branches with sample data — Branch/Product/Customer flags, acting identity for background work, deterministic dataset generator, background runner calling the business services, demo-branch hard delete with catalog cleanup, company-scope reports excluding demo branches, branches page UI; four parallel lanes |
| [plans/archived/261006-2259-inventory-round1/SUMMARY.md](plans/archived/261006-2259-inventory-round1/SUMMARY.md) | Implementation plan for inventory Round 1 — stock vouchers, ledger, costing, reports |
| [plans/archived/261007-2314-cash-debt-round2a/SUMMARY.md](plans/archived/261007-2314-cash-debt-round2a/SUMMARY.md) | Implementation plan for Round 2A — receipts/payments, funds, fund transfers, receivables/payables with matching and offset, credit limits, debt balance/ledger and cash book reports |
| [plans/archived/261012-1709-cash-debt-round2b/SUMMARY.md](plans/archived/261012-1709-cash-debt-round2b/SUMMARY.md) | Implementation plan for Round 2B — debt documents, aging, overdue, by-salesperson and reconciliation reports |
| [plans/archived/261012-0433-round2a-review-refactor/SUMMARY.md](plans/archived/261012-0433-round2a-review-refactor/SUMMARY.md) | Archived plan for the last Round 2A review findings — settlement confirmation token, shared backend helpers, Auto recompute before commit, frontend refactors |
| [plans/archived/261012-2311-sidebar-navigation-redesign/SUMMARY.md](plans/archived/261012-2311-sidebar-navigation-redesign/SUMMARY.md) | Implementation plan for the sidebar redesign — scroll hotfix, task-based groups with colored blocks, longest-prefix highlight, collapsible groups, accent-insensitive search, per-user pin chips, collapsed rail with flyouts |
| [plans/archived/261013-1926-tt133-report-forms/SUMMARY.md](plans/archived/261013-1926-tt133-report-forms/SUMMARY.md) | Implementation plan for the TT133 report forms — company profile, shared ClosedXML form builder, Excel/PDF exports of S04a/S05/S06/S07/S08/S12, debt summary and reconciliation record, branch/company scope, stock summary report |
| [plans/archived/261014-1632-tt133-voucher-print-forms/SUMMARY.md](plans/archived/261014-1632-tt133-voucher-print-forms/SUMMARY.md) | Archived plan (executed; report in `EXECUTION-REPORT.md`, go-live gate in `GO-LIVE-CHECKLIST.md`) for the TT133 voucher print forms — 01-TT/02-TT (A5) and 01-VT/02-VT (A4) as Excel/PDF from the voucher and list pages, three price modes for stock vouchers, shared export support |
| [plans/archived/](plans/archived/) | Archived implementation plans and execution reports |
