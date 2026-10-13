export type StockVoucherTab = 'lines' | 'other' | 'payment' | 'history';
export type ErrorTab = Exclude<StockVoucherTab, 'history'>;

const TAB_ORDER: readonly ErrorTab[] = ['lines', 'other', 'payment'];

const FIELD_TABS: Record<string, ErrorTab> = {
  lines: 'lines',
  partnerTaxCode: 'other',
  handlerName: 'other',
  note: 'other',
  freight: 'payment',
  orderDiscount: 'payment',
  paidAmount: 'payment',
  fundId: 'payment',
  dueDate: 'payment',
};

// A form key (`note`), a nested path (`lines.3.width`) or a server key (`lines[3].width`); null = main area.
export function tabOfField(key: string): ErrorTab | null {
  return FIELD_TABS[key.split(/[.[]/)[0]] ?? null;
}

export function errorTabs(keys: Iterable<string>): Set<ErrorTab> {
  const tabs = new Set<ErrorTab>();
  for (const key of keys) {
    const tab = tabOfField(key);
    if (tab) tabs.add(tab);
  }
  return tabs;
}

export function firstErrorTab(keys: Iterable<string>): ErrorTab | null {
  const tabs = errorTabs(keys);
  return TAB_ORDER.find((t) => tabs.has(t)) ?? null;
}
