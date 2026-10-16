# Phase 01 — Shared rule and flash hook

**Status:** [ ] pending
**Complexity:** M

## Objective

Build the shared parts that Phases 02 and 03 use, with both grids looking and behaving exactly as today for the four valid pricing modes:

- `lib/dimension-columns.ts`: the pure rule (dimension fields, mode rank, `usesDimension`, `highestPricingMode`, `shownDimensions`). `PRICING_MODES` moves here from `line-navigation.ts`.
- `isFieldDisabled` in `line-navigation.ts` reads the rule, so a missing or unknown mode counts as `PerUnit` (SUMMARY → Rule → Rank).
- `lib/use-appeared-keys.ts`: the hook that marks newly shown columns for the flash.
- The flash CSS: the token `--qldh-col-appeared-head-bg` and the `col-appeared` rules in `line-items-grid.css`.

Nothing renders the class `col-appeared` yet, and no grid calls `shownDimensions` yet. The only runtime change is on a stock row whose mode is missing or unknown: its **SL** cell becomes enabled, and with an unknown string its **Số tấm** / **Dài** cells become disabled (a missing mode already reads as `PerUnit` there, through `?? 'PerUnit'` at `stock-line-grid.tsx:507`), as the spec wants.

Not in this phase: any grid change (Phases 02 and 03), the quotation's `isLineFocusFieldDisabled` (Phase 03), the `.dxr-cell` removal (Phase 03), docs and help files (Phase 04).

## Files

- `frontend/src/lib/dimension-columns.ts` (new)
- `frontend/src/lib/dimension-columns.test.ts` (new)
- `frontend/src/lib/use-appeared-keys.ts` (new, next to `use-dialog-opening.ts`)
- `frontend/src/lib/use-appeared-keys.test.ts` (new)
- `frontend/src/pages/stock-vouchers/utils/line-navigation.ts` (drops `PRICING_MODES`; `isFieldDisabled` reads the rule)
- `frontend/src/pages/stock-vouchers/utils/line-navigation.test.ts` (new cases)
- `frontend/src/pages/stock-vouchers/utils/use-stock-grid-prefs.ts` (import only)
- `frontend/src/pages/stock-vouchers/components/stock-grid-settings-popover.tsx` (import only)
- `frontend/src/styles/control-tokens.css` (one token)
- `frontend/src/pages/quotations/components/line-items-grid.css` (flash rules appended)

Commands run from `frontend/` in the feature worktree. `npm run typecheck` does not check test files (`tsconfig.app.json` excludes `src/**/*.test.ts(x)`), and vitest runs with `css: false`. Every Bash call starts with the `WT=` assignment of Phase 00. Every command that runs npm, npx, vitest, a grep of `src` or `git checkout -- tsconfig.app.tsbuildinfo` starts with `cd "$WT/frontend" &&` (a new Bash call starts in the main checkout, so a bare command would run against the wrong tree). Commit lines start at `$WT`, so their paths begin with `frontend/`. Add a second `-m` with the `Co-Authored-By` trailer from the session's attribution instructions to every commit line below, as Phase 00 does. Under load, add `--testTimeout=30000 --maxWorkers=3` to the vitest commands (SUMMARY → Risks).

## Tasks

Before Task 1.1: set **Status:** to `[-] in progress` in this file and commit it (SUMMARY → Execution notes): `cd "$WT" && git add docs/plans/261016-0249-dimension-columns-by-pricing-mode/phase-01-shared-rule.md && git commit -m "docs(plan): phase 01 in progress"`. Then record the `<phase base>`: `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git rev-parse HEAD`. Write the hash down; the Verification uses it.

### Task 1.1 — `lib/dimension-columns.ts`

