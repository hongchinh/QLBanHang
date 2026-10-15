import { useCallback, useRef, useState } from 'react';
import { focusById } from '@/lib/enter-focus';
import type { PartnerNoun } from './search-summary';

// Blocks a save while text is typed in the partner field but no partner was picked (section-01 §5.4).
// The field reports the state through `onUnpickedChange`; the form calls `assertPicked()` before saving.
export function useUnpickedPartnerGuard(codeInputId: string, noun: PartnerNoun = 'đối tượng') {
  const hasText = useRef(false);
  const [error, setError] = useState<string | undefined>();
  const onUnpickedChange = useCallback((has: boolean) => {
    hasText.current = has;
    if (!has) setError(undefined);
  }, []);
  const assertPicked = useCallback(() => {
    if (!hasText.current) return true;
    setError(`Vui lòng chọn ${noun} từ danh sách`);
    focusById(codeInputId);
    return false;
  }, [codeInputId, noun]);
  return { error, onUnpickedChange, assertPicked };
}
