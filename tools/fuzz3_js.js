const fs = require('fs');
const path = require('path');
const fixes = require('../../repo/fixes.js');

function mulberry32(a) {
  return function () {
    a |= 0; a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
const rand = mulberry32(777);

const words = ['سلام', 'کتاب', 'خانه', 'می', 'نمی', 'بی', 'به', 'تر', 'ترین', 'ها', 'های', 'هایی',
  'مي', 'كتاب', 'علي', 'كجاست', 'انشاالله', 'میخواستم', 'نمازگذار', 'تاثیر', 'سوال', 'لطفا', 'روز', 'نامه',
  'دانش', 'آموز', 'همینطور', 'درمورد', 'حیات', 'خلوت', 'بزار', 'میدان', 'میز', 'تنها', 'رهایی', 'شما', 'همه',
  'test', 'example', 'com', 'http', 'www', 'email', 'a@b.cd', '123', '۱۲۳', '٠١٢',
  '....', '؟؟', ',,', '؛؛', '!!', '»', '«', '"', '\'', '(', ')', '[', ']', '-', '\u200C', ' ', '  ', '\n',
  'مُحَمَّد', 'لطفاً', 'ســـلام', 'ی', 'ي', 'ك', 'ک', 'أ', 'إ', 'ؤ', 'ة', 'ه', 'اً', 'خانهام', 'میشود', 'میرود'];

function gen(rng) {
  const n = 1 + Math.floor(rng() * 15);
  let s = '';
  for (let i = 0; i < n; i++) {
    s += words[Math.floor(rng() * words.length)];
    if (rng() > 0.3) s += ' ';
  }
  return s;
}

const boolKeys = ['fixArabicYK', 'fixArabicAlef', 'fixTashkeel', 'fixKashida', 'fixZWNJ', 'fixAttachedHa',
  'fixCompounds', 'fixCommonTypos', 'fixPunctuation', 'fixPersianPunct', 'fixDoubleSpace', 'fixArabicDigits',
  'fixLatinDigits', 'fixSeparators', 'fixRepeat', 'fixQuotes'];

const customPool = [
  { from: 'خانه', to: 'خونه', on: true },
  { from: 'کتاب', to: 'book', on: true },
  { from: 'kh', to: 'خوبی', on: true },
  { from: 'خوبی', to: 'عالی', on: true },
  { from: 'روز', to: 'رووز', on: false },
  { from: 'دلار', to: '$دلار', on: true },
  { from: 'a', to: 'ا', on: true },
];

const cases = [];
for (let i = 0; i < 3000; i++) {
  const opts = Object.assign({}, fixes.PTF_DEFAULTS);
  for (const k of boolKeys) {
    opts[k] = rand() > 0.35;
  }
  const nCustom = Math.floor(rand() * 3);
  const cw = [];
  for (let j = 0; j < nCustom; j++) {
    cw.push(customPool[Math.floor(rand() * customPool.length)]);
  }
  opts.customWords = cw;
  cases.push({ value: gen(rand), opts });
}

const results = [];
for (const c of cases) {
  let out;
  try { out = fixes.ptfFixText(c.value, c.opts); } catch (e) { out = 'ERROR: ' + e.message; }
  results.push(out);
}

fs.writeFileSync(path.join(__dirname, 'fuzz3_js.json'), JSON.stringify({ cases, results }, null, 1), 'utf8');
console.log('fuzz3 cases:', cases.length);
