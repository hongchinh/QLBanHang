import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ProductQuickAddDialog } from './product-quick-add-dialog';
import { toProductSuggestion } from '@/features/products/to-product-suggestion';
import { toast } from '@/lib/use-toast';
import type { Product } from '@/features/products/types';

const GROUP_ID = '11111111-1111-1111-1111-111111111111';
const UNIT_ID = '22222222-2222-2222-2222-222222222222';

const createMock = vi.fn();
const refetchGroups = vi.fn();
const refetchUnits = vi.fn();
let unitsError = false;
let unitsLoading = false;

vi.mock('@/features/products/hooks', () => ({
  useProductGroups: () => ({
    data: [{ id: GROUP_ID, code: 'TON', name: 'Tôn' }],
    isLoading: false,
    isError: false,
    refetch: refetchGroups,
  }),
  useUnits: () => ({
    data: unitsError || unitsLoading ? undefined : [{ id: UNIT_ID, code: 'TAM', name: 'Tấm' }],
    isLoading: unitsLoading,
    isError: unitsError,
    refetch: refetchUnits,
  }),
}));

vi.mock('@/features/products/use-save-product', () => ({
  useSaveProduct: () => ({
    create: createMock,
    isPending: false,
    isError: false,
    error: null,
    reset: vi.fn(),
  }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

const CREATED: Product = {
  id: 'p1',
  code: 'SP01',
  name: 'Tôn lạnh',
  productGroupId: GROUP_ID,
  unitId: UNIT_ID,
  unitName: 'Tấm',
  status: 'Active',
  pricingMode: 'PerUnit',
  trackInventory: true,
  purchaseDiscountRate: 0,
  salesDiscountRate: 0,
  priceIncludesVat: false,
  hasInventoryActivity: false,
  createdAt: '2026-10-01T00:00:00Z',
};

function renderDialog(seed: string) {
  const onOpenChange = vi.fn();
  const onCreated = vi.fn();
  render(
    <MemoryRouter>
      <ProductQuickAddDialog open onOpenChange={onOpenChange} seed={seed} onCreated={onCreated} />
    </MemoryRouter>,
  );
  return { onOpenChange, onCreated };
}

describe('ProductQuickAddDialog', () => {
  beforeEach(() => {
    createMock.mockReset().mockResolvedValue(CREATED);
    refetchGroups.mockReset();
    refetchUnits.mockReset();
    vi.mocked(toast).mockReset();
    unitsError = false;
    unitsLoading = false;
  });

  it('a code-like seed fills Mã and focuses Tên', async () => {
    renderDialog('TL-50');
    const code = await screen.findByLabelText('Mã hàng hóa');
    expect(code).toHaveValue('TL-50');
    expect(screen.getByLabelText('Tên hàng hóa *')).toHaveValue('');
    await waitFor(() => expect(screen.getByLabelText('Tên hàng hóa *')).toHaveFocus());
  });

  it('a name-like seed fills Tên and focuses Mã', async () => {
    renderDialog('Tôn lạnh 0.4');
    expect(await screen.findByLabelText('Tên hàng hóa *')).toHaveValue('Tôn lạnh 0.4');
    expect(screen.getByLabelText('Mã hàng hóa')).toHaveValue('');
    await waitFor(() => expect(screen.getByLabelText('Mã hàng hóa')).toHaveFocus());
  });

  it('an empty seed focuses Mã', async () => {
    renderDialog('');
    await waitFor(() => expect(screen.getByLabelText('Mã hàng hóa')).toHaveFocus());
  });

  it('while the lookups load, focus is in the dialog rather than the cell behind it', async () => {
    unitsLoading = true;
    renderDialog('');
    const dialog = await screen.findByRole('dialog');
    await waitFor(() => expect(dialog).toHaveFocus());
  });

  it('has its own title and no status field', async () => {
    renderDialog('');
    expect(await screen.findByText('Thêm nhanh hàng hóa')).toBeInTheDocument();
    expect(screen.queryByText('Trạng thái')).not.toBeInTheDocument();
  });

  it('Ctrl+Enter saves, toasts, hands back the suggestion and closes', async () => {
    const user = userEvent.setup();
    const { onOpenChange, onCreated } = renderDialog('Tôn lạnh');
    await user.click(await screen.findByRole('combobox', { name: 'Nhóm hàng hóa *' }));
    await user.click(screen.getByRole('option', { name: 'Tôn' }));
    await user.type(screen.getByLabelText('Đơn vị tính *'), 'Tấm');
    await user.click(screen.getByLabelText('Tên hàng hóa *'));
    await user.keyboard('{Control>}{Enter}{/Control}');

    await waitFor(() => expect(createMock).toHaveBeenCalled());
    await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false));
    expect(toast).toHaveBeenCalledWith(
      expect.objectContaining({ variant: 'success', title: 'Đã tạo hàng hóa', description: 'SP01' }),
    );
    expect(onCreated).toHaveBeenCalledWith(toProductSuggestion(CREATED));
  });

  it('a repeated Ctrl+Enter while the save is in flight starts one save, one toast, one onCreated', async () => {
    let resolveCreate!: (p: Product) => void;
    createMock.mockReset().mockReturnValue(new Promise<Product>((r) => (resolveCreate = r)));
    const user = userEvent.setup();
    const { onCreated } = renderDialog('Tôn lạnh');
    await user.click(await screen.findByRole('combobox', { name: 'Nhóm hàng hóa *' }));
    await user.click(screen.getByRole('option', { name: 'Tôn' }));
    await user.type(screen.getByLabelText('Đơn vị tính *'), 'Tấm');
    await user.click(screen.getByLabelText('Tên hàng hóa *'));
    await user.keyboard('{Control>}{Enter}{Enter}{/Control}');

    await waitFor(() => expect(createMock).toHaveBeenCalled());
    resolveCreate(CREATED);
    await waitFor(() => expect(onCreated).toHaveBeenCalled());
    expect(createMock).toHaveBeenCalledTimes(1);
    expect(onCreated).toHaveBeenCalledTimes(1);
    expect(toast).toHaveBeenCalledTimes(1);
  });

  it('keeps the dialog open and shows the error when saving fails', async () => {
    createMock.mockRejectedValue(new Error('Mã hàng hóa đã tồn tại'));
    const user = userEvent.setup();
    const { onOpenChange } = renderDialog('Tôn lạnh');
    await user.click(await screen.findByRole('combobox', { name: 'Nhóm hàng hóa *' }));
    await user.click(screen.getByRole('option', { name: 'Tôn' }));
    await user.type(screen.getByLabelText('Đơn vị tính *'), 'Tấm');
    await user.click(screen.getByLabelText('Tên hàng hóa *'));
    await user.keyboard('{Control>}{Enter}{/Control}');

    expect(await screen.findByText('Mã hàng hóa đã tồn tại')).toBeInTheDocument();
    expect(toast).toHaveBeenCalledWith(expect.objectContaining({ variant: 'destructive', title: 'Không thể lưu' }));
    expect(screen.getByLabelText('Tên hàng hóa *')).toHaveValue('Tôn lạnh');
    expect(onOpenChange).not.toHaveBeenCalledWith(false);
  });

  it('offers a retry when a lookup fails to load', async () => {
    unitsError = true;
    const user = userEvent.setup();
    renderDialog('');
    expect(await screen.findByText('Không tải được danh mục')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Thử lại' }));
    expect(refetchGroups).toHaveBeenCalled();
    expect(refetchUnits).toHaveBeenCalled();
  });

  it('Esc closes without saving', async () => {
    const user = userEvent.setup();
    const { onOpenChange } = renderDialog('');
    await screen.findByLabelText('Mã hàng hóa');
    await user.keyboard('{Escape}');
    expect(onOpenChange).toHaveBeenCalledWith(false);
    expect(createMock).not.toHaveBeenCalled();
  });
});
