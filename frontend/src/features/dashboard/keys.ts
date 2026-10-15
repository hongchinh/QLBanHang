export const dashboardKeys = {
  all: ['dashboard'] as const,
  quotationStats: (p?: object) => ['dashboard', 'quotation-stats', p] as const,
  summary: (p: object) => ['dashboard', 'summary', p] as const,
  revenueSeries: (p: object) => ['dashboard', 'revenue-series', p] as const,
  topCustomers: (p: object) => ['dashboard', 'top-customers', p] as const,
  topProducts: (p: object) => ['dashboard', 'top-products', p] as const,
  recentActivity: (p: object) => ['dashboard', 'recent-activity', p] as const,
  leaderboard: (p: object) => ['dashboard', 'leaderboard', p] as const,
};

// Prefixes (the second part of the key) refetched on a `sales` signal / on QuotationsChanged.
export const REVENUE_PREFIXES = ['summary', 'revenue-series', 'top-customers', 'top-products', 'leaderboard'];
export const QUOTATION_PREFIXES = ['summary', 'recent-activity'];
