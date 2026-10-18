const fs = require('fs');
const path = require('path');
const fixes = require('../../repo/fixes.js');

const corpus = JSON.parse(fs.readFileSync(path.join(__dirname, 'corpus2.json'), 'utf8'));

const results = [];

for (let oi = 0; oi < corpus.options.length; oi++) {
  const opts = Object.assign({}, fixes.PTF_DEFAULTS, corpus.options[oi]);
  for (const t of corpus.texts) {
    let out;
    try {
      out = fixes.ptfFixText(t, opts);
    } catch (e) {
      out = 'ERROR: ' + e.message;
    }
    results.push({ optIndex: oi, in: t, out });
  }
}

fs.writeFileSync(path.join(__dirname, 'js_results2.json'), JSON.stringify(results, null, 1), 'utf8');
console.log('cases:', results.length);
