import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

const SRC = path.resolve(__dirname, '../..');
const ICONS_DIR = path.join(SRC, 'components', 'icons');
const LUCIDE_IMPORT = /(?:from\s+|import\s*\(?\s*)['"]lucide-react(?:\/[^'"]*)?['"]/;

// Files not yet moved to AppIcon. Each migration task removes its files; Phase 04 deletes this list.
const LEGACY: string[] = [
  'components/activity-history/activity-history.tsx',
  'components/confirm-required-dialog/confirm-required-dialog.tsx',
  'components/customer-autocomplete/customer-autocomplete.tsx',
  'components/layout/header/app-header.tsx',
  'components/layout/header/brand-block.tsx',
  'components/layout/header/header-notifications.tsx',
  'components/layout/header/header-search-mobile-sheet.tsx',
  'components/layout/header/header-search.tsx',
  'components/layout/header/header-user-menu.tsx',
  'components/layout/header/search-results-list.tsx',
  'components/layout/sidebar/sidebar-pin-chips.tsx',
  'components/layout/sidebar/sidebar-search.tsx',
  'components/layout/sidebar/sidebar.tsx',
  'components/partner-autocomplete/partner-autocomplete.tsx',
  'components/search-combobox/search-combobox.tsx',
  'components/tt133-export/export-targets-dialog.tsx',
  'components/tt133-export/tt133-export-button.tsx',
  'components/ui/dialog.tsx',
  'components/ui/dropdown-menu.tsx',
  'components/ui/multi-select.tsx',
  'components/ui/select.tsx',
  'components/ui/sheet.tsx',
  'components/ui/toaster.tsx',
  'components/voucher-print/voucher-print-button.tsx',
  'features/customers/components/customer-catalog-list.tsx',
  'features/products/components/product-catalog-list.tsx',
  'pages/admin/components/role-matrix-table.tsx',
  'pages/admin/components/user-actions-menu.tsx',
  'pages/admin/roles-matrix-page.tsx',
  'pages/admin/user-settings-page.tsx',
  'pages/admin/users-list-page.tsx',
  'pages/cash-reasons/cash-reason-list-page.tsx',
  'pages/cash-vouchers/cash-voucher-form-page.tsx',
  'pages/cash-vouchers/cash-voucher-list-page.tsx',
  'pages/cash-vouchers/components/cash-line-grid.tsx',
  'pages/cash-vouchers/components/cash-partner-filter.tsx',
  'pages/credit-limits/credit-limit-list-page.tsx',
  'pages/customers/customer-form-fields.tsx',
  'pages/customers/customer-list-page.tsx',
  'pages/customers/customer-quotations-section.tsx',
  'pages/debt/components/settlement-list.tsx',
  'pages/debt/debt-matching-page.tsx',
  'pages/debt/debt-offset-form-page.tsx',
  'pages/debt/debt-offset-list-page.tsx',
  'pages/debt/opening-debt-page.tsx',
  'pages/fund-transfers/fund-transfer-form-page.tsx',
  'pages/fund-transfers/fund-transfer-list-page.tsx',
  'pages/funds/fund-list-page.tsx',
  'pages/inventory/opening-stock-page.tsx',
  'pages/payment-methods/payment-method-list-page.tsx',
  'pages/product-groups/product-group-list-page.tsx',
  'pages/products/product-form-page.tsx',
  'pages/products/product-list-page.tsx',
  'pages/quotations/components/line-items-grid.tsx',
  'pages/quotations/components/product-typeahead-cell.tsx',
  'pages/quotations/components/totals-panel.tsx',
  'pages/quotations/quotation-form-page.tsx',
  'pages/quotations/quotation-list-page.tsx',
  'pages/reports/revenue-page.tsx',
  'pages/reports/sales-performance-page.tsx',
  'pages/reports/sales-revenue-detail-page.tsx',
  'pages/settings/branches-page.tsx',
  'pages/settings/recalc-cost-page.tsx',
  'pages/settings/settings-hub-page.tsx',
  'pages/stock-reasons/stock-reason-list-page.tsx',
  'pages/stock-vouchers/components/negative-stock-dialog.tsx',
  'pages/stock-vouchers/components/stock-grid-settings-popover.tsx',
  'pages/stock-vouchers/components/stock-line-grid.tsx',
  'pages/stock-vouchers/stock-voucher-form-page.tsx',
  'pages/stock-vouchers/stock-voucher-list-page.tsx',
  'pages/suppliers/supplier-list-page.tsx',
  'pages/warehouses/warehouse-list-page.tsx',
];

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
      .filter((file) => !file.startsWith(ICONS_DIR + path.sep))
      .filter((file) => LUCIDE_IMPORT.test(readFileSync(file, 'utf8')))
      .map((file) => path.relative(SRC, file).split(path.sep).join('/'))
      .sort();
    expect(offenders).toEqual([...LEGACY].sort());
  });
});
