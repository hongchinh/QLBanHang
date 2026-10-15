import { beforeEach, describe, expect, it } from 'vitest';
import { useUiStore } from './ui-store';

const STORAGE_KEY = 'qldonhang-ui-store';

function readPersisted(): { state: Record<string, unknown>; version?: number } | null {
  const raw = localStorage.getItem(STORAGE_KEY);
  return raw ? JSON.parse(raw) : null;
}

describe('useUiStore quotationStatusFilter', () => {
  beforeEach(() => {
    localStorage.clear();
    useUiStore.setState({ sidebarCollapsed: false, quotationStatusFilter: null });
  });

  it('starts as null', () => {
    expect(useUiStore.getState().quotationStatusFilter).toBeNull();
  });

  it('setQuotationStatusFilter updates state', () => {
    useUiStore.getState().setQuotationStatusFilter(['Confirmed', 'AccountingConfirmed']);
    expect(useUiStore.getState().quotationStatusFilter).toEqual([
      'Confirmed',
      'AccountingConfirmed',
    ]);
  });

  it('persists the selection to localStorage', () => {
    useUiStore.getState().setQuotationStatusFilter(['Confirmed']);
    expect(readPersisted()?.state.quotationStatusFilter).toEqual(['Confirmed']);
  });

  it('persists an empty selection as an empty array, not null', () => {
    useUiStore.getState().setQuotationStatusFilter([]);
    expect(useUiStore.getState().quotationStatusFilter).toEqual([]);
    expect(readPersisted()?.state.quotationStatusFilter).toEqual([]);
  });
});

describe('useUiStore rehydration from a pre-existing persisted state', () => {
  beforeEach(() => {
    localStorage.clear();
    useUiStore.setState({ sidebarCollapsed: false, quotationStatusFilter: null });
  });

  it('keeps quotationStatusFilter null when the stored payload predates it', async () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ state: { sidebarCollapsed: true }, version: 1 }),
    );

    await useUiStore.persist.rehydrate();

    expect(useUiStore.getState().sidebarCollapsed).toBe(true);
    expect(useUiStore.getState().quotationStatusFilter).toBeNull();
  });

  it('restores a stored quotationStatusFilter', async () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        state: { sidebarCollapsed: false, quotationStatusFilter: ['Confirmed'] },
        version: 1,
      }),
    );

    await useUiStore.persist.rehydrate();

    expect(useUiStore.getState().quotationStatusFilter).toEqual(['Confirmed']);
  });
});

describe('useUiStore stockVoucherStatusFilter', () => {
  beforeEach(() => {
    localStorage.clear();
    useUiStore.setState({ stockVoucherStatusFilter: { In: null, Out: null } });
  });

  it('persists stock voucher status filter per type', () => {
    useUiStore.getState().setStockVoucherStatusFilter('In', 'Cancelled');

    expect(useUiStore.getState().stockVoucherStatusFilter).toEqual({ In: 'Cancelled', Out: null });
    expect(readPersisted()?.state.stockVoucherStatusFilter).toEqual({ In: 'Cancelled', Out: null });

    useUiStore.getState().setStockVoucherStatusFilter('Out', 'Active');
    expect(readPersisted()?.state.stockVoucherStatusFilter).toEqual({ In: 'Cancelled', Out: 'Active' });
  });

  it('a stored all is restored', async () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        state: { sidebarCollapsed: false, stockVoucherStatusFilter: { In: 'all', Out: null } },
        version: 1,
      }),
    );

    await useUiStore.persist.rehydrate();

    expect(useUiStore.getState().stockVoucherStatusFilter).toEqual({ In: 'all', Out: null });
  });
});

