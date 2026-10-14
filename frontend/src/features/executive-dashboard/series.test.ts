import { describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { isCashSeriesEmpty, isSalesSeriesEmpty, usePeriodGranularity } from './series';

describe('isSalesSeriesEmpty', () => {
  const point = { date: '2026-10-01', revenue: 0, cost: 0, grossProfit: 0 };

  it('is empty without points or when every figure is 0 or null', () => {
    expect(isSalesSeriesEmpty([])).toBe(true);
    expect(isSalesSeriesEmpty([point, { ...point, cost: null, grossProfit: null }])).toBe(true);
  });

  it('is not empty when one figure is not 0', () => {
    expect(isSalesSeriesEmpty([point, { ...point, revenue: 5 }])).toBe(false);
    expect(isSalesSeriesEmpty([{ ...point, cost: 5 }])).toBe(false);
    expect(isSalesSeriesEmpty([{ ...point, grossProfit: -5 }])).toBe(false);
  });
});

describe('isCashSeriesEmpty', () => {
  const point = { date: '2026-10-01', receipts: 0, payments: 0 };

  it('is empty without points or movement', () => {
    expect(isCashSeriesEmpty([])).toBe(true);
    expect(isCashSeriesEmpty([point, point])).toBe(true);
  });

  it('receipts or payments alone are a movement', () => {
    expect(isCashSeriesEmpty([{ ...point, receipts: 1 }])).toBe(false);
    expect(isCashSeriesEmpty([{ ...point, payments: 1 }])).toBe(false);
  });
});

describe('usePeriodGranularity', () => {
  const A = { from: '2026-10-01', to: '2026-10-13' };
  const B = { from: '2026-08-01', to: '2026-10-13' };
  const renderGranularity = () =>
    renderHook(({ from, to }) => usePeriodGranularity(from, to), { initialProps: A });

  it('starts at the default of the period, keeps a choice, and drops it when the period changes', () => {
    const { result, rerender } = renderGranularity();
    expect(result.current[0]).toBe('Day');

    act(() => result.current[1]('Month'));
    expect(result.current[0]).toBe('Month');

    rerender(B);
    expect(result.current[0]).toBe('Week');
  });

  it('coming back to the first period gives its default, not the old choice', () => {
    const { result, rerender } = renderGranularity();
    act(() => result.current[1]('Month'));

    rerender(B);
    rerender(A);

    expect(result.current[0]).toBe('Day');
  });

  it('a choice made after the change is kept', () => {
    const { result, rerender } = renderGranularity();
    act(() => result.current[1]('Month'));
    rerender(B);

    act(() => result.current[1]('Day'));
    rerender(B);

    expect(result.current[0]).toBe('Day');
  });
});
