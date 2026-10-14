import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PeriodPresetSelect } from './period-preset-select';

describe('PeriodPresetSelect', () => {
  it('reports the range of the chosen preset', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<PeriodPresetSelect id="preset" onChange={onChange} today="2026-10-13" />);

    await user.click(screen.getByRole('combobox', { name: 'Kỳ' }));
    await user.click(screen.getByRole('option', { name: 'Quý trước' }));

    expect(onChange).toHaveBeenCalledWith({ from: '2026-07-01', to: '2026-09-30' });
  });
});
