export type Tt133Report =
  | 'cash-book'
  | 'debt-ledger'
  | 'debt-balance'
  | 'debt-reconciliation'
  | 'stock-summary'
  | 'stock-detail'
  | 'stock-card';

export type ExportFormat = 'xlsx' | 'pdf';

// One downloadable form: `report` picks the route, `params` the screen report's parameters,
// `fileBase` the file name without extension (the server sends the same name).
export interface Tt133ExportForm {
  code: string;
  report: Tt133Report;
  params: Record<string, unknown>;
  fileBase: string;
}

export function tt133FileBase(code: string, from: string, to: string) {
  return `${code}_${from.replace(/-/g, '')}_${to.replace(/-/g, '')}`;
}
