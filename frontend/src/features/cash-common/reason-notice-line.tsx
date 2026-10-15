import { cn } from '@/lib/utils';
import type { ReasonNotice } from './reason-notice';

// The one line under the reason select, shared by the cash and stock forms: the field's own error wins over the
// notice, so the two never stack.
export function ReasonNoticeLine({ error, notice }: { error?: string; notice: ReasonNotice | null }) {
  const text = error ?? notice?.text;
  if (!text) return null;
  const isError = !!error || notice?.tone === 'error';
  return (
    <p
      role="status"
      className={cn('field-message field-message-code', isError ? 'text-destructive' : 'text-amber-700')}
    >
      {text}
    </p>
  );
}
