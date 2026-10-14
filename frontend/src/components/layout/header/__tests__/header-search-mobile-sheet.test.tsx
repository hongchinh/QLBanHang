import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { HeaderSearchMobileSheet } from '../header-search-mobile-sheet'

vi.mock('@/features/search/hooks', () => ({
  SEARCH_MIN_LENGTH: 2,
  useGlobalSearch: () => ({ data: undefined, isFetching: false }),
}))

describe('HeaderSearchMobileSheet', () => {
  it('keeps the trigger icon free of the muted tone', () => {
    render(<MemoryRouter><HeaderSearchMobileSheet /></MemoryRouter>)
    const trigger = screen.getByRole('button', { name: 'Mở tìm kiếm' })
    expect(trigger.querySelector('[data-icon="search"]')).not.toHaveClass('text-icon-muted')
  })

  // The sheet is fixed to the top of the viewport, outside the shell's padding.
  it('keeps the open sheet inside the safe area', () => {
    render(<MemoryRouter><HeaderSearchMobileSheet /></MemoryRouter>)
    fireEvent.click(screen.getByRole('button', { name: 'Mở tìm kiếm' }))
    expect(screen.getByRole('dialog')).toHaveClass(
      'pb-3',
      'pt-[max(0.75rem,env(safe-area-inset-top))]',
      'pl-[max(0.75rem,env(safe-area-inset-left))]',
      'pr-[max(0.75rem,env(safe-area-inset-right))]',
    )
  })
})
