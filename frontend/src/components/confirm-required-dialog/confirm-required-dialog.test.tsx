import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import type { ConfirmationRequest } from '@/lib/confirm-required';
import { ConfirmRequiredDialog } from './confirm-required-dialog';

const warning: ConfirmationRequest = {
  code: 'CREDIT_LIMIT_WARNING',
  flag: 'acknowledgeCreditLimit',
  blocked: false,
  title: 'Vượt hạn mức nợ',
  message: 'Khách hàng sẽ vượt hạn mức nợ.',
  items: [
    { key: 'KH01', messages: ['Dư nợ 9.000.000 vượt hạn mức 5.000.000'] },
    { key: 'KH02', messages: ['Quá hạn 12 ngày'] },
  ],
};

const blocked: ConfirmationRequest = {
  code: 'NEGATIVE_FUND_BLOCKED',
  blocked: true,
  title: 'Không đủ tiền trong quỹ',
  message: 'Không đủ tiền trong quỹ.',
  items: [{ key: 'QTM', messages: ['Âm 1.000'] }],
};

describe('ConfirmRequiredDialog', () => {
  it('renders items and confirms', () => {
    const onConfirm = vi.fn();
    const onClose = vi.fn();
    render(<ConfirmRequiredDialog request={warning} onConfirm={onConfirm} onClose={onClose} />);

    expect(screen.getByText('Vượt hạn mức nợ')).toBeInTheDocument();
    expect(screen.getByText('Khách hàng sẽ vượt hạn mức nợ.')).toBeInTheDocument();
    expect(screen.getByText('KH01')).toBeInTheDocument();
    expect(screen.getByText('Dư nợ 9.000.000 vượt hạn mức 5.000.000')).toBeInTheDocument();
    expect(screen.getByText('KH02')).toBeInTheDocument();
    expect(screen.getByText('Quá hạn 12 ngày')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Vẫn tiếp tục' }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }));
    expect(onClose).toHaveBeenCalled();
  });

  it('uses a custom confirm label', () => {
    render(
      <ConfirmRequiredDialog request={warning} confirmLabel="Vẫn lưu" onConfirm={vi.fn()} onClose={vi.fn()} />,
    );

    expect(screen.getByRole('button', { name: 'Vẫn lưu' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Vẫn tiếp tục' })).not.toBeInTheDocument();
  });

  it('blocked shows only close', () => {
    const onClose = vi.fn();
    render(<ConfirmRequiredDialog request={blocked} onConfirm={vi.fn()} onClose={onClose} />);

    expect(screen.getByText('Không đủ tiền trong quỹ')).toBeInTheDocument();
    expect(screen.getByText('Không đủ tiền trong quỹ.')).toBeInTheDocument();
    expect(screen.getByText('QTM')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Vẫn tiếp tục' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Hủy' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Đóng' }));
    expect(onClose).toHaveBeenCalled();
  });

  it('renders nothing without a request', () => {
    render(<ConfirmRequiredDialog request={null} onConfirm={vi.fn()} onClose={vi.fn()} />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
