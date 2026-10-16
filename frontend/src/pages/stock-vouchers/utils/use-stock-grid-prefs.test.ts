import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import {
  DEFAULT_STOCK_GRID_PREFS,
  dimensionErrorFields,
  forcedColumns,
  readStockGridPrefs,
  stockGridPrefsKey,
  useStockGridPrefs,
} from './use-stock-grid-prefs';

describe('readStockGridPrefs', () => {
  beforeEach(() => localStorage.clear());

  it('returns the defaults when nothing is stored', () => {
    expect(readStockGridPrefs('u1', 'In')).toEqual(DEFAULT_STOCK_GRID_PREFS);
    expect(DEFAULT_STOCK_GRID_PREFS.hiddenColumns).toEqual(['warehouse', 'discount-amount', 'vat-amount', 'note']);
  });

  it('keys by user and direction', () => {
    expect(stockGridPrefsKey('u1', 'Out')).toBe('qldh:stock-grid-prefs:u1:Out');
    localStorage.setItem(
      stockGridPrefsKey('u1', 'Out'),
      JSON.stringify({ version: 1, hiddenColumns: ['note'], enterStops: {} }),
    );
    expect(readStockGridPrefs('u1', 'Out').hiddenColumns).toEqual(['note']);
    expect(readStockGridPrefs('u1', 'In')).toEqual(DEFAULT_STOCK_GRID_PREFS);
    expect(readStockGridPrefs('u2', 'Out')).toEqual(DEFAULT_STOCK_GRID_PREFS);
  });

  it('falls back per part on corrupt, old or foreign data', () => {
    localStorage.setItem(stockGridPrefsKey('u1', 'In'), '{not json');
    expect(readStockGridPrefs('u1', 'In')).toEqual(DEFAULT_STOCK_GRID_PREFS);

    localStorage.setItem(stockGridPrefsKey('u1', 'In'), JSON.stringify({ version: 0, hiddenColumns: [] }));
    expect(readStockGridPrefs('u1', 'In')).toEqual(DEFAULT_STOCK_GRID_PREFS);

    localStorage.setItem(
      stockGridPrefsKey('u1', 'In'),
      JSON.stringify({
        version: 1,
        hiddenColumns: ['note', 'bogus', 'note'],
        // Unknown field and a field PerUnit cannot use are dropped; order follows the columns.
        enterStops: { PerUnit: ['unit-price', 'bogus', 'width', 'vat-rate'], PerSquareMeter: 'x' },
      }),
    );
    const prefs = readStockGridPrefs('u1', 'In');
    expect(prefs.hiddenColumns).toEqual(['note']);
    expect(prefs.enterStops.PerUnit).toEqual(['unit-price', 'vat-rate']);
    expect(prefs.enterStops.PerSquareMeter).toEqual(DEFAULT_STOCK_GRID_PREFS.enterStops.PerSquareMeter);
  });

  it('puts stored stops in column order and falls back to the defaults for a non-array hiddenColumns', () => {
    localStorage.setItem(
      stockGridPrefsKey('u1', 'In'),
      JSON.stringify({ version: 1, hiddenColumns: 'x', enterStops: { PerSquareMeter: ['unit-price', 'sheet-count'] } }),
    );
    const prefs = readStockGridPrefs('u1', 'In');
    expect(prefs.enterStops.PerSquareMeter).toEqual(['sheet-count', 'unit-price']);
    expect(prefs.hiddenColumns).toEqual(DEFAULT_STOCK_GRID_PREFS.hiddenColumns);
  });

  it('survives a throwing localStorage', () => {
    const spy = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    expect(readStockGridPrefs('u1', 'In')).toEqual(DEFAULT_STOCK_GRID_PREFS);
    spy.mockRestore();
  });
});

