// 生成した拡張スタイルが標準カレンダーと FullCalendar で効いているかを DOM と getComputedStyle で確認する。
// 使い方（作業用フォルダで）: node verify.mjs <standard|full> <generate が出した .css> <スクリーンショット出力先> [ラベル]
// ラベルに sun を含めると週の開始曜日が日曜（FirstDayOfWeek=0）の期待値で見出し順を確認する。
// 結果は JSON を標準出力へ。NG が 1 件でもあれば終了コード 1。色は既定テーマ（CalendarTheme の既定値）を期待値にする。
import fs from 'node:fs';
import path from 'node:path';
import { open, base, setCalendarType } from './lib.mjs';

const [mode, cssFile, shotDir, label = ''] = process.argv.slice(2);
const siteId = JSON.parse(fs.readFileSync('e2e-ids.json', 'utf8')).table;
fs.mkdirSync(shotDir, { recursive: true });
const sundayFirst = label.includes('sun');

// 期待する色（既定テーマ）。
const HOLIDAY_BG = 'rgb(255, 240, 243)', SAT_BG = 'rgb(232, 243, 255)', HOLIDAY_TEXT = 'rgb(176, 0, 32)', SAT_TEXT = 'rgb(21, 101, 192)';
const CUSTOM_BG = [HOLIDAY_BG, SAT_BG];

