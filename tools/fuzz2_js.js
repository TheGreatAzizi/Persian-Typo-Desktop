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
const rand = mulberry32(1337);

const words = ['سلام', 'کتاب', 'خانه', 'می', 'نمی', 'بی', 'به', 'تر', 'ترین', 'ها', 'های', 'هایی',
  'مي', 'كتاب', 'علي', 'كجاست', 'انشاالله', 'میخواستم', 'نمازگذار', 'تاثیر', 'سوال', 'لطفا', 'روز', 'نامه',
  'دانش', 'آموز', 'همینطور', 'درمورد', 'حیات', 'خلوت', 'بزار', 'میدان', 'میز', 'تنها', 'رهایی', 'شما', 'همه',
  'test', 'example', 'com', 'http', 'www', 'email', 'a@b.cd', '123', '۱۲۳', '٠١٢',
  '....', '؟؟', ',,', '؛؛', '!!', '»', '«', '"', '\'', '(', ')', '[', ']', '-', '\u200C', ' ', '  ', '\n',
  'مُحَمَّد', 'لطفاً', 'ســـلام', 'ی', 'ي', 'ك', 'ک', 'أ', 'إ', 'ؤ', 'ة', 'ه', 'اً', 'خانهام', 'میشود', 'می\u200Cرود'];

function gen(rng) {
  const n = 1 + Math.floor(rng() * 15);
  let s = '';
  for (let i = 0; i < n; i++) {
    s += words[Math.floor(rng() * words.length)];
    if (rng() > 0.3) s += ' ';
  }
  return s;
}

const textCases = [];
for (let i = 0; i < 5000; i++) textCases.push(gen(rand));

const textResults = [];
for (const t of textCases) {
  let out;
  try { out = fixes.ptfFixText(t, fixes.PTF_DEFAULTS); } catch (e) { out = 'ERROR: ' + e.message; }
  textResults.push(out);
}

const typingCases = [];
for (let i = 0; i < 3000; i++) {
  const v = gen(rand);
  const cursor = Math.floor(rand() * (v.length + 1));
  typingCases.push({ value: v, cursor });
}

const typingResults = [];
for (const tc of typingCases) {
  let res;
  try { res = fixes.ptfFixTypingValue(tc.value, tc.cursor, fixes.PTF_DEFAULTS); } catch (e) { res = { text: 'ERROR: ' + e.message, cursor: -1 }; }
  typingResults.push({ text: res.text, cursor: res.cursor });
}

fs.writeFileSync(path.join(__dirname, 'fuzz2_js.json'), JSON.stringify({ textCases, textResults, typingCases, typingResults }, null, 1), 'utf8');
console.log('text:', textCases.length, 'typing:', typingCases.length);
