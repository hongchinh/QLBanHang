# Documentation Summary

**QLDonHang** — phần mềm quản lý báo giá, khách hàng, hàng hóa, dashboard và báo cáo doanh thu theo nhân viên kinh doanh. Backend dùng .NET 9 Clean Architecture + EF Core/Npgsql/PostgreSQL; frontend dùng React 18 + Vite + TypeScript + TanStack Query/Table.

## Architecture

System design, component interactions, data flows, deployment, and external integrations.

| File | Description |
| ---- | ----------- |
| [architecture/system-architecture.md](architecture/system-architecture.md) | Clean Architecture layering, auth/refresh-token flow, RBAC/permission matrix, quotation ownership, dashboard/report/export flow, API response, soft-delete/audit, migration/seed, logging and frontend layering |

## Codebase

Directory structure, entry points, API patterns, and key modules.

| File | Description |
| ---- | ----------- |
| [codebase/directory-structure.md](codebase/directory-structure.md) | Current backend/frontend tree, feature modules (including inventory and cash & debt), API controllers, entry points, runtime artifacts and production configuration keys |

## Code Standard

Conventions, naming rules, tech stack versions, and development workflows.

| File | Description |
| ---- | ----------- |
| [code-standard/conventions.md](code-standard/conventions.md) | Backend and frontend coding conventions, module patterns, validation, permission guards, migrations, tests, deployment notes, and the inventory (Round 1) and cash & debt (Round 2A) patterns |

## Project PDR

Product goals, use cases, business rules, and constraints.

| File | Description |
| ---- | ----------- |
| [project-pdr/product-goals.md](project-pdr/product-goals.md) | Product scope around quotation lifecycle, target users, business rules, current modules, planned inventory/cash/debt scope and non-goals |

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
| [plans/archived/260517-0749-quotation-list-totals-footer/SUMMARY.md](plans/archived/260517-0749-quotation-list-totals-footer/SUMMARY.md) | Archived plan for quotation-list totals footer |
| [plans/archived/260517-0833-quotation-list-owner-filter/SUMMARY.md](plans/archived/260517-0833-quotation-list-owner-filter/SUMMARY.md) | Archived plan for quotation-list owner filter |
| [plans/260522-2326-zalo-send-quotation/SUMMARY.md](plans/260522-2326-zalo-send-quotation/SUMMARY.md) | Plan for sending quotation files to a Zalo group via Zalo OA |
| [plans/archived/260523-1530-pwa-progressive-web-app/SUMMARY.md](plans/archived/260523-1530-pwa-progressive-web-app/SUMMARY.md) | Implementation plan for PWA — 4 phases: installable, API cache, push backend, push frontend |
| [plans/260528-1833-product-form-dialog/SUMMARY.md](plans/260528-1833-product-form-dialog/SUMMARY.md) | Plan for create/edit actions in the product catalog dialog |
| [plans/archived/261006-2259-inventory-round1/SUMMARY.md](plans/archived/261006-2259-inventory-round1/SUMMARY.md) | Implementation plan for inventory Round 1 — stock vouchers, ledger, costing, reports |
| [plans/archived/261007-2314-cash-debt-round2a/SUMMARY.md](plans/archived/261007-2314-cash-debt-round2a/SUMMARY.md) | Implementation plan for Round 2A — receipts/payments, funds, fund transfers, receivables/payables with matching and offset, credit limits, debt balance/ledger and cash book reports |
| [plans/archived/261012-1709-cash-debt-round2b/SUMMARY.md](plans/archived/261012-1709-cash-debt-round2b/SUMMARY.md) | Implementation plan for Round 2B — debt documents, aging, overdue, by-salesperson and reconciliation reports |
| [plans/archived/261012-0433-round2a-review-refactor/SUMMARY.md](plans/archived/261012-0433-round2a-review-refactor/SUMMARY.md) | Archived plan for the last Round 2A review findings — settlement confirmation token, shared backend helpers, Auto recompute before commit, frontend refactors |
| [plans/archived/261012-2311-sidebar-navigation-redesign/SUMMARY.md](plans/archived/261012-2311-sidebar-navigation-redesign/SUMMARY.md) | Implementation plan for the sidebar redesign — scroll hotfix, task-based groups with colored blocks, longest-prefix highlight, collapsible groups, accent-insensitive search, per-user pin chips, collapsed rail with flyouts |
| [plans/archived/](plans/archived/) | Archived implementation plans and execution reports |