1. **Write the failing test** `src/lib/dimension-columns.test.ts`:
   ```ts
   import { describe, expect, it } from 'vitest';
   import {
     DIMENSION_FIELDS,
     PRICING_MODES,
     PRICING_MODE_RANK,
     highestPricingMode,
     isDimensionField,
     normalizePricingMode,
     shownDimensions,
     usesDimension,
     type DimensionField,
   } from './dimension-columns';

   describe('DIMENSION_FIELDS / PRICING_MODES / PRICING_MODE_RANK', () => {
     it('lists the dimensions in column order and the modes in rank order', () => {
       expect(DIMENSION_FIELDS).toEqual(['sheet-count', 'length', 'width', 'thickness']);
       expect(PRICING_MODES).toEqual(['PerUnit', 'PerLinearMeter', 'PerSquareMeter', 'PerCubicMeter']);
       expect(PRICING_MODE_RANK).toEqual({ PerUnit: 0, PerLinearMeter: 1, PerSquareMeter: 2, PerCubicMeter: 3 });
     });
   });

   describe('isDimensionField', () => {
     it('is true only for Số tấm, Dài, Rộng, Dày', () => {
       for (const field of DIMENSION_FIELDS) expect(isDimensionField(field)).toBe(true);
       for (const field of ['product-code', 'name', 'unit', 'warehouse', 'quantity', 'unit-price', 'unit-cost', 'note', '']) {
         expect(isDimensionField(field)).toBe(false);
       }
     });
   });

   describe('normalizePricingMode', () => {
     it('keeps each of the four modes', () => {
       for (const mode of PRICING_MODES) expect(normalizePricingMode(mode)).toBe(mode);
     });

     it.each([undefined, null, '', 'Legacy', 'perunit', 'toString', 2])('treats %j as PerUnit', (mode) => {
       expect(normalizePricingMode(mode)).toBe('PerUnit');
     });
   });

   describe('usesDimension', () => {
     it.each([
       ['PerUnit', 'sheet-count', false],
       ['PerUnit', 'length', false],
       ['PerUnit', 'width', false],
       ['PerUnit', 'thickness', false],
       ['PerLinearMeter', 'sheet-count', true],
       ['PerLinearMeter', 'length', true],
       ['PerLinearMeter', 'width', false],
       ['PerLinearMeter', 'thickness', false],
       ['PerSquareMeter', 'sheet-count', true],
       ['PerSquareMeter', 'length', true],
       ['PerSquareMeter', 'width', true],
       ['PerSquareMeter', 'thickness', false],
       ['PerCubicMeter', 'sheet-count', true],
       ['PerCubicMeter', 'length', true],
       ['PerCubicMeter', 'width', true],
       ['PerCubicMeter', 'thickness', true],
     ] as const)('%s uses %s: %s', (mode, field, expected) => {
       expect(usesDimension(mode, field)).toBe(expected);
     });

     it('uses no dimension for a missing or unknown mode', () => {
       for (const mode of [undefined, null, 'Legacy']) {
         expect(DIMENSION_FIELDS.map((field) => usesDimension(mode, field))).toEqual([false, false, false, false]);
       }
     });
   });

   describe('highestPricingMode', () => {
     it('is PerUnit when there is no line', () => {
       expect(highestPricingMode([])).toBe('PerUnit');
     });

     it('ranks md + m² as m² in either order (the PricingMode union lists m² before md)', () => {
       expect(highestPricingMode([{ pricingMode: 'PerLinearMeter' }, { pricingMode: 'PerSquareMeter' }])).toBe('PerSquareMeter');
       expect(highestPricingMode([{ pricingMode: 'PerSquareMeter' }, { pricingMode: 'PerLinearMeter' }])).toBe('PerSquareMeter');
     });

     it('picks m³ over every other mode', () => {
       expect(
         highestPricingMode([{ pricingMode: 'PerUnit' }, { pricingMode: 'PerCubicMeter' }, { pricingMode: 'PerSquareMeter' }]),
       ).toBe('PerCubicMeter');
     });

     it('counts a missing or unknown mode as PerUnit', () => {
       expect(highestPricingMode([{}, { pricingMode: 'Legacy' }, { pricingMode: null }])).toBe('PerUnit');
       expect(highestPricingMode([{ pricingMode: 'Legacy' }, { pricingMode: 'PerLinearMeter' }])).toBe('PerLinearMeter');
     });
   });

   describe('shownDimensions', () => {
     it('shows no dimension for no line, a blank line or unit lines', () => {
       expect(shownDimensions([])).toEqual([]);
       expect(shownDimensions([{}, { pricingMode: 'PerUnit' }])).toEqual([]);
     });

     it('follows the highest mode on the voucher', () => {
       expect(shownDimensions([{ pricingMode: 'PerUnit' }, { pricingMode: 'PerLinearMeter' }])).toEqual(['sheet-count', 'length']);
       expect(shownDimensions([{ pricingMode: 'PerLinearMeter' }, { pricingMode: 'PerSquareMeter' }])).toEqual([
         'sheet-count', 'length', 'width',
       ]);
       expect(shownDimensions([{ pricingMode: 'PerCubicMeter' }, { pricingMode: 'PerUnit' }])).toEqual([
         'sheet-count', 'length', 'width', 'thickness',
       ]);
     });

     it('counts a missing or unknown mode as PerUnit', () => {
       expect(shownDimensions([{ pricingMode: 'Legacy' }, { pricingMode: undefined }])).toEqual([]);
     });

     it('adds forced columns, in column order', () => {
       expect(shownDimensions([{ pricingMode: 'PerUnit' }], ['width'])).toEqual(['width']);
       expect(
         shownDimensions([{ pricingMode: 'PerLinearMeter' }], new Set<DimensionField>(['thickness', 'sheet-count'])),
       ).toEqual(['sheet-count', 'length', 'thickness']);
       expect(shownDimensions([{ pricingMode: 'PerCubicMeter' }], ['width'])).toEqual([
         'sheet-count', 'length', 'width', 'thickness',
       ]);
       expect(shownDimensions([{ pricingMode: 'PerUnit' }], ['thickness', 'width'])).toEqual(['width', 'thickness']);
       expect(shownDimensions([], [])).toEqual([]);
     });
   });
   ```
   `'toString'` guards against a lookup with `in` or an object index, which would find `Object.prototype.toString`.
