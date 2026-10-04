# The effort class of every non-event Pokémon of the Sword dex: the final form it reaches in Sword, classified as the
# Ultra Sun table did where that form is in it, and from Smogon's gen 8 singles sets where it is not.
import json, csv, collections
src = open('classify.py').read().split("rows = list(csv.DictReader")[0]
src = src.replace("json.load(open('gen7.json'))", "json.load(open('gen8.json'))")
src = src.replace("SINGLES = ['ubers','ou','uu','ru','nu','pu','zu','monotype','1v1','battlespotsingles','anythinggoes']",
                  "SINGLES = ['ubers','ou','uu','ru','nu','pu','zu','monotype','1v1','battlestadiumsingles','anythinggoes','nationaldex','nationaldexuu','nationaldexru','nationaldexmonotype']")
exec(src)
print(sorted({k for f in d.values() for k in f}))
KOC = {'tank': '탱커', 'slow-phys': '저속물리', 'slow-spec': '저속특수', 'fast-phys': '고속물리', 'fast-spec': '고속특수'}
FROMKO = {v: k for k, v in KOC.items()}
gen7 = {}
for r in csv.DictReader(open('effort.tsv'), delimiter='\t'):
    gen7[(int(r['species']), int(r['form']))] = FROMKO[r['class']]
rows = list(csv.DictReader(open('dex8.tsv'), delimiter='\t'))
BASE = {}; STATS_OF = {}; KO_OF = {}; SF = {}
for r in rows:
    KO_OF[r['showdown']] = r['ko']; STATS_OF[r['showdown']] = r['stats']; SF[r['showdown']] = (int(r['species']), int(r['form']))
    if r['form'] == '0': BASE[r['showdown']] = r['stats']
    for f in (r['finals'].split('|') if r['finals'] else []):
        sp, fo, name, st = f.split(':'); STATS_OF.setdefault(name, st); SF.setdefault(name, (int(sp), int(fo)))
        if fo == '0': BASE[name] = st
def basis_name(sd, stats):
    if sd.startswith('Minior-'): return 'Minior'
    if d.get(sd) and any(k in TIER for k in d[sd]): return sd
    plain = sd.split('-')[0]
    if '-' in sd and BASE.get(plain) == stats and d.get(plain): return plain
    return sd
cache = {}
def class_of(name, stats):
    """(class, how) of a final form: the Ultra Sun table first, then gen 8 sets, then base stats."""
    key = SF.get(name)
    if key in gen7: return gen7[key], '7세대 표'
    if name in cache: return cache[name]
    b = basis_name(name, stats)
    c, how = by_smogon(b)
    if c is None: c, how = by_stats([int(x) for x in stats.split('/')]), '종족값'
    elif b != name: how = b + ' 의 ' + how
    cache[name] = (c, how); return c, how
CHOSEN = {79: 'Slowbro', 102: 'Exeggutor-Alola', 133: 'Sylveon', 236: 'Hitmonchan', 265: 'Beautifly', 361: 'Glalie', 366: 'Huntail', 789: 'Lunala', 790: 'Lunala', 840: 'Appletun'}
out = {}; branches = {}; news = {}
for r in rows:
    if r['event'] == '1': continue
    finals = [f.split(':') for f in r['finals'].split('|')] if r['finals'] else []
    sp, g = int(r['species']), r['gender']
    if sp in (280, 281) and g == '1': finals = [f for f in finals if f[2] == 'Gardevoir']
    if sp == 361 and g == '0': finals = [f for f in finals if f[2] == 'Glalie']
    if sp == 677: finals = [f for f in finals if f[2] == ('Meowstic' if g == '0' else 'Meowstic-F')]
    if sp == 876: finals = [f for f in finals if f[2] == ('Indeedee' if g == '0' else 'Indeedee-F')]
    cands = [(f[2], f[3]) for f in finals] if finals else [(r['showdown'], r['stats'])]
    cands = sorted(dict.fromkeys(cands), key=lambda c: SF[c[0]])
    res = [(n, *class_of(n, st)) for n, st in cands]
    key = (sp, int(r['form']), int(g))
    if len({x[1] for x in res}) > 1:
        pick = next((x for x in res if x[0] == CHOSEN.get(sp)), None)
        if pick: c, how, via = pick[1], pick[2], '분기→' + KO_OF.get(pick[0], pick[0]) + ' (주인장)'
        else:
            c, how, via = res[0][1], res[0][2], '분기→' + KO_OF.get(res[0][0], res[0][0]) + ' (기본)'
            branches[r['ko']] = [(KO_OF.get(x[0], x[0]), KOC[x[1]], x[2]) for x in res]
    else:
        c, how, via = res[0][1], res[0][2], KO_OF.get(res[0][0], res[0][0]) if finals else ''
    if how != '7세대 표': news[r['ko']] = (KOC[c], how, via)
    out[key] = (c, r['ko'], via, how)
bysf = {}
for (sp, fo, g), v in out.items(): bysf.setdefault((sp, fo), {})[g] = v
lines = ['species\tform\tsex\tclass\tname\tvia\tbasis']
for (sp, fo), gs in sorted(bysf.items()):
    if len({v[0] for v in gs.values()}) == 1:
        v = next(iter(gs.values())); lines.append(f"{sp}\t{fo}\t\t{KOC[v[0]]}\t{v[1]}\t{v[2]}\t{v[3]}")
    else:
        for g, v in sorted(gs.items()): lines.append(f"{sp}\t{fo}\t{'수' if g == 0 else '암'}\t{KOC[v[0]]}\t{v[1]}\t{v[2]}\t{v[3]}")
open('effort8.tsv', 'w').write('\n'.join(lines) + '\n')
print(len(lines) - 1, 'rows;', len(out), 'entries;', collections.Counter(l.split('\t')[3] for l in lines[1:]))
print('\n새로 분류한 것', len(news))
for k, v in news.items(): print(' ', k, v)
print('\n주인장이 골라야 하는 분기', len(branches))
for k, v in branches.items(): print(' ', k, v)
