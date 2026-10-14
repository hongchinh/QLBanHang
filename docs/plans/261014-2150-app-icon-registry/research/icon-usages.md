# Icon usages — file và làn

> Sinh ngày 2026-10-14 từ `main` (`8135477`, cập nhật theo `9d3e407` ngày 2026-10-15) bằng cách quét `import {…} from 'lucide-react'` trong `frontend/src` (kể cả file test). Làn = phase và task chuyển file đó. Chạy lại phép quét ở đầu mỗi phase; file mới do plan khác thêm vào làn có cùng thư mục.

Tổng: 80 file (78 file nguồn, 2 file test).

| Làn | File (từ `frontend/src/`) | Icon lucide đang import |
|---|---|---|
| P02-T1 | `components/layout/app-layout.tsx` | LayoutDashboard |
| P02-T1 | `components/layout/nav-config.ts` | AlarmClock, ArrowDownToLine, ArrowLeftRight, ArrowUpFromLine, BadgeDollarSign, Banknote, BarChart3, BookOpen, BookText, Boxes, CarFront, ClipboardList, Combine, Container, CreditCard, FileCheck, FileText, Files, Gauge, HandCoins, History, Hourglass, Landmark, Library, Link2, ListChecks, ListOrdered, Package, PiggyBank, Presentation, QrCode, Scale, ScrollText, Settings, ShieldCheck, ShoppingCart, Sigma, SlidersHorizontal, Tag, Target, TrendingUp, Truck, UserCog, UserRound, Users, Users2, Wallet, Warehouse |
| P02-T1 | `components/layout/sidebar/sidebar-group.tsx` | ChevronDown, ChevronRight |
| P02-T1 | `components/layout/sidebar/sidebar-item.tsx` | Star |
| P02-T1 | `components/layout/sidebar/sidebar-rail.tsx` | Search, Star |
| P02-T1 | `components/layout/sidebar/sidebar-test-utils.tsx` | LayoutDashboard |
| P02-T2 | `components/layout/header/app-header.tsx` | Menu |
| P02-T2 | `components/layout/header/brand-block.tsx` | FileText, PanelLeftClose, PanelLeftOpen |
| P02-T2 | `components/layout/header/header-notifications.tsx` | Bell, Loader2 |
| P02-T2 | `components/layout/header/header-search-mobile-sheet.tsx` | Search |
| P02-T2 | `components/layout/header/header-search.tsx` | Search |
| P02-T2 | `components/layout/header/header-user-menu.tsx` | ChevronDown |
| P02-T2 | `components/layout/header/search-results-list.tsx` | Loader2, Search |
| P02-T1 | `components/layout/nav-config.test.ts` | Presentation |
| P02-T1 | `components/layout/sidebar/nav-search.test.ts` | LayoutDashboard |
| P02-T2 | `components/layout/sidebar/sidebar-pin-chips.tsx` | X |
| P02-T2 | `components/layout/sidebar/sidebar-search.tsx` | Search, X |
| P02-T2 | `components/layout/sidebar/sidebar.tsx` | SearchX, X |
| P02-T3 | `components/ui/dialog.tsx` | X |
| P02-T3 | `components/ui/dropdown-menu.tsx` | Check, ChevronRight, Circle |
| P02-T3 | `components/ui/multi-select.tsx` | ChevronDown |
| P02-T3 | `components/ui/select.tsx` | Check, ChevronDown |
| P02-T3 | `components/ui/sheet.tsx` | X |
| P02-T3 | `components/ui/toaster.tsx` | X |
| P02-T4 | `components/activity-history/activity-history.tsx` | Ban, CirclePlus, Link2Off, Loader2, ReceiptText, RotateCcw, Save, Trash2, type LucideIcon |
| P02-T4 | `components/confirm-required-dialog/confirm-required-dialog.tsx` | AlertTriangle |
| P02-T4 | `components/customer-autocomplete/customer-autocomplete.tsx` | LayoutList, Plus, X |
| P02-T4 | `components/partner-autocomplete/partner-autocomplete.tsx` | X |
| P02-T4 | `components/search-combobox/search-combobox.tsx` | X |
| P02-T4 | `components/tt133-export/export-targets-dialog.tsx` | X |
| P02-T4 | `components/tt133-export/tt133-export-button.tsx` | FileSpreadsheet, FileText, Loader2 |
| P02-T4 | `components/voucher-print/voucher-print-button.tsx` | ChevronDown, FileSpreadsheet, FileText, Loader2, Printer |
| P03-T1 | `pages/customers/customer-form-fields.tsx` | ArrowLeft |
| P03-T1 | `pages/customers/customer-list-page.tsx` | Plus, Pencil, Trash2, Search |
| P03-T1 | `pages/customers/customer-quotations-section.tsx` | Ban, CheckCircle2, Copy, Loader2, MoreHorizontal, Pencil, Plus, Printer, Search, Send |
| P03-T1 | `pages/quotations/components/line-items-grid.tsx` | Plus, Trash2 |
| P03-T1 | `pages/quotations/components/product-typeahead-cell.tsx` | LayoutList |
| P03-T1 | `pages/quotations/components/totals-panel.tsx` | QrCode |
| P03-T1 | `pages/quotations/quotation-form-page.tsx` | ArrowLeft, ArrowRightLeft, BadgeCheck, Ban, CheckCircle2, ChevronDown, CirclePlus, Clock, Copy, FileSpreadsheet, Loader2, Printer, Save, Send, X |
| P03-T1 | `pages/quotations/quotation-list-page.tsx` | Plus, Pencil, Printer, Ban, Search, Copy, MoreHorizontal, Send, CheckCircle2, BadgeCheck, Loader2, FileSpreadsheet |
| P03-T2 | `pages/cash-vouchers/cash-voucher-form-page.tsx` | ArrowLeft, Ban, RotateCcw, Save, Trash2, X |
| P03-T2 | `pages/cash-vouchers/cash-voucher-list-page.tsx` | Loader2, Plus, Search |
| P03-T2 | `pages/cash-vouchers/components/cash-line-grid.tsx` | Plus, Trash2 |
| P03-T2 | `pages/cash-vouchers/components/cash-partner-filter.tsx` | X |
| P03-T2 | `pages/fund-transfers/fund-transfer-form-page.tsx` | ArrowLeft, Ban, RotateCcw, Save, Trash2 |
| P03-T2 | `pages/fund-transfers/fund-transfer-list-page.tsx` | Loader2, Plus, Search |
| P03-T2 | `pages/stock-vouchers/components/negative-stock-dialog.tsx` | AlertTriangle |
| P03-T2 | `pages/stock-vouchers/components/stock-grid-settings-popover.tsx` | Settings2 |
| P03-T2 | `pages/stock-vouchers/components/stock-line-grid.tsx` | Keyboard, Plus, Trash2 |
| P03-T2 | `pages/stock-vouchers/stock-voucher-form-page.tsx` | ArrowLeft, Ban, RotateCcw, Save, Trash2, X |
| P03-T2 | `pages/stock-vouchers/stock-voucher-list-page.tsx` | Loader2, Plus, Search |
| P03-T3 | `pages/credit-limits/credit-limit-list-page.tsx` | Pencil, Plus, Search, Trash2 |
| P03-T3 | `pages/debt/components/settlement-list.tsx` | Undo2 |
| P03-T3 | `pages/debt/debt-matching-page.tsx` | Check, Eraser, Sparkles |
| P03-T3 | `pages/debt/debt-offset-form-page.tsx` | ArrowLeft, Ban, Eraser, Save, Sparkles, Trash2 |
| P03-T3 | `pages/debt/debt-offset-list-page.tsx` | Loader2, Plus |
| P03-T3 | `pages/debt/opening-debt-page.tsx` | AlertTriangle, Pencil, Plus, Search, Trash2 |
| P03-T3 | `pages/funds/fund-list-page.tsx` | Pencil, Plus, Trash2, Wallet |
| P03-T3 | `pages/inventory/opening-stock-page.tsx` | AlertTriangle, Plus, Save, Trash2 |
| P03-T4 | `features/customers/components/customer-catalog-list.tsx` | Search |
| P03-T4 | `features/products/components/product-catalog-list.tsx` | Search |
| P03-T4 | `pages/cash-reasons/cash-reason-list-page.tsx` | Pencil, Plus, Trash2 |
| P03-T4 | `pages/payment-methods/payment-method-list-page.tsx` | Pencil, Plus, Trash2 |
| P03-T4 | `pages/product-groups/product-group-list-page.tsx` | Plus, Pencil, Trash2, Search |
| P03-T4 | `pages/products/product-form-page.tsx` | ArrowLeft |
| P03-T4 | `pages/products/product-list-page.tsx` | Plus, Pencil, Trash2, Search |
| P03-T4 | `pages/stock-reasons/stock-reason-list-page.tsx` | Pencil, Plus, Trash2 |
| P03-T4 | `pages/suppliers/supplier-list-page.tsx` | Plus, Pencil, Trash2, Search |
| P03-T4 | `pages/warehouses/warehouse-list-page.tsx` | Pencil, Plus, Trash2 |
| P03-T5 | `pages/admin/components/role-matrix-table.tsx` | MoreVertical |
| P03-T5 | `pages/admin/components/user-actions-menu.tsx` | MoreHorizontal |
| P03-T5 | `pages/admin/roles-matrix-page.tsx` | Plus, Save, RotateCcw |
| P03-T5 | `pages/admin/user-settings-page.tsx` | ArrowRightLeft |
| P03-T5 | `pages/admin/users-list-page.tsx` | Search, UserPlus |
| P03-T5 | `pages/reports/revenue-page.tsx` | Loader2 |
| P03-T5 | `pages/reports/sales-performance-page.tsx` | ArrowDown, ArrowUp |
| P03-T5 | `pages/reports/sales-revenue-detail-page.tsx` | ArrowLeft |
| P03-T5 | `pages/settings/branches-page.tsx` | CalendarDays, Pencil, Plus, Trash2 |
| P03-T5 | `pages/settings/recalc-cost-page.tsx` | Calculator, X |
| P03-T5 | `pages/settings/settings-hub-page.tsx` | Building2, Calculator, Hash, HandCoins, Lock, Settings, SlidersHorizontal, UserCog, Users2, type LucideIcon |
