// Extensions API で拡張スタイルを登録・更新できるかを確認する。
// 前提: prepare.ps1 で Administrator を PrivilegedUsers にした本体。テナント管理で AllowExtensionsApi を有効にし、
// ユーザーの API キーを作成して /api/extensions/create・update を呼ぶ。登録した拡張は本体の起動時に読まれるため、
// 画面への反映は再起動後（このスクリプトは登録と応答の確認まで。反映は手順書の続きで確認する）。
import fs from 'node:fs';
import { open, base } from './lib.mjs';

const { browser, page } = await open();
const update = () => page.getByRole('button', { name: 'Update' }).last().click();

// テナント管理（/tenants/1/edit）で「拡張機能 API を許可」を有効にする。
await page.goto(`${base}/tenants/1/edit`);
await page.waitForTimeout(1500);
const box = page.locator('#Tenants_AllowExtensionsApi');
console.error('AllowExtensionsApi checkbox present:', await box.count());
// 折りたたまれた見出しの中にあって操作できないため、値を直接変えて change を通知する。
await page.evaluate(() => { const c = document.getElementById('Tenants_AllowExtensionsApi'); c.checked = true; c.dispatchEvent(new Event('change', { bubbles: true })); });
await update();
await page.waitForTimeout(2500);

// ユーザー（Administrator = 1）の API キーを作る。
await page.goto(`${base}/users/editapi`);
await page.waitForTimeout(1500);
const apiKeyButton = page.locator('#CreateApiKey');
console.error('API key button present:', await apiKeyButton.count());
if (await apiKeyButton.count()) { await apiKeyButton.click(); await page.waitForTimeout(2500); }
const apiKey = await page.evaluate(() => document.getElementById('ApiKey')?.innerText?.trim() || document.getElementById('ApiKey')?.value || '');
console.error('API key length:', apiKey.length);
// register の検証で使えるよう、作業用フォルダ（リポジトリの管理外）に書く。画面や記録には出さない。
fs.writeFileSync('e2e-apikey.txt', apiKey);

const call = async (url, body) => {
  const res = await fetch(base + url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ApiVersion: 1.1, ApiKey: apiKey, ...body }) });
  return { status: res.status, body: (await res.text()).slice(0, 300) };
};
const out = {};
out.create = await call('/api/extensions/create', { ExtensionType: 'Style', ExtensionName: 'CalendarHolidaysApi', Body: '/* E2E API */\n.e2e-api-test { color: red; }', ExtensionSettings: { Name: 'CalendarHolidaysApi', Description: 'E2E' } });
const id = JSON.parse(out.create.body || '{}').Id;
out.get = id ? await call(`/api/extensions/${id}/get`, {}) : null;
out.update = id ? await call(`/api/extensions/${id}/update`, { ExtensionType: 'Style', ExtensionName: 'CalendarHolidaysApi', Body: '/* E2E API v2 */\n.e2e-api-test { color: blue; }', ExtensionSettings: { Name: 'CalendarHolidaysApi' } }) : null;
out.delete = id ? await call(`/api/extensions/${id}/delete`, {}) : null;   // 動作確認用の拡張は消す
console.log(JSON.stringify(out, null, 1));
await browser.close();
