import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { QuotationDateFilter } from './quotation-date-filter';

const buttonLabels = () => screen.getAllByRole('button').map((b) => b.textContent);
const activeLabels = () =>
  screen
    .getAllByRole('button')
    .filter((b) => b.className.includes('bg-foreground'))
    .map((b) => b.textContent);

describe('QuotationDateFilter', () => {
  beforeEach(() => {
    // Tuesday 13 October 2026.
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 13, 10, 0, 0));
  });
  afterEach(() => vi.useRealTimers());

  it('shows "Tất cả", its four presets and the custom button', () => {
    render(<QuotationDateFilter from="" to="" onChange={vi.fn()} />);

    expect(buttonLabels()).toEqual(['Tất cả', '7N', '30N', 'Tháng này', 'Tháng trước', 'Tuỳ chỉnh ▾']);
    expect(activeLabels()).toEqual(['Tất cả']);
  });

  it.each([
    ['2026-10-07', '2026-10-13', '7N'],
    ['2026-09-14', '2026-10-13', '30N'],
    ['2026-10-01', '2026-10-13', 'Tháng này'],
    ['2026-09-01', '2026-09-30', 'Tháng trước'],
    ['2026-10-02', '2026-10-13', 'Tuỳ chỉnh ▾'],
  ])('%s … %s marks "%s"', (from, to, label) => {
    render(<QuotationDateFilter from={from} to={to} onChange={vi.fn()} />);

    expect(activeLabels()).toEqual([label]);
  });

  // Today, this week and this year are presets of other screens, not of this filter.
  it.each([
    ['2026-10-13', '2026-10-13'],
    ['2026-10-12', '2026-10-13'],
    ['2026-01-01', '2026-10-13'],
  ])('%s … %s is a custom range here', (from, to) => {
    render(<QuotationDateFilter from={from} to={to} onChange={vi.fn()} />);

    expect(activeLabels()).toEqual(['Tuỳ chỉnh ▾']);
  });

  it('clicking a preset reports its range', () => {
    const onChange = vi.fn();
    render(<QuotationDateFilter from="" to="" onChange={onChange} />);

    fireEvent.click(screen.getByText('Tháng trước'));
    expect(onChange).toHaveBeenLastCalledWith('2026-09-01', '2026-09-30');

    fireEvent.click(screen.getByText('7N'));
    expect(onChange).toHaveBeenLastCalledWith('2026-10-07', '2026-10-13');
  });
});
