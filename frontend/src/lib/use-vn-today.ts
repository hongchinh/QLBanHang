import { useEffect, useState } from 'react';
import { todayYmd } from './vn-datetime';

// The longest wait between two looks at the clock. A timer set for midnight can fire late when the computer
// sleeps or its clock is changed, so the date is read again at least once a minute.
const MAX_WAIT_MS = 60_000;

// Today's VN date ('yyyy-MM-dd'). It changes at VN midnight while the page stays open (the browser runs in VN
// time, lib/vn-datetime.ts).
export function useVnToday(): string {
  const [today, setToday] = useState(() => todayYmd());

  useEffect(() => {
    let timer: ReturnType<typeof setTimeout>;
    const schedule = () => {
      const now = new Date();
      const midnight = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1);
      timer = setTimeout(
        () => {
          setToday(todayYmd());
          schedule();
        },
        Math.min(midnight.getTime() - now.getTime(), MAX_WAIT_MS),
      );
    };
    schedule();
    return () => clearTimeout(timer);
  }, []);

  return today;
}
