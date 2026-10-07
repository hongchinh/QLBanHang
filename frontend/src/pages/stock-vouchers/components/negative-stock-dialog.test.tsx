import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { NegativeStockDialog } from './negative-stock-dialog';
import { toShortages } from './negative-stock';

const details = {
  'TON01@KHO01': ['Tồn 5, cần 8 — thiếu 3'],
  'GO01@KHO02': ['Tồn 0, cần 2 — thiếu 2'],
};

describe('NegativeStockDialog', () => {
  it('warning shows shortages (via toShortages) and a confirm button', () => {
    const onConfirm = vi.fn();
    const onClose = vi.fn();
    render(
      <NegativeStockDialog
        open
        blocked={false}
        shortages={toShortages(details)}
        onConfirm={onConfirm}
        onClose={onClose}
      />,
    );

    expect(screen.getByText('Cảnh báo âm kho')).toBeInTheDocument();
    expect(screen.getByText('TON01 @ KHO01')).toBeInTheDocument();
    expect(screen.getByText('Tồn 5, cần 8 — thiếu 3')).toBeInTheDocument();
    expect(screen.getByText('GO01 @ KHO02')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Vẫn lưu' }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));
    expect(onClose).toHaveBeenCalled();
  });

  it('custom confirm label', () => {
    render(
      <NegativeStockDialog
        open
        blocked={false}
        shortages={toShortages(details)}
        confirmLabel="Vẫn hủy phiếu"
        onConfirm={vi.fn()}
        onClose={vi.fn()}
      />,
    );

    expect(screen.getByRole('button', { name: 'Vẫn hủy phiếu' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Vẫn lưu' })).not.toBeInTheDocument();
  });

  it('blocked shows only close', () => {
    const onClose = vi.fn();
    render(
      <NegativeStockDialog
        open
        blocked
        shortages={toShortages(details)}
        onConfirm={vi.fn()}
        onClose={onClose}
      />,
    );

    expect(screen.getByText('Không đủ tồn kho')).toBeInTheDocument();
    expect(screen.getByText('TON01 @ KHO01')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Vẫn lưu' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Quay lại' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Đóng' }));
    expect(onClose).toHaveBeenCalled();
  });

  it('toShortages handles missing details', () => {
    expect(toShortages(undefined)).toEqual([]);
  });
});
