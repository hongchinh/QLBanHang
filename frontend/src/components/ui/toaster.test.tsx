import { act, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { dismissToast, toast } from '@/lib/use-toast'
import { Toaster } from './toaster'

const shown: string[] = []

afterEach(() => {
  act(() => shown.splice(0).forEach(dismissToast))
})

describe('Toaster', () => {
  // Keep the toasts above the iPhone home indicator.
  it('pads the viewport by the bottom safe-area inset', () => {
    render(<Toaster />)
    const viewport = document.querySelector('ol')!
    expect(viewport).toHaveClass('pb-[max(1rem,env(safe-area-inset-bottom))]')
    expect(viewport).not.toHaveClass('sm:pb-4')
  })

  // At the top right the toasts covered the sticky action bar of the forms (Cập nhật, In phiếu, Hủy phiếu).
  it('stacks the toasts from the bottom center on every screen size', () => {
    render(<Toaster />)
    const viewport = document.querySelector('ol')!
    expect(viewport).toHaveClass('bottom-0', 'left-1/2', '-translate-x-1/2', 'flex-col-reverse', 'max-w-[420px]')
    expect(viewport.className).not.toMatch(/\bsm:(top|right|bottom|flex-col)/)
  })

  // The viewport's padding and the gaps between toasts must not swallow clicks; only a toast does.
  it('lets clicks pass through the viewport but not through a toast', () => {
    render(<Toaster />)
    act(() => {
      shown.push(toast({ title: 'Đã lưu phiếu' }))
    })
    expect(document.querySelector('ol')).toHaveClass('pointer-events-none')
    expect(screen.getByText('Đã lưu phiếu').closest('li')).toHaveClass('pointer-events-auto', 'data-[state=open]:slide-in-from-bottom-full')
  })

  // At the bottom center a toast lies over tables and forms; a see-through background made its text hard to read.
  it('gives every toast variant an opaque background', () => {
    render(<Toaster />)
    act(() => {
      shown.push(toast({ title: 'Mặc định' }))
      shown.push(toast({ title: 'Thành công', variant: 'success' }))
      shown.push(toast({ title: 'Lỗi', variant: 'destructive' }))
    })
    for (const title of ['Mặc định', 'Thành công', 'Lỗi']) {
      const li = screen.getByText(title).closest('li')!
      expect(li.className).toMatch(/\bbg-(background|emerald-50|red-50)\b/)
      expect(li.className).not.toMatch(/\bbg-[\w-]+\/\d+/)
    }
  })
})
