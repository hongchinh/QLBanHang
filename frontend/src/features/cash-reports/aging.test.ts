import { describe, expect, it } from 'vitest';
import { bucketLabel, DEFAULT_BUCKETS, normalizeBuckets } from './aging';

describe('normalizeBuckets', () => {
  it('defaults when blank', () => {
    expect(DEFAULT_BUCKETS).toBe('30,60,90');
    expect(normalizeBuckets('')).toBe('30,60,90');
    expect(normalizeBuckets('   ')).toBe('30,60,90');
  });

  it('trims and joins valid boundaries', () => {
    expect(normalizeBuckets(' 15 , 45 ')).toBe('15,45');
    expect(normalizeBuckets('7')).toBe('7');
    expect(normalizeBuckets('1,2,3,4,5,6')).toBe('1,2,3,4,5,6');
    expect(normalizeBuckets('36500')).toBe('36500');
  });

  it.each([
    '60,30',
    '30,30',
    '0,30',
    'abc',
    '30,,60',
    '1,2,3,4,5,6,7',
    '+5',
    '1e2',
    '30abc',
    '99999999999',
    '36501',
    '30,50000',
  ])('rejects %s', (input) => {
    expect(normalizeBuckets(input)).toBeNull();
  });
});

describe('bucketLabel', () => {
  it('formats closed and open-ended buckets', () => {
    expect(bucketLabel({ from: 0, to: 30 })).toBe('0–30');
    expect(bucketLabel({ from: 31, to: 60 })).toBe('31–60');
    expect(bucketLabel({ from: 91, to: null })).toBe('> 90');
    expect(bucketLabel({ from: 1, to: 30 })).toBe('1–30');
  });
});
