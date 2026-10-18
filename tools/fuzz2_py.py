import io
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))
from core.fixer import ptfFixText, ptfFixTypingValue, PTF_DEFAULTS

with io.open(os.path.join(os.path.dirname(__file__), 'fuzz2_js.json'), encoding='utf-8') as f:
    data = json.load(f)

out_lines = []
diffs = 0

for t, js_out in zip(data['textCases'], data['textResults']):
    try:
        py_out = ptfFixText(t, PTF_DEFAULTS)
    except Exception as e:
        py_out = 'ERROR: ' + repr(e)
    if py_out != js_out:
        diffs += 1
        if len(out_lines) < 120:
            out_lines.append('TEXT IN : ' + repr(t))
            out_lines.append('TEXT JS : ' + repr(js_out))
            out_lines.append('TEXT PY : ' + repr(py_out))
            out_lines.append('')

for tc, js_res in zip(data['typingCases'], data['typingResults']):
    try:
        py_res = ptfFixTypingValue(tc['value'], tc['cursor'], PTF_DEFAULTS)
        py_res = {'text': py_res['text'], 'cursor': py_res['cursor']}
    except Exception as e:
        py_res = {'text': 'ERROR: ' + repr(e), 'cursor': -1}
    if py_res['text'] != js_res['text'] or py_res['cursor'] != js_res['cursor']:
        diffs += 1
        if len(out_lines) < 120:
            out_lines.append('TYPE IN : %r cursor=%d' % (tc['value'], tc['cursor']))
            out_lines.append('TYPE JS : %r cursor=%d' % (js_res['text'], js_res['cursor']))
            out_lines.append('TYPE PY : %r cursor=%d' % (py_res['text'], py_res['cursor']))
            out_lines.append('')

with io.open(os.path.join(os.path.dirname(__file__), 'fuzz2_diff.txt'), 'w', encoding='utf-8') as f:
    f.write('DIFFS: %d\n\n' % diffs + '\n'.join(out_lines))

print('fuzz2 diffs:', diffs)
