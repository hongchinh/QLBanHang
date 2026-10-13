import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { EXECUTIVE_PRESETS } from '@/features/executive-dashboard/presets';
import { DEFAULT_PRESETS, RangePicker } from './range-picker';

const buttonLabels = () => screen.getAllByRole('button').map((b) => b.textContent);
const activeLabels = () =>
  screen
    .getAllByRole('button')
    .filter((b) => b.className.includes('bg-foreground'))
    .map((b) => b.textContent);

describe('RangePicker', () => {
  beforeEach(() => {
    // Tuesday 13 October 2026.
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 13, 10, 0, 0));
  });
  afterEach(() => vi.useRealTimers());

  it('keeps its five chips and the custom button when presets is not given', () => {
    render(<RangePicker from="2026-10-01" to="2026-10-13" onChange={vi.fn()} />);

    expect(buttonLabels()).toEqual(['Hôm nay', '7N', '30N', 'Tháng này', 'Tháng trước', 'Tuỳ chỉnh ▾']);
    expect(DEFAULT_PRESETS).toEqual([
      { key: 'today', label: 'Hôm nay' },
      { key: '7d', label: '7N' },
      { key: '30d', label: '30N' },
      { key: 'this-month', label: 'Tháng này' },
      { key: 'last-month', label: 'Tháng trước' },
    ]);
  });

  it('shows the given presets instead', () => {
    render(<RangePicker from="2026-10-01" to="2026-10-13" onChange={vi.fn()} presets={EXECUTIVE_PRESETS} />);

    expect(buttonLabels()).toEqual(['Hôm nay', 'Tuần này', 'Tháng này', 'Năm nay', 'Tuỳ chỉnh ▾']);
  });

  it('clicking a chip calls onPreset with its key', () => {
    const onChange = vi.fn();
    const onPreset = vi.fn();
    render(
      <RangePicker from="2026-10-01" to="2026-10-13" onChange={onChange} onPreset={onPreset} presets={EXECUTIVE_PRESETS} />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Năm nay' }));

    expect(onPreset).toHaveBeenCalledTimes(1);
    expect(onPreset).toHaveBeenCalledWith('this-year');
    expect(onChange).not.toHaveBeenCalled();
  });

  it('without onPreset a chip calls onChange with the range of its preset', () => {
    const onChange = vi.fn();
    render(<RangePicker from="2026-10-01" to="2026-10-13" onChange={onChange} presets={EXECUTIVE_PRESETS} />);

    fireEvent.click(screen.getByRole('button', { name: 'Tuần này' }));
    expect(onChange).toHaveBeenLastCalledWith('2026-10-12', '2026-10-13');

    fireEvent.click(screen.getByRole('button', { name: 'Năm nay' }));
    expect(onChange).toHaveBeenLastCalledWith('2026-01-01', '2026-10-13');
  });

  it('marks the chip whose range is selected, and the custom button for any other range', () => {
    const { rerender } = render(<RangePicker from="2026-10-01" to="2026-10-13" onChange={vi.fn()} />);
    expect(activeLabels()).toEqual(['Tháng này']);

    rerender(<RangePicker from="2026-10-02" to="2026-10-13" onChange={vi.fn()} />);
    expect(activeLabels()).toEqual(['Tuỳ chỉnh ▾']);

    // The year so far is a preset of the executive list only.
    rerender(<RangePicker from="2026-01-01" to="2026-10-13" onChange={vi.fn()} />);
    expect(activeLabels()).toEqual(['Tuỳ chỉnh ▾']);
    rerender(<RangePicker from="2026-01-01" to="2026-10-13" onChange={vi.fn()} presets={EXECUTIVE_PRESETS} />);
    expect(activeLabels()).toEqual(['Năm nay']);
  });
});
