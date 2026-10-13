import { describe, expect, it } from 'vitest';
import { PERIOD_PRESET_LABELS, periodPreset, type PeriodPresetKey } from './period-presets';

describe('periodPreset', () => {
  const cases: [PeriodPresetKey, string, string, string][] = [
    ['thisMonth', '2026-10-13', '2026-10-01', '2026-10-31'],
    ['lastMonth', '2026-10-13', '2026-09-01', '2026-09-30'],
    ['thisQuarter', '2026-10-13', '2026-10-01', '2026-12-31'],
    ['lastQuarter', '2026-10-13', '2026-07-01', '2026-09-30'],
    ['thisYear', '2026-10-13', '2026-01-01', '2026-12-31'],
    ['lastYear', '2026-10-13', '2025-01-01', '2025-12-31'],
    ['lastMonth', '2026-01-15', '2025-12-01', '2025-12-31'],
    ['lastQuarter', '2026-01-15', '2025-10-01', '2025-12-31'],
    ['lastMonth', '2024-03-10', '2024-02-01', '2024-02-29'],
  ];

  it.each(cases)('%s on %s', (key, today, from, to) => {
    expect(periodPreset(key, today)).toEqual({ from, to });
  });

  it('has a Vietnamese label for every key', () => {
    expect(PERIOD_PRESET_LABELS).toEqual({
      thisMonth: 'Tháng này',
      lastMonth: 'Tháng trước',
      thisQuarter: 'Quý này',
      lastQuarter: 'Quý trước',
      thisYear: 'Năm nay',
      lastYear: 'Năm trước',
    });
  });
});