// 生成した CSS から祝日（yyyy-MM-dd → 名称）を取り出す（FullCalendar の祝日名ルールが日付を持つ）。
const css = fs.readFileSync(cssFile, 'utf8').replace(/^\uFEFF/, '');
const holidays = {};
for (const m of css.matchAll(/\[data-date="(\d{4}-\d\d-\d\d)"\]:not\(\.fc-day-other\) \.fc-daygrid-day-top::after[^{]*\{[^}]*content: "([^"]*)"/g)) holidays[m[1]] = m[2];

const results = [];
const check = (name, ok, detail) => { results.push({ name, ok: !!ok, detail }); if (!ok) console.error('NG', name, JSON.stringify(detail)); };
const dow = s => new Date(s + 'T00:00:00Z').getUTCDay();   // 0 = 日曜
const none = v => !v || v === 'none' || v === 'normal';

// 日付ごとの期待（背景色・文字色・祝日名）。null は「カスタム色が付かない」。
function expectFor(dateIso) {
  const name = holidays[dateIso];
  if (name !== undefined || dow(dateIso) === 0) return { bg: HOLIDAY_BG, text: HOLIDAY_TEXT, name };
  if (dow(dateIso) === 6) return { bg: SAT_BG, text: SAT_TEXT };
  return null;
}

const { browser, page } = await open();
await setCalendarType(page, siteId, mode === 'standard' ? '1' : '2');
await page.goto(`${base}/items/${siteId}/calendar`);
await page.waitForTimeout(2500);
const shot = n => page.screenshot({ path: path.join(shotDir, `${mode}-${label}-${n}.png`) });

if (mode === 'standard') {
  // ---- 標準カレンダー ----
  const cells = () => page.evaluate(() => [...document.querySelectorAll('#CalendarBody td.container')].map(td => {
    const day = td.querySelector('.day');
    return {
      id: td.dataset.id, cls: td.className, weekly: td.classList.contains('is-weekly'), hasDay: !!day,
      bg: getComputedStyle(td).backgroundColor, color: day ? getComputedStyle(day).color : null,
      after: day ? getComputedStyle(day, '::after').content : null,
      dayWidth: day ? Math.round(day.getBoundingClientRect().width) : 0, text: day?.innerText
    };
  }));
  const iso = id => { const [y, m, d] = id.split('/').map(Number); return `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`; };
  const verifyCells = (list, view) => {
    for (const c of list) {
      const date = iso(c.id);
      const other = c.cls.includes('other-month');
      if (view === 'yearly') {
        check(`year ${c.id} no custom bg`, !CUSTOM_BG.includes(c.bg), c);
        check(`year ${c.id} no holiday label`, none(c.after), c);
        continue;
      }
      const exp = other ? null : expectFor(date);
      if (exp && view === 'weekly') {
        // 週表示のセルには日付の数字（.day）も祝日名もなく、背景色だけが対象。
        check(`${view} ${c.id} bg`, c.bg === exp.bg, { got: c.bg, want: exp.bg });
      } else if (exp) {
        check(`${view} ${c.id} bg`, c.bg === exp.bg, { got: c.bg, want: exp.bg });
        check(`${view} ${c.id} day color`, c.color === exp.text, { got: c.color, want: exp.text });
        if (exp.name) check(`${view} ${c.id} name`, c.after === `"${exp.name}"`, { got: c.after, want: exp.name });
        else check(`${view} ${c.id} no name`, none(c.after), c);
      } else {
        check(`${view} ${c.id}${other ? ' (other-month)' : ''} no custom bg`, !CUSTOM_BG.includes(c.bg), { got: c.bg });
        check(`${view} ${c.id} no custom color`, c.color !== HOLIDAY_TEXT && c.color !== SAT_TEXT, { got: c.color });
        check(`${view} ${c.id} no label`, none(c.after), { got: c.after });
      }
    }
  };
  const header = () => page.evaluate(() => [...document.querySelectorAll('#CalendarBody th')].map(e => e.className.split(' ')[0]));
  const move = async kind => { await page.evaluate(k => $p.moveCalendar(k, ''), kind); await page.waitForTimeout(1500); };

  // 表示期間は利用者ごとに保存されるため、前回の実行の影響を避けて月表示から始める。
  if ((await page.inputValue('#CalendarTimePeriod')) !== 'Monthly') { await page.selectOption('#CalendarTimePeriod', 'Monthly'); await page.waitForTimeout(2000); }
  await move('Today');
  // 月表示: 2026-10（今日 10/16。10/12 祝日、10/25 日曜祝日、10/31 土曜祝日）と 2026-11（11/3・11/23）。
  const order = await header();
  check('header order follows FirstDayOfWeek', order.join() === (sundayFirst ? 'sunday,monday,tuesday,wednesday,thursday,friday,saturday' : 'monday,tuesday,wednesday,thursday,friday,saturday,sunday'), order);
  let list = await cells();
  check('month cells have .day', list.length >= 28 && list.every(c => c.hasDay), { n: list.length });
  verifyCells(list, 'monthly');
  check('month has other-month cells', list.some(c => c.cls.includes('other-month')));
  console.error('month sample', JSON.stringify(list.filter(c => c.id === '2026/10/12' || c.id === '2026/10/31')));
  await shot('month-2026-10');
  await move('Next');
  verifyCells(await cells(), 'monthly');
  await shot('month-2026-11');
  await move('Today');
  // 年表示
  await page.selectOption('#CalendarTimePeriod', 'Yearly'); await page.waitForTimeout(2000);
  list = await cells();
  check('year cells exist', list.length >= 12, { n: list.length });
  check('year cells have no .day', list.every(c => !c.hasDay), { sample: list.slice(0, 2) });
  verifyCells(list, 'yearly');
  await shot('year-2026');
  // 週表示（今日を含む週）
  await page.selectOption('#CalendarTimePeriod', 'Weekly'); await page.waitForTimeout(2000);
  list = await cells();
  check('week cells exist', list.length === 7 && list.every(c => c.weekly), list.map(c => c.id));
  console.error('week sample', JSON.stringify(list.slice(0, 2)));
  verifyCells(list, 'weekly');
  await shot('week-2026-10-16');
  await move('Previous');
  verifyCells(await cells(), 'weekly');
  await shot('week-2026-10-09');
  // 日曜祝日（10/25）と土曜祝日（10/31）の週（開始曜日によって同じ週のこともある）
  for (const target of ['2026/10/25', '2026/10/31']) {
    for (let i = 0; i < 5 && !(await cells()).some(c => c.id === target); i++) await move('Next');
    list = await cells();
    check(`week containing ${target} reached`, list.some(c => c.id === target), list.map(c => c.id));
    verifyCells(list, 'weekly');
    await shot(`week-${target.replace(/\//g, '-')}`);
  }
} else {
  // ---- FullCalendar ----
  const sample = () => page.evaluate(() => {
    const pseudo = el => el ? getComputedStyle(el, '::after').content : null;
    return [...document.querySelectorAll('[data-date]')].map(el => {
      const number = el.querySelector('.fc-daygrid-day-number'), cushion = el.querySelector('.fc-col-header-cell-cushion'), top = el.querySelector('.fc-daygrid-day-top'), listText = el.querySelector('.fc-list-day-text');
      return {
        tag: el.tagName, cls: el.className, date: el.dataset.date, bg: getComputedStyle(el).backgroundColor,
        numberColor: number ? getComputedStyle(number).color : null,
        cushionColor: cushion ? getComputedStyle(cushion).color : null,
        topAfter: pseudo(top), cushionAfter: pseudo(cushion), listAfter: pseudo(listText),
        other: el.classList.contains('fc-day-other')
      };
    });
  });
  const verifyFc = (list, view) => {
    check(`${view} has [data-date] elements`, list.length > 0, { n: list.length });
    for (const o of list) {
      const kind = o.tag.toLowerCase() + '.' + (o.cls.split(' ').find(c => /^fc-(daygrid-day|timegrid-col|col-header-cell|list-day)$/.test(c)) ?? '?');
      const exp = o.other ? null : expectFor(o.date);
      if (o.tag === 'TR') {
        // リストの日見出しは行ではなく、見出しの a（.fc-list-day-text）に祝日名が付く。
        if (exp?.name) check(`${view} ${kind} ${o.date} list label`, o.listAfter === `"${exp.name}"`, { got: o.listAfter, want: exp.name });
        else check(`${view} ${kind} ${o.date} no list label`, none(o.listAfter), { got: o.listAfter });
        continue;
      }
      if (exp) {
        check(`${view} ${kind} ${o.date} bg`, o.bg === exp.bg, { got: o.bg, want: exp.bg });
        if (o.numberColor) check(`${view} ${kind} ${o.date} number color`, o.numberColor === exp.text, { got: o.numberColor, want: exp.text });
        if (o.tag === 'TH' && o.cushionColor) check(`${view} ${kind} ${o.date} header text color`, o.cushionColor === exp.text, { got: o.cushionColor, want: exp.text });
        if (exp.name) {
          const got = o.tag === 'TH' ? o.cushionAfter : o.topAfter;
          if (o.tag === 'TH' || (kind.includes('daygrid') && view.startsWith('month'))) check(`${view} ${kind} ${o.date} name`, got === `"${exp.name}"`, { got, want: exp.name });
        }
      } else {
        check(`${view} ${kind} ${o.date}${o.other ? ' (other)' : ''} no custom bg`, !CUSTOM_BG.includes(o.bg), { got: o.bg });
        check(`${view} ${kind} ${o.date} no label`, none(o.topAfter) && none(o.cushionAfter), { top: o.topAfter, cushion: o.cushionAfter });
      }
    }
  };
  const click = async (sel, wait = 1000) => { await page.click(sel); await page.waitForTimeout(wait); };
  // 今日の表示のときは today ボタンが無効になる。
  const today = async wait => { if (await page.isEnabled('.fc-today-button')) await click('.fc-today-button', wait); };

  // 表示は前回の状態が残るため、月表示・今日から始める。
  await click('.fc-dayGridMonth-button', 1200);
  await today(800);
  // 月表示
  verifyFc(await sample(), 'month-2026-10');
  const dom = await page.evaluate(() => ({
    cell: document.querySelector('td[data-date="2026-10-12"]').className,
    th: [...document.querySelectorAll('th.fc-col-header-cell')].map(e => e.className.split(' ').find(c => /^fc-day-(mon|tue|wed|thu|fri|sat|sun)$/.test(c)).slice(7)),
    root: document.querySelector('.fc').id + '|' + document.querySelector('.fc').className.slice(0, 60)
  }));
  console.error('DOM', JSON.stringify(dom));
  // FullCalendar の週の開始曜日は FirstDayOfWeek に従わず、常に月曜始まり（本体の FullCalendar 設定）。
  check('month header order is Monday-first regardless of FirstDayOfWeek', dom.th.join() === 'mon,tue,wed,thu,fri,sat,sun', dom.th);
  await shot('month-2026-10');
  await click('.fc-next-button', 1200);
  verifyFc(await sample(), 'month-2026-11');
  await shot('month-2026-11');
  await today(800);
  // 週表示（今日の週）
  await click('.fc-timeGridWeek-button', 1500);
  verifyFc(await sample(), 'week-2026-10-16');
  await shot('week-2026-10-16');
  // 日表示: 今日（金）→ 土 10/17 → 日 10/18 → 祝日 10/12（月）
  await click('.fc-timeGridDay-button', 1200);
  await today(800);
  verifyFc(await sample(), 'day-2026-10-16');
  await shot('day-2026-10-16');
  await click('.fc-next-button');
  verifyFc(await sample(), 'day-2026-10-17-sat'); await shot('day-2026-10-17-sat');
  await click('.fc-next-button');
  verifyFc(await sample(), 'day-2026-10-18-sun'); await shot('day-2026-10-18-sun');
  for (let i = 0; i < 6; i++) await click('.fc-prev-button', 700);
  verifyFc(await sample(), 'day-2026-10-12-holiday'); await shot('day-2026-10-12-holiday');
  // リスト表示（今月）
  await click('.fc-listMonth-button', 1200);
  await today(800);
  verifyFc(await sample(), 'list-2026-10');
  await shot('list-2026-10');
}

await browser.close();
const ng = results.filter(r => !r.ok);
console.log(JSON.stringify({ mode, label, total: results.length, ng: ng.length, failures: ng.slice(0, 40) }, null, 1));
process.exitCode = ng.length ? 1 : 0;
