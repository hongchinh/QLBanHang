import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ExportTargetsDialog } from './export-targets-dialog';

const ITEMS = [
  { id: 'p1', label: 'KH1 - Minh Long' },
  { id: 'p2', label: 'KH2 - Hòa Phát' },
];

function setup(current?: { id: string; label: string } | null) {
  const onConfirm = vi.fn();
  const onOpenChange = vi.fn();
  render(
    <ExportTargetsDialog
      open
      onOpenChange={onOpenChange}
      title="Chọn đối tượng xuất sổ S12"
      current={current}
      onConfirm={onConfirm}
      renderPicker={(add) => (
        <div>
          {ITEMS.map((item) => (
            <button key={item.id} type="button" onClick={() => add(item)}>
              {`Thêm ${item.label}`}
            </button>
          ))}
        </div>
      )}
    />,
  );
  return { onConfirm, onOpenChange, user: userEvent.setup() };
}

describe('ExportTargetsDialog', () => {
  it('defaults to the item being viewed and confirms its id', async () => {
    const { onConfirm, user } = setup({ id: 'p1', label: 'KH1 - Minh Long' });

    expect(screen.getByRole('dialog', { name: 'Chọn đối tượng xuất sổ S12' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Đang xem: KH1 - Minh Long' })).toBeChecked();
    await user.click(screen.getByRole('button', { name: 'Xuất' }));

    expect(onConfirm).toHaveBeenCalledWith({ ids: ['p1'] });
  });

  it('collects several items as removable chips without duplicates', async () => {
    const { onConfirm, user } = setup({ id: 'p1', label: 'KH1 - Minh Long' });

    await user.click(screen.getByRole('radio', { name: 'Chọn nhiều' }));
    expect(screen.getByRole('button', { name: 'Xuất' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Thêm KH1 - Minh Long' }));
    await user.click(screen.getByRole('button', { name: 'Thêm KH2 - Hòa Phát' }));
    await user.click(screen.getByRole('button', { name: 'Thêm KH1 - Minh Long' }));
    expect(screen.getAllByText('KH1 - Minh Long', { selector: '[data-chip] span' })).toHaveLength(1);

    await user.click(screen.getByRole('button', { name: 'Bỏ KH2 - Hòa Phát' }));
    expect(screen.queryByRole('button', { name: 'Bỏ KH2 - Hòa Phát' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Thêm KH2 - Hòa Phát' }));

    await user.click(screen.getByRole('button', { name: 'Xuất' }));
    expect(onConfirm).toHaveBeenCalledWith({ ids: ['p1', 'p2'] });
  });

  it('exports all with activity', async () => {
    const { onConfirm, user } = setup({ id: 'p1', label: 'KH1 - Minh Long' });

    await user.click(screen.getByRole('radio', { name: 'Tất cả có phát sinh trong kỳ' }));
    await user.click(screen.getByRole('button', { name: 'Xuất' }));

    expect(onConfirm).toHaveBeenCalledWith({ all: true });
  });

  it('starts at Chọn nhiều without a current item', () => {
    setup(null);

    expect(screen.queryByRole('radio', { name: /^Đang xem/ })).not.toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Chọn nhiều' })).toBeChecked();
    expect(screen.getByRole('button', { name: 'Xuất' })).toBeDisabled();
  });
});
