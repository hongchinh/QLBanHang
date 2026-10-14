import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { PERIOD_PRESET_LABELS, periodPreset, type PeriodPresetKey } from '@/lib/period-presets';

interface Props {
  id: string;
  onChange: (range: { from: string; to: string }) => void;
  // yyyy-MM-dd; defaults to today's VN date. Tests pass a fixed day.
  today?: string;
}

const KEYS = Object.keys(PERIOD_PRESET_LABELS) as PeriodPresetKey[];

// A quick picker for the date range of a report. It only fills the range; the value stays empty
// so the same preset can be chosen again after the dates were edited by hand.
export function PeriodPresetSelect({ id, onChange, today }: Props) {
  return (
    <div className="space-y-1">
      <Label htmlFor={id}>Kỳ</Label>
      <Select value="" onValueChange={(key) => onChange(periodPreset(key as PeriodPresetKey, today))}>
        <SelectTrigger id={id}>
          <SelectValue placeholder="Chọn nhanh kỳ" />
        </SelectTrigger>
        <SelectContent>
          {KEYS.map((key) => (
            <SelectItem key={key} value={key}>
              {PERIOD_PRESET_LABELS[key]}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
