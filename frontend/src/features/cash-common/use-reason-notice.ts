import { useCallback, useEffect, useRef, useState } from 'react';

const FLASH_MS = 1600;

/** The "reason changed" notice of a voucher form: the text stays, the flash highlight lasts 1.6 s. */
export function useReasonSwitchNotice(): { notice: string | null; flash: boolean; show(text: string): void; clear(): void } {
  const [notice, setNotice] = useState<string | null>(null);
  const [flash, setFlash] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const stopTimer = useCallback(() => {
    if (timer.current !== null) {
      clearTimeout(timer.current);
      timer.current = null;
    }
  }, []);

  const show = useCallback((text: string) => {
    stopTimer();
    setNotice(text);
    setFlash(true);
    timer.current = setTimeout(() => {
      timer.current = null;
      setFlash(false);
    }, FLASH_MS);
  }, [stopTimer]);

  const clear = useCallback(() => {
    stopTimer();
    setNotice(null);
    setFlash(false);
  }, [stopTimer]);

  useEffect(() => stopTimer, [stopTimer]);

  return { notice, flash, show, clear };
}