2. **Run it and check it fails:** `npx vitest run src/lib/dimension-columns.test.ts`. Expected: FAIL, "Tests no tests", with `Error: Failed to resolve import "./dimension-columns" from "src/lib/dimension-columns.test.ts". Does the file exist?`. The module does not exist on the base.
3. **Write the minimal implementation** `src/lib/dimension-columns.ts`:
   ```ts
   import type { PricingMode } from '@/features/products/types';

   // Dimension columns of the line grids, in column order (also the cell id names).
   export const DIMENSION_FIELDS = ['sheet-count', 'length', 'width', 'thickness'] as const;
   export type DimensionField = (typeof DIMENSION_FIELDS)[number];

   export function isDimensionField(field: string): field is DimensionField {
     return (DIMENSION_FIELDS as readonly string[]).includes(field);
   }

   // Pricing modes in rank order. The PricingMode union lists m² before md, so the rank comes only from here.
   export const PRICING_MODES: readonly PricingMode[] = ['PerUnit', 'PerLinearMeter', 'PerSquareMeter', 'PerCubicMeter'];

   export const PRICING_MODE_RANK = Object.fromEntries(
     PRICING_MODES.map((mode, rank) => [mode, rank]),
   ) as Record<PricingMode, number>;

   // The lowest rank that uses each dimension: md uses Số tấm and Dài, m² adds Rộng, m³ adds Dày.
   const MIN_RANK: Record<DimensionField, number> = { 'sheet-count': 1, length: 1, width: 2, thickness: 3 };

   // A missing or unknown mode (an old draft, corrupt data) counts as PerUnit.
   export function normalizePricingMode(mode: unknown): PricingMode {
     return (PRICING_MODES as readonly unknown[]).includes(mode) ? (mode as PricingMode) : 'PerUnit';
   }

   export function usesDimension(mode: unknown, field: DimensionField): boolean {
     return PRICING_MODE_RANK[normalizePricingMode(mode)] >= MIN_RANK[field];
   }

   export function highestPricingMode(lines: readonly { pricingMode?: unknown }[]): PricingMode {
     let rank = 0;
     for (const line of lines) rank = Math.max(rank, PRICING_MODE_RANK[normalizePricingMode(line.pricingMode)]);
     return PRICING_MODES[rank];
   }

   // The dimension columns a grid shows: those the highest mode uses, plus the forced ones, in column order.
   export function shownDimensions(
     lines: readonly { pricingMode?: unknown }[],
     forced: Iterable<DimensionField> = [],
   ): DimensionField[] {
     const highest = highestPricingMode(lines);
     const extra = new Set(forced);
     return DIMENSION_FIELDS.filter((field) => usesDimension(highest, field) || extra.has(field));
   }
   ```
   Nesting (md ⊂ m² ⊂ m³) holds by construction: a field is used from its minimum rank upwards. `PRICING_MODES` keeps the order of `line-navigation.ts:17`; Task 1.2 removes that copy.
4. **Run the test and check it passes:** `npx vitest run src/lib/dimension-columns.test.ts`. Expected: PASS, 35 tests. Then `npm run typecheck`: no error.
5. **Commit:** `cd "$WT" && git add frontend/src/lib/dimension-columns.ts frontend/src/lib/dimension-columns.test.ts && git commit -m "feat(lib): dimension columns follow the highest pricing mode"`

### Task 1.2 — Import `PRICING_MODES` from `lib/dimension-columns` (no behaviour change)

1–2. **Existing tests stay green (no red test is possible).** This moves one constant with the same value and order. No behaviour can change, so no test can fail first. The compiler is the check: a missed importer fails `npm run typecheck` with a missing export. Before the edit, confirm the importers: `git grep -n "PRICING_MODES" -- src`. Expected at HEAD: the definition at `src/pages/stock-vouchers/utils/line-navigation.ts:17`, the imports at `src/pages/stock-vouchers/components/stock-grid-settings-popover.tsx:5` and `src/pages/stock-vouchers/utils/use-stock-grid-prefs.ts:7`, their uses at popover :59 and prefs :58, and the new `src/lib/dimension-columns*` lines. `line-navigation.ts` defines the constant but never uses it, so it gets no import (with `noUnusedLocals` an unused import would fail typecheck).

3. **Write the change.**
   - `src/pages/stock-vouchers/utils/line-navigation.ts` (:13-19). Before:
     ```ts
     export const ENTER_STOP_FIELDS = LINE_FOCUS_FIELDS.filter(
       (f): f is EnterStopField => f !== 'product-code',
     );

     export const PRICING_MODES: readonly PricingMode[] = ['PerUnit', 'PerLinearMeter', 'PerSquareMeter', 'PerCubicMeter'];

     // Enter stops after the product code, per pricing mode (brainstorm §2).
     ```
     After:
     ```ts
     export const ENTER_STOP_FIELDS = LINE_FOCUS_FIELDS.filter(
       (f): f is EnterStopField => f !== 'product-code',
     );

     // Enter stops after the product code, per pricing mode (brainstorm §2).
     ```
     The `import type { PricingMode }` at :1 stays: `DEFAULT_ENTER_STOPS`, `enterPath` and `verticalTarget` use it. Do not re-export `PRICING_MODES`.
   - `src/pages/stock-vouchers/components/stock-grid-settings-popover.tsx:5`. Before:
     ```ts
     import { ENTER_STOP_FIELDS, PRICING_MODES, isFieldDisabled, type EnterStopField } from '@/pages/stock-vouchers/utils/line-navigation';
     ```
     After:
     ```ts
     import { PRICING_MODES } from '@/lib/dimension-columns';
     import { ENTER_STOP_FIELDS, isFieldDisabled, type EnterStopField } from '@/pages/stock-vouchers/utils/line-navigation';
     ```
   - `src/pages/stock-vouchers/utils/use-stock-grid-prefs.ts:3-10`. Before:
     ```ts
     import type { StockDirection } from '@/features/stock-reasons/types';
     import {
       DEFAULT_ENTER_STOPS,
       ENTER_STOP_FIELDS,
       PRICING_MODES,
       isFieldDisabled,
       type EnterStopField,
     } from './line-navigation';
     ```
     After:
     ```ts
     import type { StockDirection } from '@/features/stock-reasons/types';
     import { PRICING_MODES } from '@/lib/dimension-columns';
     import {
       DEFAULT_ENTER_STOPS,
       ENTER_STOP_FIELDS,
       isFieldDisabled,
       type EnterStopField,
     } from './line-navigation';
     ```
4. **Run the tests and check they pass:** `npm run typecheck` (no error), then `npx vitest run src/pages/stock-vouchers/utils src/pages/stock-vouchers/components/stock-grid-settings-popover.test.tsx src/lib/dimension-columns.test.ts`. Expected: PASS, including `line-navigation.test.ts`, `use-stock-grid-prefs.test.ts` and `stock-grid-settings-popover.test.tsx` without edits. `git grep -n "PRICING_MODES" -- src` now lists no line in `line-navigation.ts`.
5. **Commit:** `cd "$WT" && git add frontend/src/pages/stock-vouchers/utils/line-navigation.ts frontend/src/pages/stock-vouchers/utils/use-stock-grid-prefs.ts frontend/src/pages/stock-vouchers/components/stock-grid-settings-popover.tsx && git commit -m "refactor(stock-vouchers): import PRICING_MODES from lib/dimension-columns"`

