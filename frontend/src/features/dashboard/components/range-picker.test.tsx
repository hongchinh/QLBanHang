import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { EXECUTIVE_PRESETS } from '@/features/executive-dashboard/presets';
import { RangePicker } from './range-picker';

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

  describe('Tuỳ chỉnh', () => {
    const openCustom = () => fireEvent.click(screen.getByRole('button', { name: 'Tuỳ chỉnh ▾' }));
    const setDates = (from: string, to: string) => {
      fireEvent.change(screen.getByLabelText('Từ ngày'), { target: { value: from } });
      fireEvent.change(screen.getByLabelText('Đến ngày'), { target: { value: to } });
    };
    const apply = () => screen.getByRole('button', { name: 'Áp dụng' });

    it('applies a valid range', () => {
      const onChange = vi.fn();
      render(<RangePicker from="2026-10-01" to="2026-10-13" onChange={onChange} />);
      openCustom();
      setDates('2026-09-01', '2026-09-30');

      expect(apply()).toBeEnabled();
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      fireEvent.click(apply());

      expect(onChange).toHaveBeenCalledWith('2026-09-01', '2026-09-30');
    });

    it('does not apply "to" before "from" and says why', () => {
      const onChange = vi.fn();
      render(<RangePicker from="2026-10-01" to="2026-10-13" onChange={onChange} />);
      openCustom();
      setDates('2026-10-20', '2026-10-01');

      expect(screen.getByRole('alert')).toHaveTextContent('Đến ngày phải sau hoặc bằng từ ngày.');
      expect(apply()).toBeDisabled();
      fireEvent.click(apply());
      expect(onChange).not.toHaveBeenCalled();

      setDates('2026-10-01', '2026-10-20');
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      expect(apply()).toBeEnabled();
    });

    it('does not apply a range the screen refuses (validate) and shows its reason', () => {
      const onChange = vi.fn();
      const validate = (from: string) => (from < '2026-01-01' ? 'Khoảng thời gian tối đa là 3 năm.' : null);
      render(<RangePicker from="2026-10-01" to="2026-10-13" onChange={onChange} validate={validate} />);
      openCustom();
      setDates('2022-01-01', '2026-10-13');

      expect(screen.getByRole('alert')).toHaveTextContent('Khoảng thời gian tối đa là 3 năm.');
      expect(apply()).toBeDisabled();
      fireEvent.click(apply());
      expect(onChange).not.toHaveBeenCalled();
    });

    it('needs both dates', () => {
      render(<RangePicker from="2026-10-01" to="2026-10-13" onChange={vi.fn()} />);
      openCustom();
      setDates('2026-10-01', '');

      expect(apply()).toBeDisabled();
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });
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
