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
const rand = mulberry32(42);

const words = ['سلام', 'کتاب', 'خانه', 'می', 'نمی', 'بی', 'به', 'تر', 'ترین', 'ها', 'های', 'هایی',
  'مي', 'كتاب', 'علي', 'كجاست', 'انشاالله', 'میخواستم', 'نمازگذار', 'تاثیر', 'سوال', 'لطفا', 'روز', 'نامه',
  'دانش', 'آموز', 'همینطور', 'درمورد', 'حیات', 'خلوت', 'بزار', 'میدان', 'میز', 'تنها', 'رهایی', 'شما', 'همه',
  'test', 'example', 'com', 'it', 'http', 'www', 'email', 'a@b.cd', '123', '456', '۱۲۳', '٠١٢', '0x1F600',
  '....', '؟؟', ',,', '؛؛', '!!', '????', '»', '«', '"', '\'', '(', ')', '[', ']', '-', '\u200C', ' ', '  ', '\n',
  'مُحَمَّد', 'لطفاً', 'ســـلام', 'ی', 'ي', 'ك', 'ک', 'أ', 'إ', 'ؤ', 'ة', 'ه', 'اً'];

function gen(rng) {
  const n = 1 + Math.floor(rng() * 18);
  let s = '';
  for (let i = 0; i < n; i++) {
    s += words[Math.floor(rng() * words.length)];
    if (rng() > 0.3) s += ' ';
  }
  return s;
}

const cases = [];
for (let i = 0; i < 3000; i++) cases.push(gen(rand));

const results = [];
for (const t of cases) {
  let out;
  try {
    out = fixes.ptfFixText(t, fixes.PTF_DEFAULTS);
  } catch (e) {
    out = 'ERROR: ' + e.message;
  }
  results.push(out);
}

fs.writeFileSync(path.join(__dirname, 'fuzz_js_results.json'), JSON.stringify({ cases, results }, null, 1), 'utf8');
console.log('fuzz cases:', cases.length);
