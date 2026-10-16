import { describe, expect, it } from 'vitest';
import {
  DIMENSION_FIELDS,
  PRICING_MODES,
  PRICING_MODE_RANK,
  highestPricingMode,
  isDimensionField,
  normalizePricingMode,
  shownDimensions,
  usesDimension,
  type DimensionField,
} from './dimension-columns';

describe('DIMENSION_FIELDS / PRICING_MODES / PRICING_MODE_RANK', () => {
  it('lists the dimensions in column order and the modes in rank order', () => {
    expect(DIMENSION_FIELDS).toEqual(['sheet-count', 'length', 'width', 'thickness']);
    expect(PRICING_MODES).toEqual(['PerUnit', 'PerLinearMeter', 'PerSquareMeter', 'PerCubicMeter']);
    expect(PRICING_MODE_RANK).toEqual({ PerUnit: 0, PerLinearMeter: 1, PerSquareMeter: 2, PerCubicMeter: 3 });
  });
});

describe('isDimensionField', () => {
  it('is true only for Số tấm, Dài, Rộng, Dày', () => {
    for (const field of DIMENSION_FIELDS) expect(isDimensionField(field)).toBe(true);
    for (const field of ['product-code', 'name', 'unit', 'warehouse', 'quantity', 'unit-price', 'unit-cost', 'note', '']) {
      expect(isDimensionField(field)).toBe(false);
    }
  });
});

describe('normalizePricingMode', () => {
  it('keeps each of the four modes', () => {
    for (const mode of PRICING_MODES) expect(normalizePricingMode(mode)).toBe(mode);
  });

  it.each([undefined, null, '', 'Legacy', 'perunit', 'toString', 2])('treats %j as PerUnit', (mode) => {
    expect(normalizePricingMode(mode)).toBe('PerUnit');
  });
});

describe('usesDimension', () => {
  it.each([
    ['PerUnit', 'sheet-count', false],
    ['PerUnit', 'length', false],
    ['PerUnit', 'width', false],
    ['PerUnit', 'thickness', false],
    ['PerLinearMeter', 'sheet-count', true],
    ['PerLinearMeter', 'length', true],
    ['PerLinearMeter', 'width', false],
    ['PerLinearMeter', 'thickness', false],
    ['PerSquareMeter', 'sheet-count', true],
    ['PerSquareMeter', 'length', true],
    ['PerSquareMeter', 'width', true],
    ['PerSquareMeter', 'thickness', false],
    ['PerCubicMeter', 'sheet-count', true],
    ['PerCubicMeter', 'length', true],
    ['PerCubicMeter', 'width', true],
    ['PerCubicMeter', 'thickness', true],
  ] as const)('%s uses %s: %s', (mode, field, expected) => {
    expect(usesDimension(mode, field)).toBe(expected);
  });

  it('uses no dimension for a missing or unknown mode', () => {
    for (const mode of [undefined, null, 'Legacy']) {
      expect(DIMENSION_FIELDS.map((field) => usesDimension(mode, field))).toEqual([false, false, false, false]);
    }
  });
});

describe('highestPricingMode', () => {
  it('is PerUnit when there is no line', () => {
    expect(highestPricingMode([])).toBe('PerUnit');
  });

  it('ranks md + m² as m² in either order (the PricingMode union lists m² before md)', () => {
    expect(highestPricingMode([{ pricingMode: 'PerLinearMeter' }, { pricingMode: 'PerSquareMeter' }])).toBe('PerSquareMeter');
    expect(highestPricingMode([{ pricingMode: 'PerSquareMeter' }, { pricingMode: 'PerLinearMeter' }])).toBe('PerSquareMeter');
  });

  it('picks m³ over every other mode', () => {
    expect(
      highestPricingMode([{ pricingMode: 'PerUnit' }, { pricingMode: 'PerCubicMeter' }, { pricingMode: 'PerSquareMeter' }]),
    ).toBe('PerCubicMeter');
  });

  it('counts a missing or unknown mode as PerUnit', () => {
    expect(highestPricingMode([{}, { pricingMode: 'Legacy' }, { pricingMode: null }])).toBe('PerUnit');
    expect(highestPricingMode([{ pricingMode: 'Legacy' }, { pricingMode: 'PerLinearMeter' }])).toBe('PerLinearMeter');
  });
});

describe('shownDimensions', () => {
  it('shows no dimension for no line, a blank line or unit lines', () => {
    expect(shownDimensions([])).toEqual([]);
    expect(shownDimensions([{}, { pricingMode: 'PerUnit' }])).toEqual([]);
  });

  it('follows the highest mode on the voucher', () => {
    expect(shownDimensions([{ pricingMode: 'PerUnit' }, { pricingMode: 'PerLinearMeter' }])).toEqual(['sheet-count', 'length']);
    expect(shownDimensions([{ pricingMode: 'PerLinearMeter' }, { pricingMode: 'PerSquareMeter' }])).toEqual([
      'sheet-count', 'length', 'width',
    ]);
    expect(shownDimensions([{ pricingMode: 'PerCubicMeter' }, { pricingMode: 'PerUnit' }])).toEqual([
      'sheet-count', 'length', 'width', 'thickness',
    ]);
  });

  it('counts a missing or unknown mode as PerUnit', () => {
    expect(shownDimensions([{ pricingMode: 'Legacy' }, { pricingMode: undefined }])).toEqual([]);
  });

  it('adds forced columns, in column order', () => {
    expect(shownDimensions([{ pricingMode: 'PerUnit' }], ['width'])).toEqual(['width']);
    expect(
      shownDimensions([{ pricingMode: 'PerLinearMeter' }], new Set<DimensionField>(['thickness', 'sheet-count'])),
    ).toEqual(['sheet-count', 'length', 'thickness']);
    expect(shownDimensions([{ pricingMode: 'PerCubicMeter' }], ['width'])).toEqual([
      'sheet-count', 'length', 'width', 'thickness',
    ]);
    expect(shownDimensions([{ pricingMode: 'PerUnit' }], ['thickness', 'width'])).toEqual(['width', 'thickness']);
    expect(shownDimensions([], [])).toEqual([]);
  });
});
