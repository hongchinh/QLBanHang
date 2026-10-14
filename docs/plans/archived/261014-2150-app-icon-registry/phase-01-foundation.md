# Phase 01 — Nền tảng: token, registry, AppIcon, test chặn

**Status:** [x] done
**Complexity:** M

## Objective

Tạo hạ tầng icon dùng chung: token màu cho tone, registry đủ tên cho toàn bộ plan, component `AppIcon`, test chặn import `lucide-react` (có danh sách file cũ để giảm dần), và luật ESLint ở mức `warn`. Chưa chuyển file nào.

## Files

- `frontend/src/index.css` (sửa)
- `frontend/tailwind.config.ts` (sửa)
- `frontend/eslint.config.js` (sửa)
- `frontend/src/components/icons/icon-tokens.test.ts` (mới)
- `frontend/src/components/icons/icon-registry.ts` (mới)
- `frontend/src/components/icons/icon-registry.test.ts` (mới)
- `frontend/src/components/icons/app-icon.tsx` (mới)
- `frontend/src/components/icons/app-icon.test.tsx` (mới)
- `frontend/src/components/icons/index.ts` (mới)
- `frontend/src/components/icons/no-direct-lucide-imports.test.ts` (mới)

## Bảng tên registry

Nội dung đầy đủ của `frontend/src/components/icons/icon-registry.ts` (task 2 tạo đúng file này):

