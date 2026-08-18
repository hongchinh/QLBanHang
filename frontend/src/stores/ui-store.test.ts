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
