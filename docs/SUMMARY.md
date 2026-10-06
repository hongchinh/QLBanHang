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
| [codebase/directory-structure.md](codebase/directory-structure.md) | Current backend/frontend tree, feature modules, API controllers, entry points, runtime artifacts and production configuration keys |

## Code Standard

Conventions, naming rules, tech stack versions, and development workflows.

| File | Description |
| ---- | ----------- |
| [code-standard/conventions.md](code-standard/conventions.md) | Backend and frontend coding conventions, module patterns, validation, permission guards, migrations, tests and deployment notes |

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
| [brainstorms/260523-1500-pwa-progressive-web-app/SUMMARY.md](brainstorms/260523-1500-pwa-progressive-web-app/SUMMARY.md) | Brainstorm for PWA — installable, offline cache, push notifications |
| [brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md](brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md) | Brainstorm for cloning legacy stock in/out vouchers (PhieuNhapXuat) — inventory ledger, costing, cash vouchers & debt in 3 rounds |
| [plans/260517-0749-quotation-list-totals-footer/SUMMARY.md](plans/260517-0749-quotation-list-totals-footer/SUMMARY.md) | Active plan for quotation-list totals footer |
| [plans/260517-0833-quotation-list-owner-filter/SUMMARY.md](plans/260517-0833-quotation-list-owner-filter/SUMMARY.md) | Active plan for quotation-list owner filter |
| [plans/260523-1530-pwa-progressive-web-app/SUMMARY.md](plans/260523-1530-pwa-progressive-web-app/SUMMARY.md) | Implementation plan for PWA — 4 phases: installable, API cache, push backend, push frontend |
| [plans/archived/](plans/archived/) | Archived implementation plans and execution reports |