describe('useUiStore sidebarGroupOpen', () => {
  beforeEach(() => {
    localStorage.clear();
    useUiStore.setState({ sidebarCollapsed: false, sidebarGroupOpen: {} });
  });

  it('starts empty so groups use their defaults', () => {
    expect(useUiStore.getState().sidebarGroupOpen).toEqual({});
  });

  it('records and persists the user choice per group', () => {
    useUiStore.getState().setSidebarGroupOpen('catalogs', true);
    useUiStore.getState().setSidebarGroupOpen('sales', false);
    expect(useUiStore.getState().sidebarGroupOpen).toEqual({ catalogs: true, sales: false });
    expect(readPersisted()?.state.sidebarGroupOpen).toEqual({ catalogs: true, sales: false });
  });

  it('loads a version 1 payload that predates the field', async () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ state: { sidebarCollapsed: true }, version: 1 }));
    await useUiStore.persist.rehydrate();
    expect(useUiStore.getState().sidebarCollapsed).toBe(true);
    expect(useUiStore.getState().sidebarGroupOpen).toEqual({});
  });
});

describe('useUiStore pinnedNavPaths', () => {
  beforeEach(() => {
    localStorage.clear();
    useUiStore.setState({ pinnedNavPaths: {} });
  });

  it('keeps pins per user in the order they were added', () => {
    const { togglePin } = useUiStore.getState();
    togglePin('u1', '/receipts');
    togglePin('u1', '/quotations');
    togglePin('u2', '/stock-in');
    expect(useUiStore.getState().pinnedNavPaths).toEqual({
      u1: ['/receipts', '/quotations'],
      u2: ['/stock-in'],
    });
  });

  it('toggling a pinned path removes it', () => {
    const { togglePin } = useUiStore.getState();
    togglePin('u1', '/receipts');
    togglePin('u1', '/quotations');
    togglePin('u1', '/receipts');
    expect(useUiStore.getState().pinnedNavPaths).toEqual({ u1: ['/quotations'] });
  });

  it('persists the pins', () => {
    useUiStore.getState().togglePin('u1', '/receipts');
    expect(readPersisted()?.state.pinnedNavPaths).toEqual({ u1: ['/receipts'] });
  });

  it('loads a version 1 payload that predates the field', async () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ state: { sidebarCollapsed: false }, version: 1 }));
    await useUiStore.persist.rehydrate();
    expect(useUiStore.getState().pinnedNavPaths).toEqual({});
  });
});

describe('useUiStore help', () => {
  beforeEach(() => {
    localStorage.clear();
    useUiStore.setState({ helpOpen: false, helpHintSeen: {}, helpHintsOff: {} });
  });

  it('openHelp, closeHelp and toggleHelp set helpOpen', () => {
    useUiStore.getState().openHelp();
    expect(useUiStore.getState().helpOpen).toBe(true);
    useUiStore.getState().closeHelp();
    expect(useUiStore.getState().helpOpen).toBe(false);
    useUiStore.getState().toggleHelp();
    expect(useUiStore.getState().helpOpen).toBe(true);
    useUiStore.getState().toggleHelp();
    expect(useUiStore.getState().helpOpen).toBe(false);
  });

  it('markHelpHintSeen keeps one entry per key and a list per user', () => {
    useUiStore.getState().markHelpHintSeen('u1', 'stock-out.form');
    useUiStore.getState().markHelpHintSeen('u1', 'stock-out.form');
    expect(useUiStore.getState().helpHintSeen).toEqual({ u1: ['stock-out.form'] });
    useUiStore.getState().markHelpHintSeen('u2', 'receipts.form');
    expect(useUiStore.getState().helpHintSeen).toEqual({ u1: ['stock-out.form'], u2: ['receipts.form'] });
  });

  it('turnOffHelpHints turns the hints off for that user only', () => {
    useUiStore.getState().turnOffHelpHints('u1');
    expect(useUiStore.getState().helpHintsOff.u1).toBe(true);
    expect(useUiStore.getState().helpHintsOff.u2).toBeUndefined();
  });

  it('persists the hint state but not whether the panel is open', () => {
    useUiStore.getState().openHelp();
    useUiStore.getState().markHelpHintSeen('u1', 'stock-out.form');
    useUiStore.getState().turnOffHelpHints('u1');
    const state = readPersisted()?.state;
    expect(state?.helpHintSeen).toEqual({ u1: ['stock-out.form'] });
    expect(state?.helpHintsOff).toEqual({ u1: true });
    expect(state).not.toHaveProperty('helpOpen');
  });
});
