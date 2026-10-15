const L = require('./lib');
(async () => {
  const b = await L.launch(); const ctx = await L.newCtx(b); const page = await ctx.newPage();
  await L.login(page);
  await page.goto(L.APP + '/receipts/new');
  await page.locator('#cash-partner').waitFor();
  await page.locator('#cash-partner').fill('Khách Mới Thử'); await page.waitForTimeout(700);
  await page.keyboard.press('F4');
  const dlg = page.getByRole('dialog'); await dlg.waitFor();
  await ctx.route('**/api/customers', (route) => route.request().method() === 'POST'
    ? route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ success: false, error: { code: 'VALIDATION', message: 'Mã số thuế không hợp lệ (giả lập)' } }) })
    : route.fallback());
  const btns = await dlg.locator('button').evaluateAll(bs => bs.map(b => ({ t: (b.getAttribute('aria-label')||b.innerText||'').trim(), type: b.type })));
  console.log('dialog buttons', JSON.stringify(btns));
  const name = await dlg.locator('#name').inputValue(); console.log('name prefilled:', name);
  const save = dlg.getByRole('button', { name: /Lưu|Tạo|Thêm/ }).last();
  await save.click();
  await page.waitForSelector('ol li[data-state=open]', { timeout: 4000 });
  await page.waitForTimeout(500);
  await page.screenshot({ path: L.OUT + '/s3-dialog-as-opened.png' });
  await save.scrollIntoViewIfNeeded(); await dlg.evaluate(d => { for (const e of [d, ...d.querySelectorAll('*')]) if (e.scrollHeight > e.clientHeight + 2 && getComputedStyle(e).overflowY !== 'visible') e.scrollTop = e.scrollHeight; }); await page.waitForTimeout(300);
  await page.screenshot({ path: L.OUT + '/s3-dialog.png' });
  const m = await page.evaluate(() => {
    const rect = (e) => { const r = e.getBoundingClientRect(); return [r.x, r.y, r.width, r.height].map(v => Math.round(v*10)/10); };
    const d = document.querySelector('[role=dialog]'); const li = document.querySelector('ol li[data-state=open]');
    const tr = li.getBoundingClientRect();
    const buttons = [...d.querySelectorAll('button')].map(b => {
      const r = b.getBoundingClientRect(); const cx = r.x + r.width/2, cy = r.y + r.height/2; const el = document.elementFromPoint(cx, cy);
      const overlapsToast = !(r.right < tr.left || r.left > tr.right || r.bottom < tr.top || r.top > tr.bottom);
      return { label: (b.getAttribute('aria-label')||b.innerText||'').trim().replace(/\s+/g,' '), rect: rect(b), center: [Math.round(cx), Math.round(cy)], reaches: !!el && (el===b||b.contains(el)), hitInToast: !!el && !!el.closest('ol li'), overlapsToast, disabled: b.disabled };
    });
    return { dialog: rect(d), toast: rect(li), toastText: li.innerText.replace(/\s+/g,' '), buttons, vp: [innerWidth, innerHeight] };
  });
  console.log(JSON.stringify(m, null, 1));
  L.assertNo5050();
  L.fs.writeFileSync(L.OUT + '/results-s3.json', JSON.stringify({ ...m, requests: L.reqLog }, null, 1));
  await b.close();
})().catch(e => { console.error(e); process.exit(1); });
