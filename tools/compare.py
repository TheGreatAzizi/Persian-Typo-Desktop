import io
import json
import os

base = os.path.dirname(__file__)

with io.open(os.path.join(base, 'js_results.json'), encoding='utf-8') as f:
    js = json.load(f)
with io.open(os.path.join(base, 'py_results.json'), encoding='utf-8') as f:
    py = json.load(f)

out = []
diffs = 0

for i, (a, b) in enumerate(zip(js['texts'], py['texts'])):
    if a['out'] != b['out']:
        diffs += 1
        out.append('TEXT #%d' % i)
        out.append('  IN : ' + repr(a['in']))
        out.append('  JS : ' + repr(a['out']))
        out.append('  PY : ' + repr(b['out']))

for i, (a, b) in enumerate(zip(js['typing'], py['typing'])):
    if a['out'] != b['out'] or a['outCursor'] != b['outCursor']:
        diffs += 1
        out.append('TYPING #%d cursor=%s' % (i, a['cursor']))
        out.append('  IN : ' + repr(a['in']))
        out.append('  JS : ' + repr(a['out']) + ' cursor=' + str(a['outCursor']))
        out.append('  PY : ' + repr(b['out']) + ' cursor=' + str(b['outCursor']))

header = 'DIFFS: %d\n\n' % diffs
with io.open(os.path.join(base, 'compare_result.txt'), 'w', encoding='utf-8') as f:
    f.write(header + '\n'.join(out) + '\n')

print('diffs:', diffs)