### Task 1.3 — `isFieldDisabled` counts a missing or unknown mode as `PerUnit`

On the base, `isFieldDisabled` (`line-navigation.ts:29-44` before Task 1.2, :27-42 after it) compares strings: `'sheet-count'` and `'length'` are disabled only when `mode === 'PerUnit'`, and `'quantity'` is disabled when `mode !== 'PerUnit'`. So a row with `'Legacy'` (or no mode) gets **Số tấm** and **Dài** enabled and **SL** disabled: the opposite of `PerUnit`. **Rộng** and **Dày** use `!==` checks against the metre modes, so they are already disabled for any unknown string.

1. **Write the failing test.** Extend `src/pages/stock-vouchers/utils/line-navigation.test.ts`.
   - Add the type import after the vitest import (:1). Before:
     ```ts
     import { describe, expect, it } from 'vitest';
     import {
     ```
     After:
     ```ts
     import { describe, expect, it } from 'vitest';
     import type { PricingMode } from '@/features/products/types';
     import {
     ```
   - In `describe('verticalTarget')`, after the test `'falls back to the product code when the field is disabled or hidden there'` (:66-69), add:
     ```ts
       it('treats a row with an unknown mode as PerUnit: SL kept, Dài falls back', () => {
         // The grid passes the stored mode, typed PricingMode; an old draft or corrupt data can hold any string.
         const legacy = 'Legacy' as unknown as PricingMode;
         expect(verticalTarget(legacy, 'quantity', all)).toBe('quantity');
         expect(verticalTarget(legacy, 'length', all)).toBe('product-code');
       });
     ```
   - In `describe('isFieldDisabled')`, after the test `'matches the pricing mode rules'` (:73-80), add:
     ```ts
       it.each([
         ['PerUnit', [true, true, true, true, false]],
         ['PerLinearMeter', [false, false, true, true, true]],
         ['PerSquareMeter', [false, false, false, true, true]],
         ['PerCubicMeter', [false, false, false, false, true]],
       ] as const)('keeps the %s rule for Số tấm, Dài, Rộng, Dày, SL', (mode, expected) => {
         const fields: LineFocusField[] = ['sheet-count', 'length', 'width', 'thickness', 'quantity'];
         expect(fields.map((f) => isFieldDisabled(mode, f))).toEqual(expected);
       });

       it('treats a missing or unknown mode as PerUnit: Số tấm and Dài locked, SL open', () => {
         for (const mode of ['Legacy', undefined, null]) {
           expect(isFieldDisabled(mode, 'sheet-count')).toBe(true);
           expect(isFieldDisabled(mode, 'length')).toBe(true);
           expect(isFieldDisabled(mode, 'quantity')).toBe(false);
         }
       });

       it('keeps Rộng and Dày locked and the other fields open for a missing or unknown mode', () => {
         for (const mode of ['Legacy', undefined, null]) {
           expect(isFieldDisabled(mode, 'width')).toBe(true);
           expect(isFieldDisabled(mode, 'thickness')).toBe(true);
           expect(isFieldDisabled(mode, 'unit-price')).toBe(false);
           expect(isFieldDisabled(mode, 'warehouse')).toBe(false);
         }
       });
     ```
   `LineFocusField` is already imported (:9). The four `keeps the … rule` cases are the full 4 × 5 table for the valid modes: they pass on the base and must stay green, which proves the four modes keep today's behaviour.
