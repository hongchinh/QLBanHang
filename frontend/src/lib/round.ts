// Matches .NET Math.Round(x, digits, MidpointRounding.AwayFromZero): -2.5 → -3, 2.5 → 3.
// The relative epsilon absorbs binary representation error (1.005 → 1.01 at 2 digits).
export function roundAwayFromZero(value: number, digits = 0): number {
  const factor = 10 ** digits;
  const rounded = Math.round(Math.abs(value) * factor * (1 + Number.EPSILON)) / factor;
  // `|| 0` turns -0 into 0.
  return (value < 0 ? -rounded : rounded) || 0;
}
