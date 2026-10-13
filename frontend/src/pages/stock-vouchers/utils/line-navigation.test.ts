import { describe, expect, it } from 'vitest';
import {
  DEFAULT_ENTER_STOPS,
  enterPath,
  isFieldDisabled,
  nextStop,
  prevStop,
  verticalTarget,
  type LineFocusField,
} from './line-navigation';

const all = () => true;

describe('enterPath', () => {
  it('follows the pricing mode with the default stops', () => {
    expect(enterPath('PerUnit', DEFAULT_ENTER_STOPS.PerUnit, all)).toEqual(['product-code', 'quantity', 'unit-price']);
    expect(enterPath('PerLinearMeter', DEFAULT_ENTER_STOPS.PerLinearMeter, all)).toEqual([
      'product-code', 'sheet-count', 'length', 'unit-price',
    ]);
    expect(enterPath('PerSquareMeter', DEFAULT_ENTER_STOPS.PerSquareMeter, all)).toEqual([
      'product-code', 'sheet-count', 'length', 'width', 'unit-price',
    ]);
    expect(enterPath('PerCubicMeter', DEFAULT_ENTER_STOPS.PerCubicMeter, all)).toEqual([
      'product-code', 'sheet-count', 'length', 'width', 'thickness', 'unit-price',
    ]);
  });

  it('keeps column order, drops fields disabled for the mode and hidden columns', () => {
    // Stops given out of order and with a field PerUnit cannot use.
    expect(enterPath('PerUnit', ['vat-rate', 'unit-price', 'length', 'quantity', 'warehouse'], all)).toEqual([
      'product-code', 'warehouse', 'quantity', 'unit-price', 'vat-rate',
    ]);
    const hideWarehouse = (f: LineFocusField) => f !== 'warehouse';
    expect(enterPath('PerUnit', ['warehouse', 'quantity'], hideWarehouse)).toEqual(['product-code', 'quantity']);
  });

  it('always starts with the product code, even with no stops', () => {
    expect(enterPath('PerSquareMeter', [], all)).toEqual(['product-code']);
  });
});

describe('nextStop / prevStop', () => {
  const path: LineFocusField[] = ['product-code', 'sheet-count', 'length', 'width', 'unit-price'];

  it('moves along the path and returns null past either end', () => {
    expect(nextStop(path, 'product-code')).toBe('sheet-count');
    expect(nextStop(path, 'unit-price')).toBeNull();
    expect(prevStop(path, 'sheet-count')).toBe('product-code');
    expect(prevStop(path, 'product-code')).toBeNull();
  });

  it('from a field outside the path, goes to the next/previous stop by column order', () => {
    // Reached by Tab: %CK sits after Đơn giá, so Enter leaves the row.
    expect(nextStop(path, 'discount-rate')).toBeNull();
    expect(prevStop(path, 'discount-rate')).toBe('unit-price');
    // Kho sits between Mã and Tấm.
    expect(nextStop(path, 'warehouse')).toBe('sheet-count');
  });
});

describe('verticalTarget', () => {
  it('keeps the column when the field is enabled and visible in the target row', () => {
    expect(verticalTarget('PerSquareMeter', 'width', all)).toBe('width');
  });

  it('falls back to the product code when the field is disabled or hidden there', () => {
    expect(verticalTarget('PerUnit', 'width', all)).toBe('product-code');
    expect(verticalTarget('PerUnit', 'note', (f) => f !== 'note')).toBe('product-code');
  });
});

describe('isFieldDisabled', () => {
  it('matches the pricing mode rules', () => {
    expect(isFieldDisabled('PerUnit', 'sheet-count')).toBe(true);
    expect(isFieldDisabled('PerUnit', 'quantity')).toBe(false);
    expect(isFieldDisabled('PerLinearMeter', 'width')).toBe(true);
    expect(isFieldDisabled('PerSquareMeter', 'thickness')).toBe(true);
    expect(isFieldDisabled('PerCubicMeter', 'thickness')).toBe(false);
    expect(isFieldDisabled('PerCubicMeter', 'quantity')).toBe(true);
  });
});