2. **Run it and check it fails:** `npx vitest run src/pages/stock-vouchers/utils/line-navigation.test.ts`. Expected: FAIL, `Tests 2 failed | 13 passed (15)`:
   - `verticalTarget > treats a row with an unknown mode as PerUnit: SL kept, Dài falls back`: `AssertionError: expected 'product-code' to be 'quantity'`. On the base `isFieldDisabled('Legacy', 'quantity')` is `'Legacy' !== 'PerUnit'`, so `true`, and `verticalTarget` falls back to the product code.
   - `isFieldDisabled > treats a missing or unknown mode as PerUnit: Số tấm and Dài locked, SL open`: `AssertionError: expected false to be true` on the `'sheet-count'` line. On the base `isFieldDisabled('Legacy', 'sheet-count')` is `'Legacy' === 'PerUnit'`, so `false`.
   - Already green on the base, and not part of the failing proof: the four `keeps the … rule` cases and `keeps Rộng and Dày locked and the other fields open …` (the base's `!==` checks lock **Rộng** and **Dày** for any unknown string, and its `default` branch returns `false`).
3. **Write the minimal implementation** in `src/pages/stock-vouchers/utils/line-navigation.ts`.
   - Imports (:1). Before:
     ```ts
     import type { PricingMode } from '@/features/products/types';
     ```
     After:
     ```ts
     import type { PricingMode } from '@/features/products/types';
     import { normalizePricingMode, usesDimension } from '@/lib/dimension-columns';
     ```
   - `isFieldDisabled`. Before:
     ```ts
     // Số tấm for non-PerUnit; Dài for metre modes; Rộng for m² / m³; Dày for m³; SL only for PerUnit.
     export function isFieldDisabled(mode: PricingMode, field: LineFocusField): boolean {
       switch (field) {
         case 'sheet-count':
         case 'length':
           return mode === 'PerUnit';
         case 'width':
           return mode !== 'PerSquareMeter' && mode !== 'PerCubicMeter';
         case 'thickness':
           return mode !== 'PerCubicMeter';
         case 'quantity':
           return mode !== 'PerUnit';
         default:
           return false;
       }
     }
     ```
     After:
     ```ts
     // The dimensions follow usesDimension (md: Số tấm, Dài; m² adds Rộng; m³ adds Dày); SL is typed only on PerUnit.
     // A missing or unknown mode counts as PerUnit.
     export function isFieldDisabled(mode: unknown, field: LineFocusField): boolean {
       switch (field) {
         case 'sheet-count':
         case 'length':
         case 'width':
         case 'thickness':
           return !usesDimension(mode, field);
         case 'quantity':
           return normalizePricingMode(mode) !== 'PerUnit';
         default:
           return false;
       }
     }
     ```
     The parameter widens to `unknown` because the value is read from form data at run time. Every caller compiles unchanged (`PricingMode` is assignable to `unknown`), and Phases 02 and 03 may pass a possibly-missing mode. `enterPath` and `verticalTarget` keep `mode: PricingMode`.

   **Callers, for the four valid modes.** The 4 × 5 table above equals the base rule, so these behave as today: the stock grid's dimension cells (`stock-line-grid.tsx:505-507`, `numberInput`), its **SL** cell (:635), `verticalTarget` in `removeLine` (:368) and in the ↑/↓ handler (:484), `enterPath` in `pathOf` (:440-443) and in `onSelect` (:575), the popover's Enter-stop table (`stock-grid-settings-popover.tsx:59-64`, which loops `PRICING_MODES` only), and the prefs' `cleanStops` (`use-stock-grid-prefs.ts:42`) and `toggleEnterStop` (:94), which take a typed `PricingMode`. Only a stock row with a missing or unknown mode changes: its **SL** is enabled (the **SL** cell passes `line.pricingMode` without a fallback, and its value already falls back to `computePricingQuantity`'s `default` branch, the typed quantity). With an unknown string, **Số tấm** / **Dài** are also disabled, ↑/↓ and the delete focus keep **SL**, and they fall back from **Số tấm** / **Dài** to the product code; a missing mode already reached those as `PerUnit` through `?? 'PerUnit'` (:368, :484, :507). Enter on a row with an unknown string still throws in `pathOf` (`prefs.enterStops[mode]` is `undefined`), as on the base; Phase 02 normalizes `pathOf`.
4. **Run the tests and check they pass:** `npx vitest run src/pages/stock-vouchers src/lib/dimension-columns.test.ts` (under load, add `--testTimeout=30000 --maxWorkers=3`). Expected: PASS for every file, including `line-navigation.test.ts` (15 tests), `use-stock-grid-prefs.test.ts`, `stock-grid-settings-popover.test.tsx`, `stock-line-grid.test.tsx` and `stock-voucher-form-page.test.tsx` without edits (a dry run on e61e4f1b: 17 files, 428 tests). Then `npm run typecheck`: no error.
5. **Commit:** `cd "$WT" && git add frontend/src/pages/stock-vouchers/utils/line-navigation.ts frontend/src/pages/stock-vouchers/utils/line-navigation.test.ts && git commit -m "fix(stock-vouchers): isFieldDisabled counts a missing or unknown pricing mode as PerUnit"`

### Task 1.4 — `useAppearedKeys`