describe('useStockGridPrefs', () => {
  beforeEach(() => localStorage.clear());

  it('toggles a column and an enter stop, persists them, and resets', () => {
    const { result } = renderHook(() => useStockGridPrefs('u1', 'In'));

    act(() => result.current.toggleColumn('note'));
    expect(result.current.prefs.hiddenColumns).not.toContain('note');
    act(() => result.current.toggleEnterStop('PerUnit', 'vat-rate'));
    expect(result.current.prefs.enterStops.PerUnit).toEqual(['quantity', 'unit-price', 'vat-rate']);
    // A field the mode cannot use is ignored.
    act(() => result.current.toggleEnterStop('PerUnit', 'width'));
    expect(result.current.prefs.enterStops.PerUnit).toEqual(['quantity', 'unit-price', 'vat-rate']);
    expect(readStockGridPrefs('u1', 'In')).toEqual(result.current.prefs);

    act(() => result.current.reset());
    expect(result.current.prefs).toEqual(DEFAULT_STOCK_GRID_PREFS);
    expect(localStorage.getItem(stockGridPrefsKey('u1', 'In'))).toBeNull();
  });

  it('inserts a toggled stop and a re-shown column in column order, not at the end', () => {
    const { result } = renderHook(() => useStockGridPrefs('u1', 'In'));
    act(() => result.current.toggleEnterStop('PerUnit', 'warehouse'));
    expect(result.current.prefs.enterStops.PerUnit).toEqual(['warehouse', 'quantity', 'unit-price']);
    // Show Kho, then hide it again: the stored list keeps the column order of OPTIONAL_COLUMNS.
    act(() => result.current.toggleColumn('warehouse'));
    act(() => result.current.toggleColumn('warehouse'));
    expect(result.current.prefs.hiddenColumns).toEqual(['warehouse', 'discount-amount', 'vat-amount', 'note']);
  });

  it('keeps working when localStorage cannot be written', () => {
    const spy = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('quota');
    });
    const { result } = renderHook(() => useStockGridPrefs('u1', 'In'));
    expect(() => act(() => result.current.toggleColumn('note'))).not.toThrow();
    // The change still applies for this page.
    expect(result.current.prefs.hiddenColumns).not.toContain('note');
    spy.mockRestore();
  });
});

describe('forcedColumns', () => {
  it('shows Kho when a line uses another warehouse, and any column with a line error', () => {
    const lines = [{ warehouseId: 'w1' }, { warehouseId: 'w2' }, { warehouseId: '' }];
    expect([...forcedColumns(lines, 'w1', undefined)]).toEqual(['warehouse']);
    expect([...forcedColumns([{ warehouseId: 'w1' }], 'w1', undefined)]).toEqual([]);
    // A line without a warehouse yet is not "another warehouse".
    expect([...forcedColumns([{ warehouseId: '' }], 'w1', undefined)]).toEqual([]);
    const errors = [undefined, { note: { message: 'x' }, discountAmount: { message: 'y' }, quantity: { message: 'z' } }];
    expect([...forcedColumns([{ warehouseId: 'w1' }], 'w1', errors)].sort()).toEqual(['discount-amount', 'note']);
  });

  it('ignores an array-level error, which react-hook-form gives as an object, not an array', () => {
    // Empty grid submitted: zod `min(1)` sets errors.lines to { root: ... } or { message, type }.
    expect([...forcedColumns([], 'w1', { root: { message: 'x' } })]).toEqual([]);
    expect([...forcedColumns([], 'w1', { message: 'x', type: 'too_small' })]).toEqual([]);
  });
});

describe('dimensionErrorFields', () => {
  it('lists the dimension columns with an error on any line', () => {
    const errors = [
      undefined,
      { width: { message: 'a' }, note: { message: 'b' } },
      { sheetCount: { message: 'c' }, quantity: { message: 'd' } },
    ];
    expect([...dimensionErrorFields(errors)].sort()).toEqual(['sheet-count', 'width']);
    expect([...dimensionErrorFields([{ length: { message: 'e' }, thickness: { message: 'f' } }])].sort()).toEqual([
      'length',
      'thickness',
    ]);
    expect([...dimensionErrorFields(undefined)]).toEqual([]);
  });

  it('ignores an array-level error, which react-hook-form gives as an object, not an array', () => {
    expect([...dimensionErrorFields({ root: { message: 'x' } })]).toEqual([]);
    expect([...dimensionErrorFields({ message: 'x', type: 'too_small' })]).toEqual([]);
  });
});
