import json, csv, collections, sys
d = json.load(open('gen7.json'))
SINGLES = ['ubers','ou','uu','ru','nu','pu','zu','monotype','1v1','battlespotsingles','anythinggoes']
TIER = {t: i for i, t in enumerate(SINGLES)}
KO = {'tank': '탱커', 'slow-phys': '저속 물리', 'slow-spec': '저속 특수', 'fast-phys': '고속 물리', 'fast-spec': '고속 특수'}
SPREAD = {'tank': 'HP252 방252 특방6', 'slow-phys': '공252 HP252 방6', 'slow-spec': '특공252 HP252 방6', 'fast-phys': '공252 스핏252 HP6', 'fast-spec': '특공252 스핏252 HP6'}

def class_of_set(evs):
    if not evs: return None
    top = sorted(evs.items(), key=lambda kv: -kv[1])[:2]
    if len(top) < 2 or top[1][1] < 60: return None   # one-stat or odd spreads: no opinion
    s = {k for k, _ in top}
    bulk = {'hp', 'def', 'spd'}
    if s <= bulk: return 'tank'
    if s == {'hp', 'spe'}: return 'support'
    if s == {'atk', 'spa'}: return None
    off = 'phys' if 'atk' in s else 'spec'
    return ('fast-' if 'spe' in s else 'slow-') + off

def by_stats(st):
    hp, atk, de, spa, spd, spe = st
    off = 'phys' if atk >= spa else 'spec'
    top = max(('atk', atk), ('spa', spa), ('def', de), ('spd', spd), ('spe', spe), key=lambda kv: kv[1])[0]
    if top in ('def', 'spd'): return 'tank'
    if top == 'spe' or spe >= 85: return 'fast-' + off
    return 'slow-' + off

def by_smogon(name):
    f = d.get(name)
    if not f: return None, None
    votes = collections.Counter(); first = None; n = 0
    for fm in sorted((k for k in f if k in TIER), key=lambda k: TIER[k]):
        for sname, s in f[fm].items():
            c = class_of_set(s.get('evs') or {})
            if c is None: continue
            n += 1
            if first is None: first = (fm, sname, c)
            votes[c] += 1
    if n == 0: return None, None
    if votes.get('support'):
        votes['tank'] += votes.pop('support')
    best = max(votes.items(), key=lambda kv: (kv[1], kv[0] == first[2]))
    return best[0], f"{first[0]} {n}세트 {dict(votes)}"

rows = list(csv.DictReader(open('dex.tsv'), delimiter='\t'))
BASE = {}   # plain species name -> base stats of form 0, from the dump
for r in rows:
    if r['form'] == '0': BASE[r['showdown']] = r['stats']
    for f in (r['finals'].split('|') if r['finals'] else []):
        sp, fo, name, st = f.split(':')
        if fo == '0': BASE[name] = st
out = []; flagged = []
cache = {}
def classify(sd, stats):
    if sd in cache: return cache[sd]
    c, how = by_smogon(sd)
    plain = sd.split('-')[0]
    if c is None and '-' in sd and BASE.get(plain) == stats:
        c, how = by_smogon(plain)
        if c: how = plain + ' 의 ' + how
    if c is None:
        c = by_stats([int(x) for x in stats.split('/')]); how = '종족값'
    cache[sd] = (c, how); return c, how

for r in rows:
    if r['event'] == '1': continue
    finals = [f.split(':') for f in r['finals'].split('|')] if r['finals'] else []
    sp, g = int(r['species']), r['gender']   # 0 male, 1 female
    if sp in (280, 281) and g == '1': finals = [f for f in finals if f[2] == 'Gardevoir']
    if sp == 361 and g == '0': finals = [f for f in finals if f[2] == 'Glalie']
    if sp == 412: finals = [f for f in finals if f[2].startswith('Mothim' if g == '0' else 'Wormadam')]
    if sp == 677: finals = [f for f in finals if f[2] == ('Meowstic' if g == '0' else 'Meowstic-F')]
    if not finals:
        c, how = classify(r['showdown'], r['stats']); via = ''
        cs = {c}
    else:
        res = [(f[2], *classify(f[2], f[3])) for f in finals]
        cs = {x[1] for x in res}
        c, how = res[0][1], res[0][2]; via = ' / '.join(f"{x[0]}={KO[x[1]]}" for x in res)
    flag = ''
    if len(cs) > 1: flag = '분기 진화가 다름'
    elif how == '종족값': flag = '세트 없음(종족값)' + (' 공=특공' if (lambda st: st[1] == st[3])([int(x) for x in (finals[0][3] if finals else r['stats']).split('/')]) else '')
    out.append((r['box'], r['slot'], r['ko'], r['showdown'], r['stats'], KO[c], SPREAD[c], how, via, flag))
    if flag: flagged.append(out[-1])

with open('분류.tsv', 'w') as f:
    w = csv.writer(f, delimiter='\t'); w.writerow(['박스','칸','포켓몬','쇼다운','종족값','분류','노력치','근거','최종진화','확인'])
    w.writerows(out)
print(len(out), collections.Counter(o[5] for o in out))
print('근거 종족값:', sum(1 for o in out if o[7] == '종족값'), ' 분기 다름:', sum(1 for o in out if o[9] == '분기 진화가 다름'))
for o in flagged: print('\t'.join(o[2:3] + o[4:6] + o[8:10]))

agree = tot = 0; dis = []
for sd, (c, how) in cache.items():
    if how == '종족값': continue
    st = None
    for r in rows:
        if r['showdown'] == sd: st = r['stats']; break
        for f in (r['finals'].split('|') if r['finals'] else []):
            if f.split(':')[2] == sd: st = f.split(':')[3]
    if st is None: continue
    tot += 1
    b = by_stats([int(x) for x in st.split('/')])
    if b == c: agree += 1
    else: dis.append((sd, st, KO[c], KO[b]))
print('종족값 규칙과 Smogon 일치:', agree, '/', tot)
for x in dis[:40]: print('  ', x)
