import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { CellError, MoneyCell } from './money-cell';

describe('MoneyCell', () => {
  it('renders the formatted value under its label', () => {
    render(<MoneyCell id="m" label="Số tiền" value={1234567} onCommit={() => {}} />);
    expect(screen.getByLabelText('Số tiền')).toHaveValue('1.234.567');
  });

  it('shows the raw number while focused and the formatted one after blur', async () => {
    render(<MoneyCell id="m" label="Số tiền" value={1234567} onCommit={() => {}} />);
    const input = screen.getByLabelText('Số tiền');
    await userEvent.click(input);
    expect(input).toHaveValue('1234567');
    await userEvent.tab();
    expect(input).toHaveValue('1.234.567');
  });

  it('commits the parsed number on change', () => {
    const onCommit = vi.fn();
    render(<MoneyCell id="m" label="Số tiền" value={undefined} onCommit={onCommit} />);
    fireEvent.change(screen.getByLabelText('Số tiền'), { target: { value: '1.234.567' } });
    expect(onCommit).toHaveBeenLastCalledWith(1234567);
  });
});

describe('CellError', () => {
  it('renders the message with the given id', () => {
    render(<CellError id="e1" message="Bắt buộc" />);
    expect(screen.getByText('Bắt buộc')).toHaveAttribute('id', 'e1');
  });

  it('renders nothing without a message', () => {
    const { container } = render(<CellError id="e1" />);
    expect(container).toBeEmptyDOMElement();
  });
});
