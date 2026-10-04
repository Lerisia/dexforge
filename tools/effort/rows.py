# One review row per classification target: the final evolution (or the plain name whose sets a cosmetic form borrows),
# or, for a pre-evolution with several final forms, the family that must choose.
import json, csv, collections
exec(open('classify.py').read().split("rows = list(csv.DictReader")[0])   # the helpers only
rows = list(csv.DictReader(open('dex.tsv'), delimiter='\t'))
BASE = {}
for r in rows:
    if r['form'] == '0': BASE[r['showdown']] = r['stats']
    for f in (r['finals'].split('|') if r['finals'] else []):
        sp, fo, name, st = f.split(':')
        if fo == '0': BASE[name] = st
KO_OF = {r['showdown']: r['ko'].replace('(노란 )', '(노란 꽃)').replace('(파란 )', '(파란 꽃)').replace('(하얀 )', '(하얀 꽃)').replace('(오렌지)', '(오렌지색 꽃)') for r in rows}
KO_OF['Minior'] = '메테노'
SF = {r['showdown']: f"s{r['species']}f{r['form']}" for r in rows}; SF['Minior'] = 's774f7'
STATS_OF = {r['showdown']: r['stats'] for r in rows}
for r in rows:
    for f in (r['finals'].split('|') if r['finals'] else []):
        sp, fo, name, st = f.split(':'); STATS_OF.setdefault(name, st)
TIERKO = {'ubers': 'Ubers', 'ou': 'OU', 'uu': 'UU', 'ru': 'RU', 'nu': 'NU', 'pu': 'PU', 'zu': 'ZU', 'monotype': '모노타입', '1v1': '1v1', 'battlespotsingles': '배틀스팟 싱글', 'anythinggoes': 'AG'}

def basis_name(sd, stats):
    """The Showdown name whose sets decide this form: its own, or the plain species' when the form is cosmetic."""
    if sd.startswith('Minior-'): return 'Minior'
    if d.get(sd) and any(k in TIER for k in d[sd]): return sd
    plain = sd.split('-')[0]
    if '-' in sd and BASE.get(plain) == stats and d.get(plain): return plain
    return sd

def verdict(name, stats):
    f = d.get(name) or {}
    votes = collections.Counter(); first = None; n = 0; tiers = []
    for fm in sorted((k for k in f if k in TIER), key=lambda k: TIER[k]):
        for sname, s in f[fm].items():
            c = class_of_set(s.get('evs') or {})
            if c is None: continue
            n += 1
            if fm not in tiers: tiers.append(fm)
            if first is None: first = c
            votes[c] += 1
    if votes.get('support'): votes['tank'] += votes.pop('support')
    if n == 0:
        return by_stats([int(x) for x in stats.split('/')]), '세트 없음 — 종족값으로', {}, False
    best = max(votes.items(), key=lambda kv: (kv[1], kv[0] == first))[0]
    vals = sorted(votes.values(), reverse=True)
    close = len(vals) > 1 and vals[0] - vals[1] <= 1
    how = '·'.join(TIERKO[t] for t in tiers) + f' {n}세트: ' + ' · '.join(f'{KO[k]} {v}' for k, v in votes.most_common())
    return best, how, dict(votes), close

targets = {}   # basis name -> row
branches = {}  # candidate set -> row
for r in rows:
    if r['event'] == '1': continue
    finals = [f.split(':') for f in r['finals'].split('|')] if r['finals'] else []
    sp, g = int(r['species']), r['gender']
    if sp in (280, 281) and g == '1': finals = [f for f in finals if f[2] == 'Gardevoir']
    if sp == 361 and g == '0': finals = [f for f in finals if f[2] == 'Glalie']
    if sp == 412: finals = [f for f in finals if f[2].startswith('Mothim' if g == '0' else 'Wormadam')]
    if sp == 677: finals = [f for f in finals if f[2] == ('Meowstic' if g == '0' else 'Meowstic-F')]
    cands = [(f[2], f[3]) for f in finals] if finals else [(r['showdown'], r['stats'])]
    bases = []
    for name, st in cands:
        b = basis_name(name, st)
        if b not in targets:
            c, how, votes, close = verdict(b, STATS_OF.get(b, st))
            targets[b] = dict(id=SF.get(b, b), sd=b, name=KO_OF.get(b, b), stats=STATS_OF.get(b, st), cls=c, how=how, votes=votes, close=close, members=[])
        bases.append(b)
    bases = sorted(dict.fromkeys(bases), key=lambda b: int(targets[b]['id'][1:].split('f')[0]))   # the lowest dex number first: the default to follow
    if len(bases) == 1:
        targets[bases[0]]['members'].append(KO_OF[r['showdown']])
    else:
        key = '|'.join(bases)
        if key not in branches:
            branches[key] = dict(id=f"b{r['species']}", sd=key, name='', stats=r['stats'], cls=targets[bases[0]]['cls'], how=('분기 진화 — 어느 쪽을 따를지' if len({targets[b]['cls'] for b in bases}) > 1 else '분기 진화 — 양쪽이 같음'), votes={}, close=False, members=[], cands=[dict(id=targets[b]['id'], name=targets[b]['name'], cls=targets[b]['cls']) for b in bases], branch=len({targets[b]['cls'] for b in bases}) > 1)
        branches[key]['members'].append(KO_OF[r['showdown']])
for b in branches.values(): b['name'] = '·'.join(b['members']); b['members'] = []
out = [b for b in branches.values() if b['branch']] + [b for b in branches.values() if not b['branch']] + list(targets.values())
json.dump(out, open('rows.json', 'w'), ensure_ascii=False)
print(len(targets), '대상', len(branches), '분기', sum(1 for t in targets.values() if t['close']), '접전', sum(1 for t in targets.values() if '세트 없음' in t['how']), '세트 없음')
print(collections.Counter(t['cls'] for t in targets.values()))
for b in branches.values(): print(b['name'], [(c['name'], KO[c['cls']]) for c in b['cands']])
print([ (t['name'], t['members']) for t in targets.values() if t['id'] in ('Vivillon', 'Minior', 'Florges', 'Unown')])
