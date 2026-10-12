// Root of every branch-scoped cash and debt query (P28): cash and stock voucher mutations
// invalidate ['cash'] (and ['inventory']).
export const cashKeys = { all: ['cash'] as const };
