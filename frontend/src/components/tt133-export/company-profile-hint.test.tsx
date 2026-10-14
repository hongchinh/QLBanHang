import { describe, expect, it } from 'vitest';
import { vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';

const profile: { data: { name: string | null } | undefined } = { data: undefined };
vi.mock('@/features/company-profile/hooks', () => ({ useCompanyProfile: () => profile }));

import { CompanyProfileHint } from './company-profile-hint';

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function renderHint() {
  return render(
    <MemoryRouter>
      <CompanyProfileHint />
    </MemoryRouter>,
  );
}

describe('CompanyProfileHint', () => {
  it('renders nothing while the profile is loading', () => {
    profile.data = undefined;
    setPermissions(['settings.company']);
    const { container } = renderHint();
    expect(container).toBeEmptyDOMElement();
  });

  it('renders nothing when the company has a name', () => {
    profile.data = { name: 'Công ty A' };
    setPermissions(['settings.company']);
    const { container } = renderHint();
    expect(container).toBeEmptyDOMElement();
  });

  it('shows the text and a settings link for a blank name with permission', () => {
    profile.data = { name: '' };
    setPermissions(['settings.company']);
    renderHint();
    expect(screen.getByText(/Chưa có thông tin đơn vị/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Cài đặt' })).toHaveAttribute('href', '/settings/company');
  });

  it('shows the text without a link when the permission is missing', () => {
    profile.data = { name: '  ' };
    setPermissions([]);
    renderHint();
    expect(screen.getByText(/Chưa có thông tin đơn vị/)).toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });
});
