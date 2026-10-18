import io
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))
from core.fixer import ptfFixText, PTF_DEFAULTS

with io.open(os.path.join(os.path.dirname(__file__), 'fuzz_js_results.json'), encoding='utf-8') as f:
    data = json.load(f)

out_lines = []
diffs = 0
for t, js_out in zip(data['cases'], data['results']):
    try:
        py_out = ptfFixText(t, PTF_DEFAULTS)
    except Exception as e:
        py_out = 'ERROR: ' + repr(e)
    if py_out != js_out:
        diffs += 1
        if diffs <= 30:
            out_lines.append('IN : ' + repr(t))
            out_lines.append('JS : ' + repr(js_out))
            out_lines.append('PY : ' + repr(py_out))
            out_lines.append('')

with io.open(os.path.join(os.path.dirname(__file__), 'fuzz_diff.txt'), 'w', encoding='utf-8') as f:
    f.write('DIFFS: %d / %d\n\n' % (diffs, len(data['cases'])) + '\n'.join(out_lines))

print('fuzz diffs:', diffs)
