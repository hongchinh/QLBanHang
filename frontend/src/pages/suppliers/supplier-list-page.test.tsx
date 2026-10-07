import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { SupplierListPage } from './supplier-list-page';

vi.mock('@/features/suppliers/hooks', () => ({
  useSuppliers: () => ({
    data: {
      items: [
        {
          id: 's1',
          code: 'NCC0001',
          name: 'Nhà máy Thép Việt',
          group: 'Company',
          status: 'Active',
          isCustomer: false,
          isSupplier: true,
        },
      ],
      page: 1,
      pageSize: 20,
      totalItems: 1,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    },
    isLoading: false,
    isError: false,
  }),
  useDeleteSupplier: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

let granted = new Set<string>();
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean; isInRole: () => boolean }) => unknown) =>
    selector({ hasPermission: (p) => granted.has(p), isInRole: () => false }),
}));

function renderPage() {
  return render(
    <MemoryRouter>
      <SupplierListPage />
    </MemoryRouter>,
  );
}

describe('SupplierListPage', () => {
  it('renders suppliers and hides create without suppliers.create', () => {
    granted = new Set(['suppliers.view']);
    const { unmount } = renderPage();

    expect(screen.getByRole('heading', { name: 'Nhà cung cấp' })).toBeInTheDocument();
    expect(screen.getByText('Nhà máy Thép Việt')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Thêm nhà cung cấp/ })).not.toBeInTheDocument();
    unmount();

    granted = new Set(['suppliers.view', 'suppliers.create']);
    renderPage();
    expect(screen.getByRole('link', { name: /Thêm nhà cung cấp/ })).toHaveAttribute('href', '/suppliers/new');
  });
});
