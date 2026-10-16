// Pleasanter 本体の画面を Playwright で操作する共通処理。検証用の本体だけに向けて使う。
import { chromium } from 'playwright-core';
import fs from 'node:fs';

export const base = process.env.E2E_BASE ?? 'http://127.0.0.1:58801';
const password = process.env.E2E_PASSWORD ?? 'E2e_Test_only_123!';
const stateFile = process.env.E2E_STATE ?? 'e2e-state.json';
const changedFile = stateFile + '.changed';

export async function open() {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const context = await browser.newContext({ locale: 'ja-JP', viewport: { width: 1400, height: 900 }, storageState: fs.existsSync(stateFile) ? stateFile : undefined });
  const page = await context.newPage();
  page.on('dialog', d => d.accept());
  // 本体の再起動直後は最初の要求が失敗することがあるため、数回やり直す。
  for (let i = 0; ; i++) {
    try { await page.goto(base + '/items/0/index'); break; } catch (e) { if (i >= 5) throw e; await page.waitForTimeout(3000); }
  }
  if (page.url().includes('/users/login')) {
    await page.fill('#Users_LoginId', 'Administrator');
    await page.fill('#Users_Password', fs.existsSync(changedFile) ? password : 'pleasanter');
    await page.click('#Login');
    await page.waitForTimeout(2000);
    // 初回ログインは既定パスワードの変更を求められる。
    if (await page.locator('#Users_ChangedPassword').isVisible().catch(() => false)) {
      await page.fill('#Users_ChangedPassword', password);
      await page.fill('#Users_ChangedPasswordValidator', password);
      await page.click('#ChangePassword');
      await page.waitForTimeout(2000);
      fs.writeFileSync(changedFile, '');
    }
    await context.storageState({ path: stateFile });
  }
  return { browser, page };
}

const update = page => page.getByRole('button', { name: 'Update' }).last().click();

// テンプレート（Folder / Issues など）からサイトを作り、新しい SiteId を返す。
export async function createSite(page, parentId, template, title) {
  await page.goto(`${base}/items/${parentId}/index`);
  await page.click('#NewMenuContainer');
  await page.waitForTimeout(1000);
  await page.getByText(template, { exact: true }).first().click();
  await page.waitForTimeout(800);
  await page.click('#OpenSiteTitleDialog');
  await page.fill('#SiteTitle', title);
  await page.click('#CreateByTemplate');
  await page.waitForTimeout(2500);
  return page.evaluate(t => [...document.querySelectorAll('.nav-site')].filter(e => e.innerText.trim() === t).map(e => Number(e.dataset.value)).pop(), title);
}

// サイト設定のカレンダー種別を切り替える（'1' = Standard、'2' = FullCalendar）。
export async function setCalendarType(page, siteId, type) {
  await page.goto(`${base}/items/${siteId}/edit`);
  await page.click('a[href="#CalendarSettingsEditor"]');
  await page.waitForTimeout(800);
  await page.selectOption('#CalendarType', type);
  await update(page);
  await page.waitForTimeout(2500);
}

// レコードを作る。fields は { 要素ID: 値 }。日付は本体の既定書式 MM/dd/yyyy。
export async function createRecord(page, siteId, fields) {
  await page.goto(`${base}/items/${siteId}/new`);
  await page.waitForTimeout(1200);
  for (const [id, value] of Object.entries(fields)) await page.fill('#' + id, value);
  await page.click('#CreateCommand');
  await page.waitForTimeout(2000);
}
