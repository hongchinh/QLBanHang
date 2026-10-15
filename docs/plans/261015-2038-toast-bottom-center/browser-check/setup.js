const L = require('./lib');
(async () => {
  const b = await L.launch(); const ctx = await L.newCtx(b); const page = await ctx.newPage();
  // real API through the browser context's request (direct to :5056)
  const r = await ctx.request.post(L.API + '/api/auth/login', { data: { username: 'admin', password: 'Admin@123' } });
  console.log('login', r.status()); const j = await r.json(); console.log(Object.keys(j)); const tok = j.accessToken || j.token || j.data?.accessToken;
  const c = await ctx.request.post(L.API + '/api/customers', { headers: { Authorization: 'Bearer ' + tok }, data: { code: 'KH0001', name: 'Công ty TNHH Xây dựng An Phát', group: 'Company', isCustomer: true } });
  console.log('customer', c.status(), (await c.text()).slice(0, 200));
  await b.close();
})();