```ts
import {
  AlarmClock, AlertTriangle, ArrowDown, ArrowDownToLine, ArrowLeft, ArrowLeftRight, ArrowRight, ArrowRightLeft,
  ArrowUp, ArrowUpFromLine, BadgeCheck, BadgeDollarSign, Ban, Banknote, BarChart3, Bell, BookOpen, BookText, Boxes,
  Building2, Calculator, CalendarDays, CarFront, Check, CheckCircle2, ChevronDown, ChevronLeft, ChevronRight, Circle,
  CirclePlus, ClipboardList, Clock, Combine, Container, Copy, CreditCard, Eraser, FileCheck, FileSpreadsheet,
  FileText, Files, Gauge, HandCoins, Hash, History, Hourglass, Keyboard, Landmark, LayoutDashboard, LayoutList,
  Library, Link2, Link2Off, ListChecks, ListOrdered, Loader2, Lock, Menu, MoreHorizontal, MoreVertical, Package,
  PanelLeftClose, PanelLeftOpen, Pencil, PiggyBank, Plus, Presentation, Printer, QrCode, ReceiptText, RotateCcw,
  Save, Scale, ScrollText, Search, SearchX, Send, Settings, Settings2, ShieldCheck, ShoppingCart, Sigma,
  SlidersHorizontal, Sparkles, Star, Tag, Target, Trash2, TrendingDown, TrendingUp, Truck, Undo2, UserCog,
  UserPlus, UserRound, Users, Users2, Wallet, Warehouse, X, type LucideIcon,
} from 'lucide-react';

// The only file allowed to import lucide-react (enforced by no-direct-lucide-imports.test.ts and ESLint).
// Names describe the business meaning, not the drawing, so a later icon set can map them differently.

export const ICON_TONES = ['create', 'edit', 'danger', 'confirm', 'info', 'export', 'print', 'accent', 'muted', 'warn'] as const;
export type IconTone = (typeof ICON_TONES)[number];

export interface IconDef {
  icon: LucideIcon;
  // Default colour; a caller can pass tone="none" (icon on a filled or coloured surface).
  tone?: IconTone;
  // Always spins (loading indicators).
  spin?: boolean;
}

export const ICONS = {
  // Utility: no tone, the colour comes from the surrounding text.
  menu: { icon: Menu },
  sidebarCollapse: { icon: PanelLeftClose },
  sidebarExpand: { icon: PanelLeftOpen },
  chevronDown: { icon: ChevronDown },
  chevronRight: { icon: ChevronRight },
  chevronLeft: { icon: ChevronLeft },
  arrowLeft: { icon: ArrowLeft },
  arrowRight: { icon: ArrowRight },
  trendUp: { icon: TrendingUp },
  trendDown: { icon: TrendingDown },
  sortAsc: { icon: ArrowUp },
  sortDesc: { icon: ArrowDown },
  more: { icon: MoreHorizontal },
  moreVertical: { icon: MoreVertical },
  check: { icon: Check },
  radioIndicator: { icon: Circle },
  close: { icon: X },
  loading: { icon: Loader2, spin: true },
  pin: { icon: Star },
  searchEmpty: { icon: SearchX },
  keyboard: { icon: Keyboard },
  gridSettings: { icon: Settings2 },
  listPicker: { icon: LayoutList },
  notifications: { icon: Bell },
  brandFallback: { icon: FileText },
  transferOwnership: { icon: ArrowRightLeft },
  applyMatching: { icon: Check },
  time: { icon: Clock },
  calendar: { icon: CalendarDays },
  paymentQr: { icon: QrCode },

  // Actions (tone = the per-action colour of the conventions).
  search: { icon: Search, tone: 'muted' },
  back: { icon: ArrowLeft, tone: 'muted' },
  closeForm: { icon: X, tone: 'muted' },
  remove: { icon: X, tone: 'danger' },
  add: { icon: Plus, tone: 'create' },
  addUser: { icon: UserPlus, tone: 'create' },
  edit: { icon: Pencil, tone: 'edit' },
  delete: { icon: Trash2, tone: 'danger' },
  cancelVoucher: { icon: Ban, tone: 'danger' },
  restore: { icon: RotateCcw, tone: 'confirm' },
  discardChanges: { icon: Undo2, tone: 'muted' },
  unsettle: { icon: Undo2, tone: 'danger' },
  save: { icon: Save, tone: 'edit' },
  send: { icon: Send, tone: 'create' },
  confirm: { icon: CheckCircle2, tone: 'confirm' },
  accountingConfirm: { icon: BadgeCheck, tone: 'confirm' },
  clone: { icon: Copy, tone: 'accent' },
  print: { icon: Printer, tone: 'print' },
  exportExcel: { icon: FileSpreadsheet, tone: 'export' },
  exportPdf: { icon: FileText, tone: 'print' },
  autoMatch: { icon: Sparkles, tone: 'accent' },
  clearMatches: { icon: Eraser, tone: 'danger' },
  warning: { icon: AlertTriangle, tone: 'warn' },
  recalcCost: { icon: Calculator, tone: 'edit' },
  openingBalance: { icon: Wallet, tone: 'confirm' },

  // Document activity history (activity-history.tsx, quotation-form-page.tsx).
  activityCreated: { icon: CirclePlus, tone: 'create' },
  activityUpdated: { icon: Save, tone: 'edit' },
  activitySent: { icon: Send, tone: 'create' },
  activityConfirmed: { icon: CheckCircle2, tone: 'confirm' },
  activityAccountingConfirmed: { icon: CheckCircle2, tone: 'info' },
  activityCancelled: { icon: Ban, tone: 'danger' },
  activityRestored: { icon: RotateCcw, tone: 'confirm' },
  activityDeleted: { icon: Trash2, tone: 'danger' },
  activitySettlementRemoved: { icon: Link2Off, tone: 'warn' },
  activityAutoVoucherChanged: { icon: ReceiptText, tone: 'print' },
  activityOwnerTransferred: { icon: ArrowRightLeft, tone: 'accent' },
  activityCloned: { icon: Copy, tone: 'accent' },

  // Sidebar (nav-config.ts): groups, then items. Drawings must stay unique within the sidebar.
  overview: { icon: LayoutDashboard },
  sales: { icon: ShoppingCart },
  quotation: { icon: FileText },
  customers: { icon: Users },
  inventory: { icon: Warehouse },
  stockIn: { icon: ArrowDownToLine },
  stockOut: { icon: ArrowUpFromLine },
  openingStock: { icon: ClipboardList },
  cash: { icon: Wallet },
  receipt: { icon: HandCoins },
  payment: { icon: Banknote },
  fundTransfer: { icon: ArrowLeftRight },
  debt: { icon: Scale },
  debtMatching: { icon: Link2 },
  debtOffset: { icon: Combine },
  openingDebt: { icon: History },
  creditLimit: { icon: Gauge },
  reports: { icon: BarChart3 },
  executiveDashboard: { icon: Presentation },
  revenueReport: { icon: TrendingUp },
  salesRevenueReport: { icon: BadgeDollarSign },
  vehicleRevenueReport: { icon: CarFront },
  salesPerformanceReport: { icon: Target },
  stockOnHandReport: { icon: Boxes },
  stockCardReport: { icon: ScrollText },
  stockSummaryReport: { icon: Sigma },
  cashBookReport: { icon: PiggyBank },
  debtBalanceReport: { icon: BookOpen },
  debtLedgerReport: { icon: BookText },
  debtByDocumentReport: { icon: Files },
  debtAgingReport: { icon: Hourglass },
  debtOverdueReport: { icon: AlarmClock },
  debtBySalespersonReport: { icon: UserRound },
  reconciliationReport: { icon: FileCheck },
  catalogs: { icon: Library },
  product: { icon: Package },
  productGroup: { icon: Tag },
  supplier: { icon: Truck },
  warehouse: { icon: Container },
  stockReason: { icon: ListChecks },
  cashReason: { icon: ListOrdered },
  paymentMethod: { icon: CreditCard },
  fund: { icon: Landmark },
  system: { icon: Settings },
  mySettings: { icon: UserCog },
  systemConfig: { icon: SlidersHorizontal },
  users: { icon: Users2 },
  roles: { icon: ShieldCheck },

  // Settings hub cards (settings-hub-page.tsx); the hub's other cards use system, mySettings, users.
  settingsCompany: { icon: Building2 },
  settingsBranches: { icon: Building2 },
  settingsPeriodLock: { icon: Lock },
  settingsNumbering: { icon: Hash },
  settingsInventory: { icon: SlidersHorizontal },
  settingsRecalcCost: { icon: Calculator },
  settingsCash: { icon: HandCoins },
  branch: { icon: Building2 },

  // Home page KPI labels (dashboard-page.tsx).
  kpiRevenueToday: { icon: TrendingUp },
  kpiRevenueRange: { icon: BarChart3 },
  kpiQuotations: { icon: FileText },
  kpiCancelled: { icon: Ban },
} satisfies Record<string, IconDef>;

export type IconName = keyof typeof ICONS;

export function iconDef(name: IconName): IconDef {
  return ICONS[name];
}
```

