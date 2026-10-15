export const revenueReportKeys = {
  all: ['reports', 'revenue'] as const,
  summary: (p: object) => ['reports', 'revenue', 'summary', p] as const,
  series: (p: object) => ['reports', 'revenue', 'series', p] as const,
  topCustomers: (p: object) => ['reports', 'revenue', 'top-customers', p] as const,
};
