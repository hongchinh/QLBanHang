import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

const css = readFileSync(resolve(process.cwd(), 'src/index.css'), 'utf8')

// env() is 0 on desktop and Android, so these are no-ops there; on a notched iPhone they keep content
// clear of the notch (landscape) and the home indicator.
describe('safe-area utilities', () => {
  it.each([
    ['pt-safe', 'padding-top', 'top'],
    ['pb-safe', 'padding-bottom', 'bottom'],
    ['pl-safe', 'padding-left', 'left'],
    ['pr-safe', 'padding-right', 'right'],
  ])('.%s sets %s from env(safe-area-inset-%s)', (cls, property, side) => {
    const rule = new RegExp(`\\.${cls}\\s*\\{\\s*${property}:\\s*env\\(safe-area-inset-${side}\\);?\\s*\\}`)
    expect(css).toMatch(rule)
  })

  it('declares them in the utilities layer so they win over component classes', () => {
    expect(css).toMatch(/@layer utilities\s*\{[^}]*\.pt-safe/)
  })
})