Bảng sidebar (label → tên): Bán hàng `sales`, Báo giá `quotation`, Khách hàng `customers`, Tạo mã QR thanh toán `paymentQr`, Kho `inventory`, Phiếu nhập kho `stockIn`, Phiếu xuất kho `stockOut`, Tồn đầu kỳ `openingStock`, Thu chi `cash`, Phiếu thu `receipt`, Phiếu chi `payment`, Chuyển quỹ `fundTransfer`, Công nợ `debt`, Đối trừ chứng từ `debtMatching`, Bù trừ công nợ `debtOffset`, Nợ đầu kỳ `openingDebt`, Hạn mức nợ `creditLimit`, Báo cáo `reports`, Điều hành `executiveDashboard`, Doanh thu `revenueReport`, Doanh thu sale `salesRevenueReport`, Doanh thu xe `vehicleRevenueReport`, Hiệu suất sale `salesPerformanceReport`, Tồn kho `stockOnHandReport`, Thẻ kho `stockCardReport`, Tổng hợp nhập xuất tồn `stockSummaryReport`, Sổ quỹ `cashBookReport`, Số dư công nợ `debtBalanceReport`, Sổ chi tiết công nợ `debtLedgerReport`, Công nợ theo chứng từ `debtByDocumentReport`, Tuổi nợ `debtAgingReport`, Nợ quá hạn `debtOverdueReport`, Công nợ theo nhân viên `debtBySalespersonReport`, Biên bản đối chiếu `reconciliationReport`, Danh mục `catalogs`, Hàng hóa `product`, Nhóm hàng hóa `productGroup`, Nhà cung cấp `supplier`, Kho hàng `warehouse`, Lý do nhập xuất `stockReason`, Lý do thu/chi `cashReason`, Hình thức thanh toán `paymentMethod`, Quỹ `fund`, Hệ thống `system`, Cài đặt của tôi `mySettings`, Cấu hình hệ thống `systemConfig`, Quản lý người dùng `users`, Phân quyền `roles`. Mục "Tổng quan" (`app-layout.tsx`) dùng `overview`.

Token tone (HSL channels, giá trị Tailwind đang dùng):

