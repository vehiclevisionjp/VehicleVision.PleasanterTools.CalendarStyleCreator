// 検証用の本体に期限付きテーブルとレコードを画面から作る。空の DB で一度だけ実行し、SiteId を e2e-ids.json に書く。
// 日付は固定（実際の日付に依存しない。検証の「今日」は 2026-10-16 で、lib.mjs が固定する）。
// レコードは 2026 年 10〜11 月（祝日 10/12・11/3・11/23、検証用の土曜祝日 10/31、日曜祝日 10/25）に置く。
import fs from 'node:fs';
import { open, createSite, createRecord } from './lib.mjs';

const { browser, page } = await open();
const ids = { table: await createSite(page, 0, 'Time limited table', '期限付き') };
const records = [
  ['祝日の予定', '10/12/2026', '10/12/2026'],
  ['週末の作業', '10/17/2026', '10/18/2026'],
  ['今日の予定', '10/16/2026', '10/16/2026'],
  ['土曜祝日の予定', '10/31/2026', '10/31/2026'],
  ['月またぎの予定', '10/26/2026', '11/04/2026']
];
for (const [title, from, to] of records) await createRecord(page, ids.table, { Issues_Title: title, Issues_StartTime: from, Issues_CompletionTime: to });
fs.writeFileSync('e2e-ids.json', JSON.stringify(ids, null, 2));
console.log(JSON.stringify(ids));
await browser.close();
