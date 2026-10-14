import { render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Toaster } from './toaster'

describe('Toaster', () => {
  // On phones the toasts stack from the bottom edge; keep them above the iPhone home indicator.
  it('pads the phone viewport by the bottom safe-area inset', () => {
    render(<Toaster />)
    const viewport = document.querySelector('ol')!
    expect(viewport).toHaveClass('pb-[max(1rem,env(safe-area-inset-bottom))]', 'sm:pb-4')
  })
})