1. **Write the failing test** `src/lib/use-appeared-keys.test.ts`, with fake timers as in `src/lib/use-debounced-value.test.ts`:
   ```ts
   import { StrictMode } from 'react';
   import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
   import { act, renderHook } from '@testing-library/react';
   import { useAppearedKeys } from './use-appeared-keys';

   function renderKeys(keys: string[], durationMs?: number) {
     // A new array on every render, as the grids pass shownDimensions(...) without a memo.
     return renderHook(({ keys }) => useAppearedKeys([...keys], durationMs), { initialProps: { keys } });
   }

   describe('useAppearedKeys', () => {
     beforeEach(() => vi.useFakeTimers());
     afterEach(() => vi.useRealTimers());

     it('marks nothing on mount', () => {
       const { result } = renderKeys(['sheet-count', 'length']);
       expect(result.current.size).toBe(0);
     });

     it('marks the keys that were not there before', () => {
       const { result, rerender } = renderKeys(['sheet-count']);
       rerender({ keys: ['sheet-count', 'length', 'width'] });
       expect([...result.current]).toEqual(['length', 'width']);
     });

     it('keeps the marks and adds none when a new array holds the same keys', () => {
       const { result, rerender } = renderKeys([]);
       rerender({ keys: ['sheet-count', 'length'] });
       act(() => {
         vi.advanceTimersByTime(600);
       });
       rerender({ keys: ['sheet-count', 'length'] });
       expect([...result.current]).toEqual(['sheet-count', 'length']);
       // The re-render did not restart the timer: the marks go 1200 ms after the keys appeared.
       act(() => {
         vi.advanceTimersByTime(600);
       });
       expect(result.current.size).toBe(0);
     });

     it('clears a mark durationMs after its key appeared', () => {
       const { result, rerender } = renderKeys([]);
       rerender({ keys: ['width'] });
       act(() => {
         vi.advanceTimersByTime(1199);
       });
       expect(result.current.has('width')).toBe(true);
       act(() => {
         vi.advanceTimersByTime(1);
       });
       expect(result.current.has('width')).toBe(false);
     });

     it('clears a key added while another is marked durationMs after its own appearance', () => {
       const { result, rerender } = renderKeys([], 1000);
       rerender({ keys: ['length'] });
       act(() => {
         vi.advanceTimersByTime(400);
       });
       rerender({ keys: ['length', 'width'] });
       expect([...result.current]).toEqual(['length', 'width']);
       act(() => {
         vi.advanceTimersByTime(600);
       });
       expect([...result.current]).toEqual(['width']);
       act(() => {
         vi.advanceTimersByTime(399);
       });
       expect(result.current.has('width')).toBe(true);
       act(() => {
         vi.advanceTimersByTime(1);
       });
       expect(result.current.size).toBe(0);
     });

     it('drops a removed key at once, with its timer, and never marks it', () => {
       const { result, rerender } = renderKeys(['sheet-count', 'length']);
       rerender({ keys: ['sheet-count'] });
       expect(result.current.size).toBe(0);
       rerender({ keys: ['sheet-count', 'width'] });
       expect([...result.current]).toEqual(['width']);
       rerender({ keys: ['sheet-count'] });
       expect(result.current.size).toBe(0);
       expect(vi.getTimerCount()).toBe(0);
     });

     it('marks a key that hides and comes back for durationMs after its second appearance', () => {
       const { result, rerender } = renderKeys(['sheet-count']);
       rerender({ keys: ['sheet-count', 'width'] });
       act(() => {
         vi.advanceTimersByTime(500);
       });
       rerender({ keys: ['sheet-count'] });
       rerender({ keys: ['sheet-count', 'width'] });
       // The first appearance's timer would have fired at 1200 ms; the second appearance keeps the mark until 1700 ms.
       act(() => {
         vi.advanceTimersByTime(1199);
       });
       expect([...result.current]).toEqual(['width']);
       act(() => {
         vi.advanceTimersByTime(1);
       });
       expect(result.current.size).toBe(0);
     });

     it('marks new keys inside StrictMode', () => {
       const { result, rerender } = renderHook(({ keys }) => useAppearedKeys([...keys]), {
         initialProps: { keys: ['sheet-count'] },
         wrapper: StrictMode,
       });
       expect(result.current.size).toBe(0);
       rerender({ keys: ['sheet-count', 'length'] });
       expect([...result.current]).toEqual(['length']);
     });

     it('leaves no timer behind after unmount', () => {
       const { rerender, unmount } = renderKeys([]);
       rerender({ keys: ['length'] });
       expect(vi.getTimerCount()).toBe(1);
       unmount();
       expect(vi.getTimerCount()).toBe(0);
     });
   });
   ```
   The nine tests are the nine cases of SUMMARY → Testing → New unit tests, in its order (section 02 M3 counted eight before V16 added hide-and-return). The last test is the "no state update after unmount" case: React 18 no longer warns on a state update after unmount, so the test checks that no timer is left that could make one.
2. **Run it and check it fails:** `npx vitest run src/lib/use-appeared-keys.test.ts`. Expected: FAIL, "Tests no tests", with `Error: Failed to resolve import "./use-appeared-keys" from "src/lib/use-appeared-keys.test.ts". Does the file exist?`. The module does not exist on the base.
3. **Write the minimal implementation** `src/lib/use-appeared-keys.ts`:
   ```ts
   import { useEffect, useLayoutEffect, useRef, useState } from 'react';

   // Keys that were added since the first commit, each kept for `durationMs` after it appeared (the flash of newly
   // shown grid columns). The first commit marks nothing, so opening a saved document does not flash.
   export function useAppearedKeys(keys: readonly string[], durationMs = 1200): ReadonlySet<string> {
     // Compared by value (keys never hold '|'): callers build a new array on every render.
     const signature = keys.join('|');
     // Written only by the layout effect. A ref written during render would already hold the new keys on StrictMode's
     // second render, and nothing would ever be marked in dev.
     const previous = useRef<string[] | null>(null);
     const timers = useRef(new Map<string, ReturnType<typeof setTimeout>>());
     const [appeared, setAppeared] = useState<ReadonlySet<string>>(() => new Set());

     useLayoutEffect(() => {
       const current = signature === '' ? [] : signature.split('|');
       const before = previous.current;
       previous.current = current;
       if (before === null) return;
       const added = current.filter((key) => !before.includes(key));
       const removed = before.filter((key) => !current.includes(key));
       if (added.length === 0 && removed.length === 0) return;
       // A key that went away loses its mark and its timer; the other keys keep theirs.
       for (const key of removed) {
         clearTimeout(timers.current.get(key));
         timers.current.delete(key);
       }
       for (const key of added) {
         const id = setTimeout(() => {
           timers.current.delete(key);
           setAppeared((marks) => {
             const next = new Set(marks);
             next.delete(key);
             return next;
           });
         }, durationMs);
         timers.current.set(key, id);
       }
       setAppeared((marks) => {
         const next = new Set(marks);
         for (const key of removed) next.delete(key);
         for (const key of added) next.add(key);
         return next;
       });
     }, [signature, durationMs]);

     useEffect(() => {
       const pending = timers.current;
       return () => {
         for (const id of pending.values()) clearTimeout(id);
         pending.clear();
       };
     }, []);

     return appeared;
   }
   ```
   How this meets SUMMARY → Units:
   - The layout effect is keyed on the joined string, so a re-render with a new array of the same keys (every keystroke) does not run it and keeps the marks. The state update inside a layout effect re-renders before paint, so the class lands in the same paint as the new column.
   - `previous` starts as `null` and only the effect reads or writes it. Under `StrictMode` (`main.tsx:8`) the mount effects run twice; the second run finds the same keys and marks nothing.
   - Each new key gets its own timer. A later key change never cancels the timer of a key that is still marked. The timer of a key that went away is cleared with its mark: that key has no flash left (test "drops a removed key at once, with its timer…"), and a key that comes back gets a full flash of its own (test "marks a key that hides and comes back…").
   - The unmount effect copies the map into `pending` because `react-hooks/exhaustive-deps` warns about reading `ref.current` in a cleanup. The map is never replaced, so it is the same object.

   A dry run on e61e4f1b checked that each defect named in section 02 (M3) fails its own test: a render-phase ref fails the `StrictMode` test, an effect cleanup that clears timers on a key change fails "clears a key added while another is marked…", a removed key that keeps its timer fails "drops a removed key at once, with its timer…" and "marks a key that hides and comes back…", a missing unmount cleanup fails "leaves no timer behind…", and marking on mount fails "marks nothing on mount". An effect keyed on the array itself that sets state on every run loops forever ('Maximum update depth exceeded') and fails eight tests, because each test passes a new array on every render, as the grids do.
