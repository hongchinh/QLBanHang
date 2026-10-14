import { afterEach, describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { BranchDocumentLink } from './branch-document-link';

function signIn(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function renderLink(props: Partial<React.ComponentProps<typeof BranchDocumentLink>> = {}) {
  return render(
    <MemoryRouter>
      <BranchDocumentLink
        sourceType="Receipt"
        sourceId="r1"
        code="PT00124"
        branchCode="CN01"
        workingBranchCode="CN01"
        {...props}
      />
    </MemoryRouter>,
  );
}

describe('BranchDocumentLink', () => {
  afterEach(() => useAuthStore.setState({ user: null }));

  it('links a document of the working branch to its screen', () => {
    signIn(['receipts.view']);
    renderLink();

    expect(screen.getByRole('link', { name: 'PT00124' })).toHaveAttribute('href', '/receipts/r1');
  });

  it('links each kind of document to its own screen', () => {
    signIn(['stock_out.view', 'stock_in.view', 'payments.view']);
    renderLink({ sourceType: 'StockOut', sourceId: 's1', code: 'XK00311' });
    renderLink({ sourceType: 'StockIn', sourceId: 's2', code: 'NK00092' });
    renderLink({ sourceType: 'Payment', sourceId: 'p1', code: 'PC00087' });

    expect(screen.getByRole('link', { name: 'XK00311' })).toHaveAttribute('href', '/stock-out/s1');
    expect(screen.getByRole('link', { name: 'NK00092' })).toHaveAttribute('href', '/stock-in/s2');
    expect(screen.getByRole('link', { name: 'PC00087' })).toHaveAttribute('href', '/payments/p1');
  });

  it('is plain text for a user who may not open the screen', () => {
    signIn(['payments.view']);
    renderLink();

    expect(screen.getByText('PT00124')).toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  // The document screens reject a document of another branch (403).
  it('is plain text, with a hint, for a document of another branch', () => {
    signIn(['receipts.view']);
    renderLink({ branchCode: 'CN02' });

    expect(screen.queryByRole('link')).not.toBeInTheDocument();
    expect(screen.getByText('PT00124')).toHaveAttribute('title', 'Chứng từ của chi nhánh CN02');
  });

  it('is plain text while the working branch is not known', () => {
    signIn(['receipts.view']);
    renderLink({ workingBranchCode: undefined });

    expect(screen.queryByRole('link')).not.toBeInTheDocument();
    expect(screen.getByText('PT00124')).toHaveAttribute('title', 'Chứng từ của chi nhánh CN01');
  });
});
