import { useEffect, useRef, useState } from 'react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { computePreset, matchActivePreset } from '@/lib/date-range-presets';
import type { RangePreset } from '@/lib/date-range-presets';

interface QuotationDateFilterProps {
  from: string;
  to: string;
  onChange: (from: string, to: string) => void;
}

const PRESETS: { key: RangePreset; label: string }[] = [
  { key: '7d', label: '7N' },
  { key: '30d', label: '30N' },
  { key: 'this-month', label: 'Tháng này' },
  { key: 'last-month', label: 'Tháng trước' },
];

export function QuotationDateFilter({ from, to, onChange }: QuotationDateFilterProps) {
  const [open, setOpen] = useState(false);
  const [draftFrom, setDraftFrom] = useState(from);
  const [draftTo, setDraftTo] = useState(to);
  const wrapRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    setDraftFrom(from);
    setDraftTo(to);
    function handleClick(e: MouseEvent) {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false);
    }
    function handleKey(e: KeyboardEvent) {
      if (e.key === 'Escape') setOpen(false);
    }
    document.addEventListener('mousedown', handleClick);
    document.addEventListener('keydown', handleKey);
    return () => {
      document.removeEventListener('mousedown', handleClick);
      document.removeEventListener('keydown', handleKey);
    };
  }, [open, from, to]);

  const noFilter = from === '' && to === '';
  const activePreset = noFilter ? null : matchActivePreset(from, to, PRESETS.map((p) => p.key));
  const isCustomActive = !noFilter && activePreset === null;

  const pillCls = (active: boolean) =>
    cn(
      'rounded-full border px-3 py-1 text-xs font-medium transition-colors',
      active
        ? 'border-foreground bg-foreground text-background'
        : 'border-border bg-card text-muted-foreground hover:bg-accent',
    );

  return (
    <div className="flex flex-wrap items-center gap-2" ref={wrapRef}>
      <button type="button" onClick={() => onChange('', '')} className={pillCls(noFilter)}>
        Tất cả
      </button>
      {PRESETS.map((p) => (
        <button
          key={p.key}
          type="button"
          aria-label={p.key === '7d' ? '7 ngày gần nhất' : p.key === '30d' ? '30 ngày gần nhất' : undefined}
          onClick={() => { const r = computePreset(p.key); onChange(r.from, r.to); }}
          className={pillCls(activePreset === p.key)}
        >
          {p.label}
        </button>
      ))}
      <div className="relative">
        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          className={pillCls(isCustomActive)}
        >
          Tuỳ chỉnh ▾
        </button>
        {open && (
          <div className="absolute right-0 top-9 z-20 w-64 rounded-lg border border-border bg-popover p-3 shadow-md">
            <div className="space-y-2">
              <label className="block text-xs text-muted-foreground">
                Từ ngày
                <Input type="date" value={draftFrom} onChange={(e) => setDraftFrom(e.target.value)} className="mt-1" />
              </label>
              <label className="block text-xs text-muted-foreground">
                Đến ngày
                <Input type="date" value={draftTo} onChange={(e) => setDraftTo(e.target.value)} className="mt-1" />
              </label>
              <div className="flex justify-end gap-2 pt-1">
                <Button variant="ghost" size="sm" onClick={() => setOpen(false)}>Huỷ</Button>
                <Button
                  size="sm"
                  disabled={!draftFrom || !draftTo}
                  onClick={() => { onChange(draftFrom, draftTo); setOpen(false); }}
                >
                  Áp dụng
                </Button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
