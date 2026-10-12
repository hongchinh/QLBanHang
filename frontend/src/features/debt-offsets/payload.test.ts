import { describe, expect, it } from 'vitest';
import { toDateTimeLocalValue } from '@/lib/vn-datetime';
import { toCreatePayload } from './payload';

const P1 = '11111111-1111-4111-8111-111111111111';
const AT = '2026-10-07T01:30:00.000Z';

describe('debt offset payload', () => {
  it('maps rows with amounts', () => {
    expect(
      toCreatePayload({
        voucherAt: toDateTimeLocalValue(AT),
        partnerId: P1,
        note: '  Bù trừ tháng 10  ',
        receivables: [
          { entryId: 'r1', amount: 1_000_000 },
          { entryId: 'r2', amount: 0 },
          { entryId: 'r3', amount: 500_000.5 },
        ],
        payables: [
          { entryId: 'p0', amount: 0 },
          { entryId: 'p1', amount: 1_500_000.5 },
        ],
      }),
    ).toEqual({
      voucherAt: AT,
      partnerId: P1,
      note: 'Bù trừ tháng 10',
      receivables: [
        { entryId: 'r1', amount: 1_000_000 },
        { entryId: 'r3', amount: 500_000.5 },
      ],
      payables: [{ entryId: 'p1', amount: 1_500_000.5 }],
    });
  });

  it('sends no note when it is blank', () => {
    const payload = toCreatePayload({
      voucherAt: toDateTimeLocalValue(AT),
      partnerId: P1,
      note: '   ',
      receivables: [{ entryId: 'r1', amount: 100 }],
      payables: [{ entryId: 'p1', amount: 100 }],
    });

    expect(payload.note).toBeUndefined();
  });
});