| Tone | Màu Tailwind | Giá trị |
|---|---|---|
| create | cyan-600 | `191.6 91.4% 36.5%` |
| edit | blue-600 | `221.2 83.2% 53.3%` |
| danger | red-600 | `0 72.2% 50.6%` |
| confirm | emerald-600 | `161.4 93.5% 30.4%` |
| info | sky-600 | `200.4 98% 39.4%` |
| export | emerald-700 | `162.9 93.5% 24.3%` |
| print | indigo-600 | `243.4 75.4% 58.6%` |
| accent | violet-600 | `262.1 83.3% 57.8%` |
| muted | slate-500 | `215.4 16.3% 46.9%` |
| warn | amber-600 | `32.1 94.6% 43.7%` |

## Tasks

### Task 1 — Token màu cho tone

1. Viết test hỏng `frontend/src/components/icons/icon-tokens.test.ts`:
   ```ts
   import { readFileSync } from 'node:fs';
   import path from 'node:path';
   import { describe, expect, it } from 'vitest';

   const css = readFileSync(path.resolve(__dirname, '../../index.css'), 'utf8');
   const TONES = ['create', 'edit', 'danger', 'confirm', 'info', 'export', 'print', 'accent', 'muted', 'warn'];
   const block = (selector: string) => css.slice(css.indexOf(`${selector} {`), css.indexOf('}', css.indexOf(`${selector} {`)));

   describe('icon tone tokens', () => {
     it.each([':root', '.dark'])('%s defines every --icon-* token', (selector) => {
       for (const tone of TONES) expect(block(selector)).toMatch(new RegExp(`--icon-${tone}:\\s*[\\d.]+ [\\d.]+% [\\d.]+%;`));
     });
   });
   ```
   (Mở `index.css` trước để xác nhận `:root {` và `.dark {` đúng dạng như vậy; nếu khác, sửa hàm `block` cho khớp.)
2. Chạy `npm run test -- src/components/icons/icon-tokens.test.ts` → FAIL: `--icon-create` không có.
3. Thêm 10 dòng `--icon-<tone>: <giá trị>;` (bảng trên) ngay sau `--nav-match` trong khối `:root` và khối `.dark` của `frontend/src/index.css`. Thêm vào `theme.extend.colors` trong `frontend/tailwind.config.ts`:
   ```ts
   icon: {
     create: 'hsl(var(--icon-create))', edit: 'hsl(var(--icon-edit))', danger: 'hsl(var(--icon-danger))',
     confirm: 'hsl(var(--icon-confirm))', info: 'hsl(var(--icon-info))', export: 'hsl(var(--icon-export))',
     print: 'hsl(var(--icon-print))', accent: 'hsl(var(--icon-accent))', muted: 'hsl(var(--icon-muted))',
     warn: 'hsl(var(--icon-warn))',
   },
   ```
4. Chạy lại test → PASS.
5. `git commit -m "feat(icons): add icon tone tokens"`

### Task 2 — Registry

1. Viết test hỏng `frontend/src/components/icons/icon-registry.test.ts`:
   ```ts
   import { describe, expect, it } from 'vitest';
   import { FileText, Loader2, Plus, Trash2, Undo2 } from 'lucide-react';
   import { ICONS, ICON_TONES, iconDef, type IconName } from './icon-registry';

   describe('icon registry', () => {
     it('every entry has a lucide icon and a known tone', () => {
       for (const [name, def] of Object.entries(ICONS)) {
         expect(def.icon, name).toBeTruthy();
         if ('tone' in def) expect(ICON_TONES, name).toContain(def.tone);
       }
     });
     it.each<[IconName, unknown, string | undefined]>([
       ['add', Plus, 'create'],
       ['delete', Trash2, 'danger'],
       ['discardChanges', Undo2, 'muted'],
       ['exportPdf', FileText, 'print'],
       ['quotation', FileText, undefined],
     ])('%s maps to its drawing and tone', (name, icon, tone) => {
       expect(iconDef(name).icon).toBe(icon);
       expect(iconDef(name).tone).toBe(tone);
     });
     it('loading spins', () => {
       expect(iconDef('loading')).toEqual({ icon: Loader2, spin: true });
     });
   });
   ```
2. Chạy `npm run test -- src/components/icons/icon-registry.test.ts` → FAIL: không tìm thấy `./icon-registry`.
3. Tạo `icon-registry.ts` đúng như mục "Bảng tên registry".
4. Chạy lại test → PASS; `npm run typecheck` → PASS.
5. `git commit -m "feat(icons): add the business-named icon registry"`

### Task 3 — Component `AppIcon`

1. Viết test hỏng `frontend/src/components/icons/app-icon.test.tsx`:
   ```tsx
   import { render } from '@testing-library/react';
   import { describe, expect, it } from 'vitest';
   import { AppIcon } from './app-icon';

   const svg = (c: HTMLElement) => c.querySelector('svg')!;

   describe('AppIcon', () => {
     it('defaults to 16px, stroke 2, decorative, tagged with its name', () => {
       const { container } = render(<AppIcon name="add" />);
       expect(svg(container)).toHaveAttribute('width', '16');
       expect(svg(container)).toHaveAttribute('stroke-width', '2');
       expect(svg(container)).toHaveAttribute('aria-hidden', 'true');
       expect(svg(container)).toHaveAttribute('data-icon', 'add');
     });
     it.each([[8, '2'], [12, '2'], [14, '2'], [18, '1.85'], [20, '1.85'], [24, '1.75']] as const)(
       'size %i draws stroke %s', (size, stroke) => {
         const { container } = render(<AppIcon name="add" size={size} />);
         expect(svg(container)).toHaveAttribute('width', String(size));
         expect(svg(container)).toHaveAttribute('stroke-width', stroke);
       });
     it('applies the default tone class, an explicit tone, or none', () => {
       expect(svg(render(<AppIcon name="add" />).container)).toHaveClass('text-icon-create');
       expect(svg(render(<AppIcon name="add" tone="danger" />).container)).toHaveClass('text-icon-danger');
       expect(svg(render(<AppIcon name="add" tone="none" />).container).getAttribute('class')).not.toMatch(/text-icon-/);
     });
     it('keeps extra classes and spins the loading icon', () => {
       const { container } = render(<AppIcon name="loading" className="mr-2" />);
       expect(svg(container)).toHaveClass('mr-2', 'animate-spin');
     });
     it('is an image with a name when labelled', () => {
       const { getByRole } = render(<AppIcon name="trendUp" label="tăng" />);
       expect(getByRole('img', { name: 'tăng' })).toBeInTheDocument();
     });
   });
   ```
2. Chạy `npm run test -- src/components/icons/app-icon.test.tsx` → FAIL: không tìm thấy `./app-icon`.
3. Tạo `frontend/src/components/icons/app-icon.tsx`:
   ```tsx
   import type { LucideProps } from 'lucide-react';
   import { cn } from '@/lib/utils';
   import { ICONS, type IconDef, type IconName, type IconTone } from './icon-registry';

   export type IconSize = 8 | 12 | 14 | 16 | 18 | 20 | 24;

   const STROKE: Record<IconSize, number> = { 8: 2, 12: 2, 14: 2, 16: 2, 18: 1.85, 20: 1.85, 24: 1.75 };

   // Literal class names so Tailwind generates them.
   const TONE_CLASS: Record<IconTone, string> = {
     create: 'text-icon-create', edit: 'text-icon-edit', danger: 'text-icon-danger', confirm: 'text-icon-confirm',
     info: 'text-icon-info', export: 'text-icon-export', print: 'text-icon-print', accent: 'text-icon-accent',
     muted: 'text-icon-muted', warn: 'text-icon-warn',
   };

   export interface AppIconProps extends Omit<LucideProps, 'ref' | 'size' | 'strokeWidth' | 'color'> {
     name: IconName;
     size?: IconSize;
     // Defaults to the registry tone; "none" keeps the surrounding text colour.
     tone?: IconTone | 'none';
     // Makes the icon an image with this accessible name; without it the icon is decorative.
     label?: string;
   }

   export function AppIcon({ name, size = 16, tone, label, className, ...rest }: AppIconProps) {
     const def: IconDef = ICONS[name];
     const Icon = def.icon;
     const effective = tone ?? def.tone;
     return (
       <Icon
         {...rest}
         width={size}
         height={size}
         strokeWidth={STROKE[size]}
         data-icon={name}
         className={cn('shrink-0', effective && effective !== 'none' && TONE_CLASS[effective], def.spin && 'animate-spin', className)}
         {...(label ? { role: 'img', 'aria-label': label } : { 'aria-hidden': true })}
       />
     );
   }
   ```
   Và `frontend/src/components/icons/index.ts`:
   ```ts
   export { AppIcon, type AppIconProps, type IconSize } from './app-icon';
   export { ICONS, ICON_TONES, iconDef, type IconName, type IconTone } from './icon-registry';
   ```
4. Chạy lại test → PASS; `npm run typecheck` → PASS.
5. `git commit -m "feat(icons): add AppIcon with size-based stroke and tones"`

### Task 4 — Test chặn import trực tiếp (danh sách giảm dần)

1. Viết test hỏng `frontend/src/components/icons/no-direct-lucide-imports.test.ts`:
   ```ts
   import { readdirSync, readFileSync, statSync } from 'node:fs';
   import path from 'node:path';
   import { describe, expect, it } from 'vitest';

   const SRC = path.resolve(__dirname, '../..');
   const ICONS_DIR = path.join(SRC, 'components', 'icons');
   const LUCIDE_IMPORT = /from\s+['"]lucide-react['"]/;

   // Files not yet moved to AppIcon. Each migration task removes its files; Phase 04 deletes this list.
   const LEGACY: string[] = [];

   function walk(dir: string): string[] {
     return readdirSync(dir).flatMap((entry) => {
       const full = path.join(dir, entry);
       if (statSync(full).isDirectory()) return walk(full);
       return /\.(ts|tsx)$/.test(entry) ? [full] : [];
     });
   }

   describe('lucide-react imports', () => {
     it('happen only in components/icons (plus the legacy list)', () => {
       const offenders = walk(SRC)
         .filter((file) => !file.startsWith(ICONS_DIR))
         .filter((file) => LUCIDE_IMPORT.test(readFileSync(file, 'utf8')))
         .map((file) => path.relative(SRC, file).split(path.sep).join('/'))
         .sort();
       expect(offenders).toEqual([...LEGACY].sort());
     });
   });
   ```
2. Chạy `npm run test -- src/components/icons/no-direct-lucide-imports.test.ts` → FAIL: mảng thực tế có 80 file (78 file nguồn + `components/layout/nav-config.test.ts` + `components/layout/sidebar/nav-search.test.ts`).
3. Điền `LEGACY` bằng đúng 80 đường dẫn trong output (đường dẫn tương đối từ `src/`, dạng `pages/quotations/quotation-form-page.tsx`). Đối chiếu với [research/icon-usages.md](research/icon-usages.md); nếu có file mới do plan khác thêm, ghi vào cột "Làn" của research và vẫn đưa vào `LEGACY`.
4. Chạy lại test → PASS.
5. `git commit -m "test(icons): guard lucide-react imports with a shrinking legacy list"`

### Task 5 — Luật ESLint (mức warn)

Task cấu hình, không có code chạy. Kiểm bằng lệnh.

1. Thêm vào khối `rules` chung của `frontend/eslint.config.js`:
   ```js
   'no-restricted-imports': ['warn', {
     paths: [{ name: 'lucide-react', message: 'Dùng AppIcon và tên trong src/components/icons/icon-registry.ts.' }],
   }],
   ```
   và một khối mới ngay sau khối test:
   ```js
   {
     files: ['src/components/icons/**/*.{ts,tsx}'],
     rules: { 'no-restricted-imports': 'off' },
   },
   ```
2. Chạy `npx eslint src/components/icons src/App.tsx` → không có cảnh báo `no-restricted-imports` (App.tsx không import lucide).
3. Chạy `npx eslint src/pages/warehouses/warehouse-list-page.tsx` → có 1 cảnh báo `no-restricted-imports`.
4. Chạy `npm run lint` → 0 error.
5. `git commit -m "chore(lint): warn on direct lucide-react imports"`

## Verification

- `npm run test -- src/components/icons` → PASS (4 file test)
- `npm run typecheck` → PASS
- `npm run lint` → 0 error
- `npm run build` → PASS

## Exit Criteria

- Registry chứa mọi tên trong bảng; `AppIcon` qua toàn bộ test.
- `LEGACY` liệt kê đúng các file đang import `lucide-react`, test chặn xanh.
- Chưa có thay đổi nhìn thấy nào trên giao diện.
