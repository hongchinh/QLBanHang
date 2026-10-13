import { describe, expect, it } from 'vitest';
import { debtAfterVoucher, savedImpactOf } from './debt-after-voucher';

const summary = { receivableBalance: 7_000_000, payableBalance: 500_000, creditLimit: 10_000_000 };

describe('debtAfterVoucher', () => {
  it('ReceivableIncrease adds the unpaid part to the receivable', () => {
    expect(debtAfterVoucher(summary, { effect: 'ReceivableIncrease', total: 4_000_000, paid: 1_000_000 })).toEqual({
      receivableAfter: 10_000_000,
      payableAfter: 500_000,
      availableAfter: 0,
      receivableChanged: true,
      payableChanged: false,
    });
  });

  it('ReceivableDecrease, PayableIncrease and PayableDecrease move their side', () => {
    expect(debtAfterVoucher(summary, { effect: 'ReceivableDecrease', total: 2_000_000, paid: 0 }).receivableAfter).toBe(5_000_000);
    const buy = debtAfterVoucher(summary, { effect: 'PayableIncrease', total: 3_000_000, paid: 1_000_000 });
    expect(buy).toMatchObject({ receivableAfter: 7_000_000, payableAfter: 2_500_000, receivableChanged: false, payableChanged: true });
    expect(debtAfterVoucher(summary, { effect: 'PayableDecrease', total: 200_000, paid: 0 }).payableAfter).toBe(300_000);
  });

  it('None changes nothing', () => {
    expect(debtAfterVoucher(summary, { effect: 'None', total: 9_000_000, paid: 0 })).toMatchObject({
      receivableAfter: 7_000_000,
      payableAfter: 500_000,
      receivableChanged: false,
      payableChanged: false,
    });
  });

  it('an edited voucher replaces its saved impact, which the balance already holds', () => {
    const saved = { effect: 'ReceivableIncrease' as const, total: 4_000_000, paid: 0 };
    expect(debtAfterVoucher(summary, { effect: 'ReceivableIncrease', total: 5_000_000, paid: 0 }, saved).receivableAfter).toBe(8_000_000);
    // Reason changed to one without debt: the saved impact goes away.
    const none = debtAfterVoucher(summary, { effect: 'None', total: 5_000_000, paid: 0 }, saved);
    expect(none.receivableAfter).toBe(3_000_000);
    expect(none.receivableChanged).toBe(true);
  });

  it('an edited purchase subtracts the saved payable impact, including its paid part', () => {
    const saved = { effect: 'PayableIncrease' as const, total: 3_000_000, paid: 1_000_000 };
    const r = debtAfterVoucher(summary, { effect: 'PayableIncrease', total: 4_000_000, paid: 1_000_000 }, saved);
    // 500.000 + 3.000.000 (new net) - 2.000.000 (saved net)
    expect(r.payableAfter).toBe(1_500_000);
    expect(r.payableChanged).toBe(true);
    // Reason changed to one without debt: the saved payable impact goes away and the side still counts as changed.
    const none = debtAfterVoucher(summary, { effect: 'None', total: 0, paid: 0 }, saved);
    expect(none.payableAfter).toBe(-1_500_000);
    expect(none.payableChanged).toBe(true);
  });

  it('a saved paid amount reduces the saved impact that is taken back', () => {
    const saved = { effect: 'ReceivableIncrease' as const, total: 4_000_000, paid: 1_000_000 };
    // Saved net 3.000.000 is taken out of 7.000.000 when the reason loses its debt effect.
    expect(debtAfterVoucher(summary, { effect: 'None', total: 0, paid: 0 }, saved).receivableAfter).toBe(4_000_000);
  });

  it('no credit limit gives no available amount', () => {
    const r = debtAfterVoucher({ ...summary, creditLimit: null }, { effect: 'ReceivableIncrease', total: 1, paid: 0 });
    expect(r.availableAfter).toBeNull();
  });
});

describe('savedImpactOf', () => {
  const voucher = {
    status: 'Active',
    partnerId: 'p1',
    reasonDebtEffect: 'ReceivableIncrease',
    total: 4_000_000,
    paidAmount: 500_000,
  } as const;

  it('gives the stored impact of an Active voucher whose partner is still selected', () => {
    expect(savedImpactOf(voucher, 'p1')).toEqual({ effect: 'ReceivableIncrease', total: 4_000_000, paid: 500_000 });
  });

  it('gives nothing for a new voucher, another partner, a cancelled voucher or no partner', () => {
    expect(savedImpactOf(undefined, 'p1')).toBeUndefined();
    // Partner changed while editing: the other partner's balance never held this voucher.
    expect(savedImpactOf(voucher, 'p2')).toBeUndefined();
    expect(savedImpactOf({ ...voucher, status: 'Cancelled' }, 'p1')).toBeUndefined();
    expect(savedImpactOf(voucher, undefined)).toBeUndefined();
  });
});
