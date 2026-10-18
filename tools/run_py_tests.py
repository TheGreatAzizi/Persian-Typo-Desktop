import io
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))
from core.fixer import ptfFixText, ptfFixTypingValue, PTF_DEFAULTS

with io.open(os.path.join(os.path.dirname(__file__), 'corpus.json'), encoding='utf-8') as f:
    corpus = json.load(f)

results = {'texts': [], 'typing': []}

for t in corpus['texts']:
    try:
        out = ptfFixText(t, PTF_DEFAULTS)
    except Exception as e:
        out = 'ERROR: ' + repr(e)
    results['texts'].append({'in': t, 'out': out})

for item in corpus['typing']:
    try:
        res = ptfFixTypingValue(item['value'], item['cursor'], PTF_DEFAULTS)
        results['typing'].append({'in': item['value'], 'cursor': item['cursor'], 'out': res['text'], 'outCursor': res['cursor']})
    except Exception as e:
        results['typing'].append({'in': item['value'], 'cursor': item['cursor'], 'out': 'ERROR: ' + repr(e), 'outCursor': -1})

with io.open(os.path.join(os.path.dirname(__file__), 'py_results.json'), 'w', encoding='utf-8') as f:
    f.write(json.dumps(results, ensure_ascii=False, indent=1))

print('texts:', len(results['texts']), 'typing:', len(results['typing']))
