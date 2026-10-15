const L = require('./lib');
const toastLis = (page) => page.evaluate(() => [...document.querySelectorAll('ol li[data-state]')].filter(l=>l.closest('ol')).map(l => { const r = l.getBoundingClientRect(); return { text: l.innerText.replace(/\s+/g,' '), state: l.dataset.state, x: r.x, y: r.y, w: r.width, h: r.height, cx: r.x + r.width/2 }; }));
const barCheck = (page) => page.evaluate(() => {
  const bar = document.querySelector('div.sticky.top-0'); const res = [];
  for (const b of bar.querySelectorAll('button, a')) {
    const r = b.getBoundingClientRect(); if (!r.width || !r.height) continue;
    const cx = r.x + r.width/2, cy = r.y + r.height/2; const el = document.elementFromPoint(cx, cy);
    res.push({ label: (b.getAttribute('aria-label') || b.innerText || b.title || '').trim().replace(/\s+/g,' '), disabled: b.disabled || false, rect: [r.x, r.y, r.width, r.height].map(Math.round), reaches: !!el && (el === b || b.contains(el)), hit: el ? el.tagName + '.' + (el.className||'').toString().slice(0,40) : null });
  }
  return res;
});
(async () => {
  const out = {}; const b = await L.launch(); const ctx = await L.newCtx(b); const page = await ctx.newPage();
  page.on('console', m => { if (m.type()==='error') console.log('console.error', m.text().slice(0,150)); });
  await L.login(page);
  await page.goto(L.APP + '/receipts/new');
  await page.locator('#cash-partner').waitFor();
  await page.locator('#cash-partner').fill('KH0001'); await page.waitForTimeout(800); await page.keyboard.press('Enter');
  await page.waitForTimeout(800);
  await page.locator('#cash-line-amount-0').fill('150000');
  await page.waitForTimeout(300);
  let putCount = 0; let put400 = false;
  await ctx.route('**/api/cash-vouchers/*', async (route) => {
    const rq = route.request();
    if (rq.method() === 'PUT') { putCount++; if (put400) { put400 = false; return route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ success: false, error: { code: 'VALIDATION', message: 'Dữ liệu không hợp lệ (giả lập)' } }) }); } }
    const u = new URL(rq.url()); return route.fallback();
  });
  await page.getByRole('button', { name: 'Lưu tạm' }).click();
  await page.waitForSelector('ol li[data-state=open]', { timeout: 5000 });
  await page.waitForURL(/\/receipts\/[0-9a-f-]{36}/);
  await page.waitForTimeout(500);
  const t0 = Date.now();
  await page.screenshot({ path: L.OUT + '/s1-toast.png' });
  const lis = await toastLis(page); const bar = await barCheck(page);
  const vp = page.viewportSize();
  out.s1 = { lis, bar, vp, msSinceToast: Date.now() - t0 };
  console.log(JSON.stringify(out.s1, null, 1));
  // click Cập nhật while toast shows
  const upd = page.getByRole('button', { name: 'Cập nhật' });
  // dirty the form? Cập nhật is enabled when clean? record
  out.s1.updDisabled = await upd.isDisabled();
  let reqSeen = false; page.on('request', r => { if (r.method()==='PUT' && r.url().includes('cash-vouchers')) reqSeen = true; });
  const stillToast = (await toastLis(page)).length;
  await upd.click({ timeout: 2000 }).catch(e => { out.s1.clickErr = e.message.slice(0,200); });
  await page.waitForTimeout(600);
  out.s1.clickReachedPut = reqSeen; out.s1.toastsAtClick = stillToast;
  console.log('upd disabled', out.s1.updDisabled, 'put seen', reqSeen, 'clickErr', out.s1.clickErr);
  // S2: wait for toast to expire, then force 400 once and click again while first... need two toasts
  await page.waitForTimeout(5000);
  console.log('toasts after wait', (await toastLis(page)).length);
  // trigger a success toast (Cập nhật succeeds) then immediately a failing one
  put400 = false; reqSeen = false;
  await upd.click(); await page.waitForSelector('ol li[data-state=open]'); // success toast
  put400 = true;
  await upd.click({ timeout: 3000 }); // second click => 400 => second toast
  await page.waitForFunction(() => document.querySelectorAll('ol li[data-state=open]').length >= 2, null, { timeout: 4000 }).catch(()=>{});
  await page.waitForTimeout(300);
  const lis2 = await toastLis(page); const bar2 = await barCheck(page);
  await page.screenshot({ path: L.OUT + '/s2-two-toasts.png' });
  out.s2 = { lis: lis2, bar: bar2 };
  console.log(JSON.stringify(out.s2, null, 1));
  L.assertNo5050();
  L.fs.writeFileSync(L.OUT + '/results-s12.json', JSON.stringify({ ...out, requests: L.reqLog }, null, 1));
  await b.close();
})().catch(e => { console.error(e); process.exit(1); });
