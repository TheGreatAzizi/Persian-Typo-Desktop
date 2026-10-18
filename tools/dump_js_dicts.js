const fs = require('fs');
const path = require('path');

const src = fs.readFileSync(path.join(__dirname, '..', '..', 'repo', 'fixes.js'), 'utf8');

function extractArray(name) {
  const start = src.indexOf(`const ${name} = [`);
  if (start === -1) throw new Error('not found: ' + name);
  let i = src.indexOf('[', start);
  let depth = 0, end = -1;
  for (let j = i; j < src.length; j++) {
    if (src[j] === '[') depth++;
    else if (src[j] === ']') { depth--; if (depth === 0) { end = j; break; } }
  }
  const block = src.slice(i, end + 1);
  const pairs = [];
  const re = /\[\s*'((?:[^'\\]|\\.)*)'\s*,\s*'((?:[^'\\]|\\.)*)'\s*\]/g;
  let m;
  while ((m = re.exec(block)) !== null) {
    const unesc = s => s.replace(/\\'/g, "'").replace(/\\\\/g, '\\').replace(/\\u200C/g, '\u200C');
    pairs.push([unesc(m[1]), unesc(m[2])]);
  }
  return pairs;
}

function extractSet(name) {
  const start = src.indexOf(`const ${name} = new Set([`);
  let i = src.indexOf('[', start);
  let depth = 0, end = -1;
  for (let j = i; j < src.length; j++) {
    if (src[j] === '[') depth++;
    else if (src[j] === ']') { depth--; if (depth === 0) { end = j; break; } }
  }
  const block = src.slice(i, end + 1);
  const items = [];
  const re = /'((?:[^'\\]|\\.)*)'/g;
  let m;
  while ((m = re.exec(block)) !== null) items.push(m[1]);
  return items;
}

const out = {
  PTF_COMPOUNDS: extractArray('PTF_COMPOUNDS'),
  PTF_COMMON_TYPOS: extractArray('PTF_COMMON_TYPOS'),
  PTF_STEM_FIXES: extractArray('PTF_STEM_FIXES'),
  PTF_SUB_FIXES: extractArray('PTF_SUB_FIXES'),
  PTF_ATTACHED_FIXES: extractArray('PTF_ATTACHED_FIXES'),
  ATTACHED_HA_EXCEPTIONS: extractSet('ATTACHED_HA_EXCEPTIONS'),
  MI_ATTACHED_EXCEPTIONS: extractSet('MI_ATTACHED_EXCEPTIONS'),
  NEMI_ATTACHED_EXCEPTIONS: extractSet('NEMI_ATTACHED_EXCEPTIONS'),
};

fs.writeFileSync(path.join(__dirname, 'js_dicts.json'), JSON.stringify(out, null, 1), 'utf8');
console.log('COMPOUNDS:', out.PTF_COMPOUNDS.length);
console.log('TYPOS:', out.PTF_COMMON_TYPOS.length);
console.log('STEMS:', out.PTF_STEM_FIXES.length);
console.log('SUBS:', out.PTF_SUB_FIXES.length);
console.log('ATTACHED:', out.PTF_ATTACHED_FIXES.length);
console.log('HA_SET:', out.ATTACHED_HA_EXCEPTIONS.length);
console.log('MI_SET:', out.MI_ATTACHED_EXCEPTIONS.length);
console.log('NEMI_SET:', out.NEMI_ATTACHED_EXCEPTIONS.length);
