const fs = require('fs');
const path = require('path');
const fixes = require('../../repo/fixes.js');

const corpus = JSON.parse(fs.readFileSync(path.join(__dirname, 'corpus.json'), 'utf8'));

const results = { texts: [], typing: [] };

for (const t of corpus.texts) {
  let out;
  try {
    out = fixes.ptfFixText(t, fixes.PTF_DEFAULTS);
  } catch (e) {
    out = 'ERROR: ' + e.message;
  }
  results.texts.push({ in: t, out });
}

for (const item of corpus.typing) {
  let res;
  try {
    res = fixes.ptfFixTypingValue(item.value, item.cursor, fixes.PTF_DEFAULTS);
  } catch (e) {
    res = { text: 'ERROR: ' + e.message, cursor: -1 };
  }
  results.typing.push({ in: item.value, cursor: item.cursor, out: res.text, outCursor: res.cursor });
}

fs.writeFileSync(path.join(__dirname, 'js_results.json'), JSON.stringify(results, null, 1), 'utf8');
console.log('texts:', results.texts.length, 'typing:', results.typing.length);
