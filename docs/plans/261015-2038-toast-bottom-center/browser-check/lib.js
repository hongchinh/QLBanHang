const { chromium } = require('playwright-core');
const fs = require('fs');
const OUT = 'D:/Projects/QLBanHang/.claude/worktrees/toast-bottom-center/docs/plans/261015-2038-toast-bottom-center/browser-check';
const API = 'http://localhost:5056', APP = 'http://localhost:5182';
const reqLog = [];
async function newCtx(browser, opts = {}) {
  const ctx = await browser.newContext({ serviceWorkers: 'block', locale: 'vi-VN', timezoneId: 'Asia/Ho_Chi_Minh', viewport: { width: 1280, height: 768 }, ...opts });
  await ctx.route('**/api/**', (route) => {
    const u = new URL(route.request().url());
    if (u.pathname.startsWith('/hubs')) return route.abort();
    route.continue({ url: API + u.pathname + u.search });
  });
  await ctx.route('**/hubs/**', (r) => r.abort());
  await ctx.routeWebSocket(/\/hubs\//, (ws) => ws.close());
  ctx.on('request', (r) => reqLog.push(r.method() + ' ' + r.url()));
  return ctx;
}
async function login(page) {
  await page.goto(APP + '/login');
  await page.locator('input[type=password]').waitFor();
  const user = page.locator('input:not([type=password]):not([type=hidden]):not([type=checkbox])').first();
  await user.fill('admin');
  await page.locator('input[type=password]').fill('Admin@123');
  await page.keyboard.press('Enter');
  await page.waitForURL((u) => !u.pathname.startsWith('/login'), { timeout: 15000 });
}
function assertNo5050() { const bad = reqLog.filter((l) => l.includes(':5050')); if (bad.length) throw new Error('5050 hit: ' + bad[0]); }
async function launch() { return chromium.launch({ channel: 'msedge' }); }
module.exports = { OUT, API, APP, reqLog, newCtx, login, assertNo5050, launch, fs };
