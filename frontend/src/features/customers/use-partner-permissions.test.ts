import { afterEach, describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useAuthStore, type CurrentUser } from '@/stores/auth-store';
import { usePartnerPermissions } from './use-partner-permissions';

describe('usePartnerPermissions', () => {
  afterEach(() => useAuthStore.setState({ user: null }));

  it('reads the create and view permissions of both roles', () => {
    useAuthStore.setState({
      user: { permissions: ['customers.create', 'suppliers.view'] } as CurrentUser,
    });

    const { result } = renderHook(() => usePartnerPermissions());

    expect(result.current).toEqual({
      canCreateCustomer: true,
      canCreateSupplier: false,
      canViewCustomer: false,
      canViewSupplier: true,
    });
  });
});