4. **Run the test and check it passes:** `npx vitest run src/lib/use-appeared-keys.test.ts src/lib/use-debounced-value.test.ts`. Expected: PASS, 9 tests in the new file and 3 in `use-debounced-value.test.ts` (12 in all). Then `npx eslint src/lib/use-appeared-keys.ts src/lib/use-appeared-keys.test.ts`: no warning.
5. **Commit:** `cd "$WT" && git add frontend/src/lib/use-appeared-keys.ts frontend/src/lib/use-appeared-keys.test.ts && git commit -m "feat(lib): useAppearedKeys marks newly shown keys for a flash"`

### Task 1.5 — Flash token and `col-appeared` rules (CSS only)

1–2. **Existing tests stay green (no red test is possible).** vitest runs with `css: false` (`vitest.config.ts`), so no test can see a colour or an animation. Nothing renders `col-appeared` yet, so both grids look as today. Phase 04's real-browser check verifies the colours, the readable header text in light and dark mode, and the reduced-motion rule. Here the check is that the build parses the CSS and ships the rules.

3. **Write the change.**
   - `src/styles/control-tokens.css` (:25-27), next to `--qldh-brand-header`. Before:
     ```css
         --qldh-brand-header: #005bac;
         --qldh-brand-header-foreground: #ffffff;
         --qldh-cell-focus-bg: #fff8dc;
     ```
     After:
     ```css
         --qldh-brand-header: #005bac;
         --qldh-brand-header-foreground: #ffffff;
         --qldh-col-appeared-head-bg: #b45309;
         --qldh-cell-focus-bg: #fff8dc;
     ```
     The file has no `.dark` block, so the token is the same in both themes, like the brand header.
   - `src/pages/quotations/components/line-items-grid.css`: append after the last rule, `.accounting-grid .cell-input:disabled` (:353-357). Before (end of file):
     ```css
     .accounting-grid .cell-input:disabled {
       background: #e9ecef;
       color: hsl(var(--muted-foreground));
       cursor: not-allowed;
     }
     ```
     After:
     ```css
     .accounting-grid .cell-input:disabled {
       background: #e9ecef;
       color: hsl(var(--muted-foreground));
       cursor: not-allowed;
     }

     /* Columns that just appeared (useAppearedKeys): a background fade from a `from` step only, so each element ends on
      * its own background. The header text colour never animates: white stays readable on the start colour.
      * A "—" cell is a disabled input whose opaque grey would hide the cell's fade, so the input fades in from
      * transparent and the cell's colour shows through it. It must not start from --nav-match too: in dark mode that
      * token has alpha 0.3, and two layers would flash brighter. An enabled input is transparent and shows the cell's
      * fade; a focused one keeps its focus colour on top. */
     @keyframes col-appeared-head {
       from { background-color: var(--qldh-col-appeared-head-bg); }
     }

     @keyframes col-appeared-cell {
       from { background-color: hsl(var(--nav-match)); }
     }

     @keyframes col-appeared-input {
       from { background-color: transparent; }
     }

     .accounting-grid th.col-appeared {
       animation: col-appeared-head 1.2s ease-out;
     }

     .accounting-grid td.col-appeared {
       animation: col-appeared-cell 1.2s ease-out;
     }

     .accounting-grid td.col-appeared .cell-input:disabled {
       animation: col-appeared-input 1.2s ease-out;
     }

     @media (prefers-reduced-motion: reduce) {
       .accounting-grid th.col-appeared,
       .accounting-grid td.col-appeared,
       .accounting-grid td.col-appeared .cell-input:disabled {
         animation: none;
       }
     }
     ```
   **Why these selectors win.** Both grid tables carry `accounting-grid` (`stock-line-grid.tsx:788`, `line-items-grid.tsx:242`), and both files already import this stylesheet; the cash, allocation and open-item grids import it too and also carry `accounting-grid`, but never set `col-appeared`.
   - The three keyframes match SUMMARY → Behaviour → Flash: the header from the new token, the `<td>` from `hsl(var(--nav-match))`, and the "—" input from `transparent` (its own keyframe, so the `<td>` colour shows through while the input's grey fades back in; section 02 V15 explains why the input must not start from `--nav-match`).
   - The keyframes animate only `background-color`. While an animation runs, its value overrides every normal declaration of that property, whatever their specificity (the cascade's animation origin). So the flash shows over `th` `background: var(--qldh-brand-header)` (:35-43), `td` `background: hsl(var(--card))` (:26-33), the hover tint (:49-51), and the opaque disabled backgrounds `.accounting-grid .cell-input:disabled` (:353-357, specificity 0,3,0) and `.stock-grid .cell-input:disabled` (`stock-line-grid.css:53-55`, 0,3,0). Without a `to` step, each element fades to its own current background.
   - Specificity matters only between `animation` declarations. No other rule on these elements sets `animation`. The reduced-motion block repeats the same three selectors (`th.col-appeared` 0,2,1; `td.col-appeared` 0,2,1; `td.col-appeared .cell-input:disabled` 0,4,1) later in the file, so `animation: none` wins over them.
   - The header text keeps `color: var(--qldh-brand-header-foreground)` (white): no keyframe touches `color`.
   - A "—" cell is a disabled `.cell-input` that fills the `<td>` (`width: 100%; height: 33px`, :67-78) and paints an opaque background, so the input itself animates, from `transparent` to its own grey (`#e9ecef`, or `hsl(var(--muted))` on the stock grid), under the `<td>`'s `--nav-match` fade. An enabled input is `background: transparent` (:75) and shows the `<td>` fade. A focused input sets `background: var(--qldh-cell-focus-bg)` (:92-95), is not in the animated selectors, and covers the `<td>` fade, so the focused cell keeps its focus colour and does not flash.
   - `hsl(var(--nav-match))` reads `48 100% 85%` in light mode (`index.css:48`) and `47.9 95.8% 53.1% / 0.3` in dark mode (`index.css:92`); the space-separated syntax with `/ alpha` is valid in `hsl()`.
   - The `.dxr-cell` rules (:154-168) and `.accounting-grid { min-width: 1360px }` (:16-24) stay; Phase 03 removes `.dxr-cell`.
4. **Run the checks:** `npm run build` (runs `tsc -b` and `vite build`; `dist/` is git-ignored). Expected: success. The build rewrites the tracked `tsconfig.app.tsbuildinfo`; restore it at once: `git checkout -- tsconfig.app.tsbuildinfo`. Then check the bundle (minified, so `from` reads `0%`):
   - `grep -o "col-appeared-[a-z]*{0%{[^}]*}" dist/assets/*.css` prints the three keyframes: `col-appeared-head{0%{background-color:var(--qldh-col-appeared-head-bg)}`, `col-appeared-cell{0%{background-color:hsl(var(--nav-match))}` and `col-appeared-input{0%{background-color:transparent}`.
   - `grep -o "[^{}]*col-appeared[^{}]*{animation:[^}]*}" dist/assets/*.css` prints the `th` rule (`col-appeared-head`), the `td` rule (`col-appeared-cell`), the disabled-input rule (`col-appeared-input`), all `1.2s ease-out`, and the reduced-motion rule (the three selectors with `animation:none`).
   - `grep -o "\-\-qldh-col-appeared-head-bg:[^;]*" dist/assets/*.css` prints `--qldh-col-appeared-head-bg: #b45309`.

   `npx vitest run src/pages/quotations src/pages/stock-vouchers` stays green.
5. **Commit:** `git status --short` lists only the two CSS files. `cd "$WT" && git add frontend/src/styles/control-tokens.css frontend/src/pages/quotations/components/line-items-grid.css && git commit -m "feat(grids): col-appeared flash for newly shown columns"`

## Verification

- `npx vitest run src/lib src/pages/stock-vouchers src/pages/quotations` (under load, add `--testTimeout=30000 --maxWorkers=3`; a dry run of this phase on e61e4f1b: 57 files, 831 tests passed).
- `npm run typecheck`
- `npm run lint` (no new warning in the changed files)
- `npm run build`, then `git checkout -- tsconfig.app.tsbuildinfo`; `git status --short` is empty, and `git log <phase base>.. -- tsconfig.app.tsbuildinfo` prints nothing, where `<phase base>` is the hash recorded before Task 1.1 (see "Before Task 1.1"; plan SUMMARY → Execution notes).
- `git grep -n "PRICING_MODES" -- src` shows the definition only in `src/lib/dimension-columns.ts`; the popover and the prefs import it from `@/lib/dimension-columns` (the new test imports `./dimension-columns`), and `line-navigation.ts` has no line.
- `npm run help:stale` is informational in this phase. On the base (qlbanhang/main 438b0db4 or later, where the worktree starts) it already prints 3 warnings (`admin-dashboard`, `home`, `reports-revenue`; `inventory-stock-card` is no longer flagged since PR #31). After these commits it also lists `stock-in.form` and `stock-out.form` (`sources` hold `frontend/src/pages/stock-vouchers/**`) and `quotations.form` (`frontend/src/pages/quotations/components/**` holds `line-items-grid.css`). No help file changes here: Phase 04 commits the help files last, after the real-browser check.
- Set **Status:** to `[x] done (<today>)` in this file and commit it: `cd "$WT" && git add docs/plans/261016-0249-dimension-columns-by-pricing-mode/phase-01-shared-rule.md && git commit -m "docs(plan): phase 01 done"`.

## Exit Criteria

- `src/lib/dimension-columns.ts` exports exactly the shared contract: `DIMENSION_FIELDS`, `DimensionField`, `isDimensionField`, `PRICING_MODES` (rank order `PerUnit`, `PerLinearMeter`, `PerSquareMeter`, `PerCubicMeter`), `PRICING_MODE_RANK` (derived from it), `normalizePricingMode`, `usesDimension`, `highestPricingMode`, `shownDimensions`.
- `src/lib/use-appeared-keys.ts` exports `useAppearedKeys(keys: readonly string[], durationMs = 1200): ReadonlySet<string>` and passes its 9 tests, including `StrictMode` and hide-and-return.
- `line-navigation.ts` no longer defines `PRICING_MODES` and does not re-export it. `isFieldDisabled` gives today's result for the four valid modes (the 4 × 5 table) and treats a missing or unknown mode as `PerUnit`.
- The token `--qldh-col-appeared-head-bg: #b45309` and the `col-appeared` rules are in place (header from the token, `<td>` from `--nav-match`, the "—" input from `transparent`, none under reduced motion); no component sets the class yet.
- Both grids look and behave as today for the four valid modes: every existing test in `src/pages/stock-vouchers` and `src/pages/quotations` passes without edits.
