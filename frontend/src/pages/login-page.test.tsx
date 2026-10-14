import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { LoginPage } from './login-page';

describe('LoginPage', () => {
  // The viewport height belongs to AppShell (it also holds the push banner). A page that claimed it again with
  // min-h-screen would be taller than the room under the banner and bring the scrollbar back.
  it('fills the shell instead of claiming the viewport height itself', () => {
    const { container } = render(
      <QueryClientProvider client={new QueryClient()}>
        <MemoryRouter initialEntries={['/login']}>
          <LoginPage />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    const root = container.firstElementChild;
    expect(root).toContainElement(screen.getByRole('button', { name: 'Đăng nhập' }));
    expect(root).toHaveClass('flex-1');
    expect(root).not.toHaveClass('min-h-screen');
    expect(root).not.toHaveClass('h-screen');
  });
});
