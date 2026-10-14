import { describe, expect, it } from 'vitest';
import { HEADER_ERROR_FIELDS, LINE_ERROR_FIELDS, staleServerErrorKeys } from './server-field-errors';

// A line priced per unit whose Tiền CK follows %CK (not typed by hand).
const line = (overrides: Record<string, unknown> = {}) => ({
  productId: 'p1',
  productCode: 'SP01',
  warehouseId: 'w1',
  sheetCount: '',
  length: '',
  width: '',
  thickness: '',
  quantity: 2,
  unitPrice: 50_000,
  discountRate: 10,
  discountAmount: '',
  discountManual: false,
  vatRate: 10,
  note: '',
  ...overrides,
});

type Lines = ReturnType<typeof line>[];

// `lines` after the change; `previousLines` default to the same lines (a single cell changed).
const stale = (changed: string, lines: Lines = [line()], previousLines: Lines = lines) =>
  staleServerErrorKeys(changed, { lines, paidAmount: 100_000 }, previousLines);

// The paid amount (or another header field) changed and the form now holds `paidAmount`.
const staleWithPaid = (changed: string, paidAmount: unknown) =>
  staleServerErrorKeys(changed, { lines: [line()], paidAmount }, [line()]);

describe('staleServerErrorKeys', () => {
  it('lists the fields that can carry a server error', () => {
    expect([...HEADER_ERROR_FIELDS].sort()).toEqual(['dueDate', 'fundId', 'paidAmount', 'paymentMethodId']);
    expect(LINE_ERROR_FIELDS.has('quantity')).toBe(true);
    expect(LINE_ERROR_FIELDS.has('productCode')).toBe(false);
  });

  describe('a line cell', () => {
    it('makes its own error stale, on its own line only', () => {
      expect(stale('lines.0.note')).toEqual(['lines.0.note']);
      expect(stale('lines.3.vatRate', [line(), line(), line(), line()])).toEqual(['lines.3.vatRate']);
      expect(stale('lines.0.warehouseId')).toEqual(['lines.0.warehouseId']);
    });

    it('carries no error of its own when it is not a field the server reports on', () => {
      expect(stale('lines.0.productCode')).toEqual([]);
      expect(stale('lines.0.sortOrder')).toEqual([]);
    });

    it.each(['sheetCount', 'length', 'width', 'thickness'])(
      '%s also makes the SL error of its line stale (SL is computed from it)',
      (field) => {
        const lines = [line(), line({ discountManual: true, discountAmount: 20_000 })];
        expect(stale(`lines.1.${field}`, lines)).toEqual([`lines.1.${field}`, 'lines.1.quantity']);
      },
    );

    it.each(['discountRate', 'unitPrice', 'quantity', 'sheetCount', 'length', 'width', 'thickness'])(
      '%s also makes the error of a Tiền CK that follows %%CK stale',
      (field) => {
        const keys = stale(`lines.1.${field}`, [line(), line()]);
        expect(keys).toContain('lines.1.discountAmount');
        expect(keys).toContain(`lines.1.${field}`);
        expect(keys.every((key) => key.startsWith('lines.1.'))).toBe(true);
      },
    );

    it.each(['discountRate', 'unitPrice', 'quantity', 'sheetCount', 'length', 'width', 'thickness'])(
      '%s leaves the error of a Tiền CK typed by hand',
      (field) => {
        const lines = [line({ discountManual: true, discountAmount: 20_000 })];
        expect(stale(`lines.0.${field}`, lines)).not.toContain('lines.0.discountAmount');
      },
    );

    it.each(['vatRate', 'note', 'warehouseId', 'productId'])(
      '%s leaves the Tiền CK and SL errors (they are not computed from it)',
      (field) => {
        expect(stale(`lines.0.${field}`)).toEqual([`lines.0.${field}`]);
      },
    );

    it('the hand-typed switch makes the Tiền CK error stale: the cell shows the other amount', () => {
      // %CK was edited: the grid turns the hand-typed amount off first.
      expect(stale('lines.0.discountManual', [line({ discountManual: false })])).toEqual(['lines.0.discountAmount']);
      // Tiền CK was typed: the grid turns it on.
      expect(stale('lines.0.discountManual', [line({ discountManual: true })])).toEqual(['lines.0.discountAmount']);
    });

    it('reads the hand-typed switch of the changed line, not of another line', () => {
      const lines = [line({ discountManual: true, discountAmount: 20_000 }), line()];
      expect(stale('lines.1.unitPrice', lines)).toEqual(['lines.1.unitPrice', 'lines.1.discountAmount']);
      expect(stale('lines.0.unitPrice', lines)).toEqual(['lines.0.unitPrice']);
    });
  });

  describe('a whole line replaced at once (a product picked)', () => {
    it('makes stale only the fields whose value changed, and what is computed from them', () => {
      const before = [line({ quantity: 3, warehouseId: 'w1', note: 'giao gấp' })];
      // The same product picked again: SL goes back to 0, everything else is as it was.
      const after = [line({ quantity: 0, warehouseId: 'w1', note: 'giao gấp' })];
      expect(stale('lines.0', after, before).sort()).toEqual(['lines.0.discountAmount', 'lines.0.quantity']);
    });

    it('keeps the error of a field the pick did not change', () => {
      const before = [line({ productId: 'p1', unitPrice: 50_000, vatRate: 10, quantity: 3 })];
      const after = [line({ productId: 'p2', unitPrice: 70_000, vatRate: 8, quantity: 0 })];
      const keys = stale('lines.0', after, before);
      expect(keys).toEqual(expect.arrayContaining(['lines.0.productId', 'lines.0.unitPrice', 'lines.0.vatRate', 'lines.0.quantity']));
      expect(keys).not.toContain('lines.0.warehouseId');
      expect(keys).not.toContain('lines.0.note');
      expect(keys).not.toContain('lines.0.discountRate');
    });

    it('a changed dimension makes the SL error stale too', () => {
      const before = [line({ length: 3000 })];
      const after = [line({ length: 2000 })];
      expect(stale('lines.0', after, before).sort()).toEqual(['lines.0.discountAmount', 'lines.0.length', 'lines.0.quantity']);
    });

    it('a hand-typed Tiền CK the pick turned off is stale; nothing is when the line is unchanged', () => {
      const typed = [line({ discountManual: true, discountAmount: 20_000 })];
      expect(stale('lines.0', [line({ discountManual: false, discountAmount: '' })], typed)).toEqual(['lines.0.discountAmount']);
      expect(stale('lines.0', [line()], [line()])).toEqual([]);
    });

    it('never touches another line', () => {
      const before = [line({ quantity: 3 }), line({ quantity: 5 })];
      const after = [line({ quantity: 3 }), line({ quantity: 0 })];
      expect(stale('lines.1', after, before).every((key) => key.startsWith('lines.1.'))).toBe(true);
      expect(stale('lines.1', after, before)).toContain('lines.1.quantity');
    });

    it('treats every field of a line that was not there before as changed', () => {
      const keys = stale('lines.1', [line(), line()], [line()]);
      expect(keys).toEqual(expect.arrayContaining(['lines.1.productId', 'lines.1.warehouseId', 'lines.1.quantity']));
      // Only fields that can carry a server error.
      expect(keys).not.toContain('lines.1.productCode');
    });
  });

  describe('a header field', () => {
    it('makes its own error stale', () => {
      expect(stale('fundId')).toEqual(['fundId']);
      expect(stale('dueDate')).toEqual(['dueDate']);
    });

    it('the payment method drives the fund and the paid amount', () => {
      expect(stale('paymentMethodId')).toEqual(['paymentMethodId', 'fundId', 'paidAmount']);
    });

    it('the voucher date and the reason drive the due date', () => {
      expect(stale('voucherAt')).toEqual(['dueDate']);
      expect(stale('reasonId')).toEqual(['dueDate']);
    });

    it.each([0, '', undefined, null, -5])(
      'a paid amount of %s makes the fund error stale too: no fund is shown or sent',
      (paidAmount) => {
        expect(staleWithPaid('paidAmount', paidAmount)).toEqual(['paidAmount', 'fundId']);
      },
    );

    it('a paid amount that is still positive leaves the fund error: the fund is still needed', () => {
      expect(staleWithPaid('paidAmount', 100_000)).toEqual(['paidAmount']);
      expect(staleWithPaid('paidAmount', '1')).toEqual(['paidAmount']);
    });

    it('a field without a server error of its own, and the lines array itself, make nothing stale', () => {
      expect(stale('freight')).toEqual([]);
      expect(stale('partnerName')).toEqual([]);
      expect(staleWithPaid('paidAmountTouched', 0)).toEqual([]);
      expect(stale('lines')).toEqual([]);
    });
  });
});
