import io
import json
import os

base = os.path.dirname(__file__)

with io.open(os.path.join(base, 'js_results2.json'), encoding='utf-8') as f:
    js = json.load(f)
with io.open(os.path.join(base, 'py_results2.json'), encoding='utf-8') as f:
    py = json.load(f)

out = []
diffs = 0

for i, (a, b) in enumerate(zip(js, py)):
    if a['out'] != b['out']:
        diffs += 1
        out.append('CASE #%d (opt %s)' % (i, a['optIndex']))
        out.append('  IN : ' + repr(a['in']))
        out.append('  JS : ' + repr(a['out']))
        out.append('  PY : ' + repr(b['out']))

header = 'DIFFS: %d\n\n' % diffs
with io.open(os.path.join(base, 'compare_result2.txt'), 'w', encoding='utf-8') as f:
    f.write(header + '\n'.join(out) + '\n')

print('diffs:', diffs)
