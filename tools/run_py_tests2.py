import io
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))
from core.fixer import ptfFixText, PTF_DEFAULTS

with io.open(os.path.join(os.path.dirname(__file__), 'corpus2.json'), encoding='utf-8') as f:
    corpus = json.load(f)

results = []

for oi, opt_override in enumerate(corpus['options']):
    opts = dict(PTF_DEFAULTS)
    opts.update(opt_override)
    for t in corpus['texts']:
        try:
            out = ptfFixText(t, opts)
        except Exception as e:
            out = 'ERROR: ' + repr(e)
        results.append({'optIndex': oi, 'in': t, 'out': out})

with io.open(os.path.join(os.path.dirname(__file__), 'py_results2.json'), 'w', encoding='utf-8') as f:
    f.write(json.dumps(results, ensure_ascii=False, indent=1))

print('cases:', len(results))
