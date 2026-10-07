// TS port of the backend DocumentNumberFormatter.Format (D13), used for the live preview.
// {STT} is zero-padded to `length` and never truncated. Replacer functions keep `$` in the prefix literal.
export function formatDocumentNumber(
  pattern: string,
  prefix: string,
  length: number,
  counter: number,
  date: Date,
): string {
  const year = String(date.getFullYear()).padStart(4, '0');
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const stt = String(counter).padStart(length, '0');
  return pattern
    .replaceAll('{KH}', () => prefix)
    .replaceAll('{NAM}', () => year)
    .replaceAll('{THANG}', () => month)
    .replaceAll('{STT}', () => stt);
}
