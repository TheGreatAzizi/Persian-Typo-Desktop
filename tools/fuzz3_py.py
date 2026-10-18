import io
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))
from core.fixer import ptfFixText, PTF_DEFAULTS

with io.open(os.path.join(os.path.dirname(__file__), 'fuzz3_js.json'), encoding='utf-8') as f:
    data = json.load(f)

out_lines = []
diffs = 0
for c, js_out in zip(data['cases'], data['results']):
    opts = dict(PTF_DEFAULTS)
    opts.update(c['opts'])
    try:
        py_out = ptfFixText(c['value'], opts)
    except Exception as e:
        py_out = 'ERROR: ' + repr(e)
    if py_out != js_out:
        diffs += 1
        if diffs <= 20:
            out_lines.append('IN : ' + repr(c['value']))
            out_lines.append('OPTS: ' + json.dumps(c['opts'], ensure_ascii=False))
            out_lines.append('JS : ' + repr(js_out))
            out_lines.append('PY : ' + repr(py_out))
            out_lines.append('')

with io.open(os.path.join(os.path.dirname(__file__), 'fuzz3_diff.txt'), 'w', encoding='utf-8') as f:
    f.write('DIFFS: %d / %d\n\n' % (diffs, len(data['cases'])) + '\n'.join(out_lines))

print('fuzz3 diffs:', diffs)
