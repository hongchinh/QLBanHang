import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ReasonNoticeLine } from './reason-notice-line';

describe('ReasonNoticeLine', () => {
  it('renders nothing without an error or a notice', () => {
    const { container } = render(<ReasonNoticeLine notice={null} />);

    expect(container).toBeEmptyDOMElement();
  });

  it('renders an info notice as one amber status paragraph', () => {
    render(<ReasonNoticeLine notice={{ text: 'Đã đổi lý do', tone: 'info' }} />);

    const line = screen.getByRole('status');
    expect(line).toHaveTextContent('Đã đổi lý do');
    expect(line).toHaveClass('field-message', 'field-message-code', 'text-amber-700');
    expect(line).not.toHaveClass('text-destructive');
  });

  it('renders an error notice as a destructive status paragraph', () => {
    render(<ReasonNoticeLine notice={{ text: 'Lý do cần khách hàng', tone: 'error' }} />);

    const line = screen.getByRole('status');
    expect(line).toHaveClass('field-message', 'field-message-code', 'text-destructive');
    expect(line).not.toHaveClass('text-amber-700');
  });

  it('shows only the field error when there is also a notice', () => {
    render(<ReasonNoticeLine error="Chọn lý do" notice={{ text: 'Đã đổi lý do', tone: 'info' }} />);

    expect(screen.getAllByRole('status')).toHaveLength(1);
    const line = screen.getByText('Chọn lý do');
    expect(line).toHaveClass('text-destructive');
    expect(screen.queryByText('Đã đổi lý do')).toBeNull();
  });
});
