import { Navigate, Route, createRoutesFromChildren } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { appRoutes } from '@/app-routes';
import { ROUTES_WITHOUT_HELP, flattenRoutePaths } from './route-paths';

const Page = () => null;
const tree = (
  <>
    <Route path="/login" element={<Page />} />
    <Route path="/403" element={<Page />} />
    <Route element={<Page />}>
      <Route index element={<Page />} />
      <Route path="customers">
        <Route index element={<Page />} />
        <Route path="new" element={<Page />} />
        <Route path=":id" element={<Page />} />
      </Route>
      <Route path="debt/matching" element={<Page />} />
      <Route path="reports">
        <Route index element={<Navigate to="revenue" replace />} />
        <Route path="revenue" element={<Page />} />
        <Route path="sales-revenue/:saleUserId" element={<Page />} />
      </Route>
    </Route>
    <Route path="/404" element={<Page />} />
    <Route path="*" element={<Navigate to="/404" replace />} />
  </>
);

describe('flattenRoutePaths', () => {
  it('joins nested paths, maps index routes to their parent and skips layouts, redirects, * and pages without help', () => {
    expect(flattenRoutePaths(createRoutesFromChildren(tree))).toEqual([
      '/', '/customers', '/customers/:id', '/customers/new', '/debt/matching',
      '/reports/revenue', '/reports/sales-revenue/:saleUserId',
    ]);
  });
  it('reads the real app routes', () => {
    const paths = flattenRoutePaths(createRoutesFromChildren(appRoutes));
    expect(paths).toEqual(expect.arrayContaining([
      '/', '/stock-out/new', '/stock-out/:id', '/settings', '/admin/users/:userId/transfer-quotations',
    ]));
    expect(paths).not.toContain('/reports');
    for (const p of ROUTES_WITHOUT_HELP) expect(paths).not.toContain(p);
  });
});
