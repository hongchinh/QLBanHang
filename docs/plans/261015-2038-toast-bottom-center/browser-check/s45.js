const L = require('./lib');
const count = (page) => page.evaluate(() => document.querySelectorAll('ol li[data-state=open]').length);
const mk = async (ctx) => { // open new receipt, fill, save => toast
  const page = ctx.pages()[0] || await ctx.newPage(); return page; };
async function saveReceipt(page, mobile) {
  await page.goto(L.APP + '/receipts/new');
  await page.locator('#cash-partner').waitFor();
  await page.locator('#cash-partner').fill('KH0001'); await page.waitForTimeout(800); await page.keyboard.press('Enter');
  await page.waitForTimeout(800);
  await page.locator('#cash-line-amount-0').fill('250000'); await page.waitForTimeout(300);
  await page.getByRole('button', { name: 'Lưu tạm' }).click();
  await page.waitForSelector('ol li[data-state=open]', { timeout: 5000 });
}
(async () => {
  const out = {}; const b = await L.launch();
  // S4
  let ctx = await L.newCtx(b); let page = await ctx.newPage(); await L.login(page);
  await saveReceipt(page);
  const li = page.locator('ol li[data-state=open]').first();
  const box = await li.boundingBox();
  await page.mouse.move(box.x + box.width/2, box.y + box.height/2);
  const t0 = Date.now(); await page.waitForTimeout(6000);
  out.s4 = { stillOpenAfter6sHover: await count(page), hoverMs: Date.now() - t0 };
  await page.screenshot({ path: L.OUT + '/s4-hover.png' });
  await page.mouse.move(100, 300); const t1 = Date.now();
  await page.waitForFunction(() => document.querySelectorAll('ol li').length === 0, null, { timeout: 8000 }).catch(() => {});
  out.s4.closedAfterMoveAwayMs = Date.now() - t1; out.s4.countAfter = await page.evaluate(() => document.querySelectorAll('ol li').length);
  // close button
  await saveReceipt(page);
  const t2 = Date.now();
  await page.locator('ol li[data-state=open]').first().getByRole('button', { name: 'Đóng' }).click();
  await page.waitForFunction(() => document.querySelectorAll('ol li[data-state=open]').length === 0, null, { timeout: 3000 }).catch(() => {});
  out.s4.xClosedOpenCountMs = Date.now() - t2; out.s4.openAfterX = await count(page);
  await page.waitForTimeout(600); out.s4.liAfterX600ms = await page.evaluate(() => document.querySelectorAll('ol li').length);
  console.log(JSON.stringify(out.s4));
  await ctx.close();
  // S5
  ctx = await L.newCtx(b, { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true }); page = await ctx.newPage(); await L.login(page);
  await page.goto(L.APP + '/receipts/new'); await page.locator('#cash-partner').waitFor();
  await page.locator('#cash-partner').fill('KH0001'); await page.waitForTimeout(800); await page.keyboard.press('Enter'); await page.waitForTimeout(800);
  await page.locator('#cash-line-amount-0').fill('300000'); await page.waitForTimeout(300);
  const hide = page.getByRole('button', { name: 'Ẩn', exact: true }); if (await hide.count()) { await hide.first().tap(); out.s5hintDismissed = true; await page.waitForTimeout(300); }
  await page.getByRole('button', { name: 'Lưu tạm' }).tap();
  await page.waitForSelector('ol li[data-state=open]', { timeout: 5000 }); await page.waitForURL(/\/receipts\/[0-9a-f-]{36}/); await page.waitForTimeout(500);
  await page.screenshot({ path: L.OUT + '/s5-phone.png' });
  out.s5 = await page.evaluate(() => {
    const li = document.querySelector('ol li[data-state=open]'); const r = li.getBoundingClientRect(); const ol = li.closest('ol').getBoundingClientRect();
    const bar = document.querySelector('div.sticky.top-0'); const btns = [...bar.querySelectorAll('button,a')].map(b => { const q = b.getBoundingClientRect(); if (!q.width) return null; const el = document.elementFromPoint(q.x+q.width/2, q.y+q.height/2); return { label: (b.getAttribute('aria-label')||b.innerText||'').trim().replace(/\s+/g,' '), rect:[q.x,q.y,q.width,q.height].map(Math.round), reaches: !!el && (el===b||b.contains(el)) }; }).filter(Boolean);
    return { vp: [innerWidth, innerHeight], toast: [r.x, r.y, r.width, r.height], viewport: [ol.x, ol.y, ol.width, ol.height], text: li.innerText.replace(/\s+/g,' '), btns, docScrollW: document.documentElement.scrollWidth };
  });
  console.log(JSON.stringify(out.s5));
  L.assertNo5050();
  L.fs.writeFileSync(L.OUT + '/results-s45.json', JSON.stringify({ ...out, requests: L.reqLog }, null, 1));
  await b.close();
})().catch(e => { console.error(e); process.exit(1); });
