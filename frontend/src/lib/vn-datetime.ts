import { format } from 'date-fns';

// The API returns instants (timestamptz); the browser is assumed to run in VN time (D14).
// Never derive a local date with toISOString().slice(0, 10): before 07:00 VN it gives the previous day.

// ISO instant → value for <input type="datetime-local"> in local time.
export function toDateTimeLocalValue(iso: string): string {
  return format(new Date(iso), "yyyy-MM-dd'T'HH:mm");
}

// <input type="datetime-local"> value (local time) → ISO instant.
export function fromDateTimeLocalValue(local: string): string {
  return new Date(local).toISOString();
}

export function todayYmd(now: Date = new Date()): string {
  return format(now, 'yyyy-MM-dd');
}

export function firstDayOfMonthYmd(now: Date = new Date()): string {
  return format(now, 'yyyy-MM-01');
}
