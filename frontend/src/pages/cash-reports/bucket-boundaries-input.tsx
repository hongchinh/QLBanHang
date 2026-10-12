import { useState } from 'react';
import { DEFAULT_BUCKETS, normalizeBuckets } from '@/features/cash-reports/aging';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

export const BUCKETS_ERROR = 'Mốc không hợp lệ: các số nguyên dương tăng dần, cách nhau bởi dấu phẩy, tối đa 6 mốc';

// Aging bucket boundaries typed by the user. The raw `buckets` url value is the source of truth: a valid typed
// value is committed through onCommit (normalized), an invalid one only shows the error and never reaches the API.
export function BucketBoundariesInput({
  id,
  label,
  rawValue,
  onCommit,
}: {
  id: string;
  label: string;
  rawValue: string;
  onCommit: (normalized: string) => void;
}) {
  const [text, setText] = useState(rawValue || DEFAULT_BUCKETS);
  const [typedInvalid, setTypedInvalid] = useState(false);
  // The page stays mounted when only the query string changes (e.g. the sidebar link), so re-derive the
  // typed text from the URL whenever its raw value changes.
  const [syncedRaw, setSyncedRaw] = useState(rawValue);
  if (syncedRaw !== rawValue) {
    setSyncedRaw(rawValue);
    setText(rawValue || DEFAULT_BUCKETS);
    setTypedInvalid(false);
  }

  const urlInvalid = normalizeBuckets(rawValue) === null;

  const commit = () => {
    const normalized = normalizeBuckets(text);
    if (normalized === null) {
      setTypedInvalid(true);
      return;
    }
    setTypedInvalid(false);
    setText(normalized);
    onCommit(normalized);
  };

  return (
    <>
      <div className="space-y-1">
        <Label htmlFor={id}>{label}</Label>
        <Input
          id={id}
          value={text}
          onChange={(e) => setText(e.target.value)}
          onBlur={commit}
          onKeyDown={(e) => {
            if (e.key === 'Enter') commit();
          }}
        />
      </div>
      {(typedInvalid || urlInvalid) && <p className="text-sm text-destructive sm:col-span-full">{BUCKETS_ERROR}</p>}
    </>
  );
}
