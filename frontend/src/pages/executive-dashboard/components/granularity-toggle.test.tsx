import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { GranularityToggle } from './granularity-toggle';

describe('GranularityToggle', () => {
  it('has three buttons and marks the chosen one', () => {
    render(<GranularityToggle value="Week" onChange={vi.fn()} />);

    expect(screen.getAllByRole('button').map((b) => b.textContent)).toEqual(['Ngày', 'Tuần', 'Tháng']);
    expect(screen.getByRole('button', { name: 'Tuần' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Ngày' })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: 'Tuần' }).className).toContain('bg-foreground');
    expect(screen.getByRole('button', { name: 'Tháng' }).className).not.toContain('bg-foreground');
  });

  it('calls onChange with the granularity of the button', () => {
    const onChange = vi.fn();
    render(<GranularityToggle value="Day" onChange={onChange} />);

    fireEvent.click(screen.getByRole('button', { name: 'Tuần' }));
    expect(onChange).toHaveBeenLastCalledWith('Week');
    fireEvent.click(screen.getByRole('button', { name: 'Tháng' }));
    expect(onChange).toHaveBeenLastCalledWith('Month');
    fireEvent.click(screen.getByRole('button', { name: 'Ngày' }));
    expect(onChange).toHaveBeenLastCalledWith('Day');
    expect(onChange).toHaveBeenCalledTimes(3);
  });

  it('dayDisabled disables "Ngày" only', () => {
    const onChange = vi.fn();
    render(<GranularityToggle value="Month" onChange={onChange} dayDisabled />);

    expect(screen.getByRole('button', { name: 'Ngày' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Tuần' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Tháng' })).toBeEnabled();
    fireEvent.click(screen.getByRole('button', { name: 'Ngày' }));
    expect(onChange).not.toHaveBeenCalled();
  });

  it('"Ngày" is enabled by default', () => {
    render(<GranularityToggle value="Month" onChange={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Ngày' })).toBeEnabled();
  });
});
