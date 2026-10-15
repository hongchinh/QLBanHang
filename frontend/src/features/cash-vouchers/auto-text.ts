import { toNumberOrZero } from '@/lib/to-number';

export function buildAutoDescription(reasonName: string | null | undefined, partnerName: string | null | undefined): string {
  const reason = (reasonName ?? '').trim();
  if (!reason) return '';
  const partner = (partnerName ?? '').trim();
  return partner ? `${reason} - ${partner}` : reason;
}

// A field follows the auto text while it is empty or still holds the previous auto text.
export function followAuto(current: string | undefined, prevAuto: string, nextAuto: string): string {
  const value = current ?? '';
  return value.trim() === '' || value === prevAuto ? nextAuto : value;
}

export function followHeaderInLines<T extends { description?: string }>(
  lines: readonly T[], oldDescription: string, newDescription: string,
): { lines: T[]; changed: boolean } {
  if (oldDescription === newDescription) return { lines: lines as T[], changed: false };
  let changed = false;
  const next = lines.map((line) => {
    const value = line.description ?? '';
    if ((value.trim() === '' || value === oldDescription) && value !== newDescription) {
      changed = true;
      return { ...line, description: newDescription };
    }
    return line;
  });
  return changed ? { lines: next, changed } : { lines: lines as T[], changed: false };
}

export function isUntouchedLine(line: { description?: string; amount?: number | string }, headerDescription: string): boolean {
  const description = (line.description ?? '').trim();
  return toNumberOrZero(line.amount) === 0 && (description === '' || description === headerDescription.trim());
}
