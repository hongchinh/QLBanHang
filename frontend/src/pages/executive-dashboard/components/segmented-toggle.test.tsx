import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { SegmentedToggle } from './segmented-toggle';

type Sort = 'Revenue' | 'GrossProfit';
const OPTIONS: { key: Sort; label: string; disabled?: boolean }[] = [
  { key: 'Revenue', label: 'Doanh thu' },
  { key: 'GrossProfit', label: 'Lãi gộp' },
];

describe('SegmentedToggle', () => {
  it('is a labelled group of buttons that marks the chosen one', () => {
    render(<SegmentedToggle label="Xếp hạng theo" options={OPTIONS} value="GrossProfit" onChange={vi.fn()} />);

    const group = screen.getByRole('group', { name: 'Xếp hạng theo' });
    expect(within(group).getAllByRole('button').map((b) => b.textContent)).toEqual(['Doanh thu', 'Lãi gộp']);
    expect(screen.getByRole('button', { name: 'Lãi gộp' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Doanh thu' })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: 'Lãi gộp' }).className).toContain('bg-foreground');
    expect(screen.getByRole('button', { name: 'Doanh thu' }).className).not.toContain('bg-foreground');
  });

  it('calls onChange with the key of the button', () => {
    const onChange = vi.fn();
    render(<SegmentedToggle label="Xếp hạng theo" options={OPTIONS} value="Revenue" onChange={onChange} />);

    fireEvent.click(screen.getByRole('button', { name: 'Lãi gộp' }));
    expect(onChange).toHaveBeenLastCalledWith('GrossProfit');
    fireEvent.click(screen.getByRole('button', { name: 'Doanh thu' }));
    expect(onChange).toHaveBeenLastCalledWith('Revenue');
  });

  it('a disabled option cannot be chosen', () => {
    const onChange = vi.fn();
    const options = [OPTIONS[0], { ...OPTIONS[1], disabled: true }];
    render(<SegmentedToggle label="Xếp hạng theo" options={options} value="Revenue" onChange={onChange} />);

    expect(screen.getByRole('button', { name: 'Lãi gộp' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Doanh thu' })).toBeEnabled();
    fireEvent.click(screen.getByRole('button', { name: 'Lãi gộp' }));
    expect(onChange).not.toHaveBeenCalled();
  });
});
