import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ProductFormPage } from './product-form-page';
import type { Product } from '@/features/products/types';
import type * as RouterModule from 'react-router-dom';

const GROUP_ID = '11111111-1111-1111-1111-111111111111';
const UNIT_ID = '22222222-2222-2222-2222-222222222222';

const createMock = vi.fn();
let routeId = 'new';
let product: Product | undefined;

vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof RouterModule>();
  return { ...actual, useParams: () => ({ id: routeId }), useNavigate: () => vi.fn() };
});

vi.mock('@/features/products/hooks', () => ({
  useProduct: () => ({ data: product, isLoading: false }),
  useProductGroups: () => ({ data: [{ id: GROUP_ID, code: 'TON', name: 'Tôn' }], isLoading: false }),
  useUnits: () => ({ data: [{ id: UNIT_ID, code: 'TAM', name: 'Tấm' }], isLoading: false }),
  useCreateProduct: () => ({ mutateAsync: createMock, isPending: false, isError: false, error: null }),
  useUpdateProduct: () => ({ mutateAsync: vi.fn(), isPending: false, isError: false, error: null }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

function renderPage() {
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter>
        <ProductFormPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const LOCKED_PRODUCT: Product = {
  id: 'p1',
  code: 'SP01',
  name: 'Tôn lạnh',
  productGroupId: GROUP_ID,
  unitId: UNIT_ID,
  unitName: 'Tấm',
  status: 'Active',
  pricingMode: 'PerSquareMeter',
  trackInventory: true,
  purchaseDiscountRate: 0,
  salesDiscountRate: 0,
  priceIncludesVat: false,
  hasInventoryActivity: true,
  createdAt: '2026-10-01T00:00:00Z',
};

describe('ProductFormPage inventory fields', () => {
  beforeEach(() => {
    createMock.mockReset().mockResolvedValue({});
    routeId = 'new';
    product = undefined;
  });

  it('new product payload includes inventory fields with defaults', async () => {
    const user = userEvent.setup();
    renderPage();

    expect(screen.getByLabelText('Theo dõi tồn kho')).toBeChecked();
    expect(screen.getByLabelText('Giá bán đã gồm VAT')).not.toBeChecked();

    await user.type(screen.getByLabelText('Tên hàng hóa *'), 'Tôn lạnh');
    await user.click(screen.getByRole('combobox', { name: 'Nhóm hàng hóa *' }));
    await user.click(screen.getByRole('option', { name: 'Tôn' }));
    await user.type(screen.getByLabelText('Đơn vị tính *'), 'Tấm');
    await user.type(screen.getByLabelText('% CK mua'), '5');
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() =>
      expect(createMock).toHaveBeenCalledWith(
        expect.objectContaining({
          name: 'Tôn lạnh',
          productGroupId: GROUP_ID,
          unitId: UNIT_ID,
          trackInventory: true,
          priceIncludesVat: false,
          purchaseDiscountRate: 5,
          salesDiscountRate: 0,
        }),
      ),
    );
  });

  it('locked fields are disabled when the product has inventory activity', () => {
    routeId = 'p1';
    product = LOCKED_PRODUCT;
    renderPage();

    expect(screen.getByRole('combobox', { name: 'Loại giá' })).toBeDisabled();
    expect(screen.getByLabelText('Đơn vị tính *')).toBeDisabled();
    expect(screen.getByLabelText('Theo dõi tồn kho')).toBeDisabled();
    expect(screen.getByLabelText('Giá bán đã gồm VAT')).toBeDisabled();
    expect(screen.getByLabelText('% CK mua')).toBeEnabled();
    expect(screen.getAllByText('Hàng đã phát sinh kho — không đổi được').length).toBeGreaterThan(0);
  });
});
