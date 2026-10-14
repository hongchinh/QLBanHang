import { useEffect, useState } from 'react';
import { cn } from '@/lib/utils';
import { AppIcon } from '@/components/icons';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { computePreset, matchActivePreset } from '@/lib/date-range-presets';
import type { PresetOption, RangePreset } from '@/lib/date-range-presets';

interface RangePickerProps {
  from: string;
  to: string;
  onChange: (from: string, to: string) => void;
  onPreset?: (preset: RangePreset) => void;
  // The chips in front of "Tuỳ chỉnh"; default DEFAULT_PRESETS.
  presets?: PresetOption[];
  // Why a custom range cannot be applied (shown under the dates, "Áp dụng" disabled), or null.
  // Default: "to" before "from" is refused.
  validate?: (from: string, to: string) => string | null;
}

const orderedRange = (from: string, to: string) => (to < from ? 'Đến ngày phải sau hoặc bằng từ ngày.' : null);

const DEFAULT_PRESETS: PresetOption[] = [
  { key: 'today', label: 'Hôm nay' },
  { key: '7d', label: '7N' },
  { key: '30d', label: '30N' },
  { key: 'this-month', label: 'Tháng này' },
  { key: 'last-month', label: 'Tháng trước' },
];

export function RangePicker({
  from,
  to,
  onChange,
  onPreset,
  presets = DEFAULT_PRESETS,
  validate = orderedRange,
}: RangePickerProps) {
  const [open, setOpen] = useState(false);
  const [draftFrom, setDraftFrom] = useState(from);
  const [draftTo, setDraftTo] = useState(to);

  useEffect(() => {
    setDraftFrom(from);
    setDraftTo(to);
  }, [from, to]);

  const matchPreset = matchActivePreset(from, to, presets.map((p) => p.key));

  const handlePresetClick = (preset: RangePreset) => {
    if (onPreset) onPreset(preset);
    else {
      const { from: f, to: t } = computePreset(preset);
      onChange(f, t);
    }
  };

  const draftError = draftFrom && draftTo ? validate(draftFrom, draftTo) : null;
  const canApply = Boolean(draftFrom && draftTo) && draftError === null;

  const handleApply = () => {
    if (!canApply) return;
    onChange(draftFrom, draftTo);
    setOpen(false);
  };

  const chipClass = (active: boolean) =>
    cn(
      'rounded-full border px-3 py-1 text-xs font-medium transition-colors',
      active ? 'border-foreground bg-foreground text-background' : 'border-border bg-card text-muted-foreground hover:bg-accent',
    );

  // The selected chip is pressed (not only darker). "Tuỳ chỉnh" opens the dates in a popover: Radix gives it
  // aria-expanded, Escape and an outside click close it, the focus moves to "Từ ngày", and it moves itself
  // back on screen when "Tuỳ chỉnh" wraps to the start of a line on a phone.
  return (
    <div role="group" aria-label="Kỳ báo cáo" className="flex flex-wrap items-center gap-2">
      {presets.map((p) => (
        <button
          key={p.key}
          type="button"
          aria-pressed={matchPreset === p.key}
          onClick={() => handlePresetClick(p.key)}
          className={chipClass(matchPreset === p.key)}
        >
          {p.label}
        </button>
      ))}
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <button type="button" aria-pressed={matchPreset == null} className={cn(chipClass(matchPreset == null), 'inline-flex items-center gap-1')}>
            Tuỳ chỉnh
            <AppIcon name="chevronDown" size={14} />
          </button>
        </PopoverTrigger>
        <PopoverContent align="end" collisionPadding={16} className="w-72 max-w-[calc(100vw-2rem)] p-3">
          <div className="space-y-2">
            <label className="block text-xs text-muted-foreground">
              Từ ngày
              <Input type="date" value={draftFrom} onChange={(e) => setDraftFrom(e.target.value)} className="mt-1" />
            </label>
            <label className="block text-xs text-muted-foreground">
              Đến ngày
              <Input type="date" value={draftTo} onChange={(e) => setDraftTo(e.target.value)} className="mt-1" />
            </label>
            {draftError && (
              <p role="alert" className="text-xs text-rose-700">
                {draftError}
              </p>
            )}
            <div className="flex justify-end gap-2 pt-1">
              <Button variant="ghost" size="sm" onClick={() => setOpen(false)}>
                Huỷ
              </Button>
              <Button size="sm" onClick={handleApply} disabled={!canApply}>
                Áp dụng
              </Button>
            </div>
          </div>
        </PopoverContent>
      </Popover>
      <span className="ml-1 inline-flex items-center gap-1 text-xs text-muted-foreground">
        {from} <AppIcon name="arrowRight" size={12} label="đến" /> {to}
      </span>
    </div>
  );
}
