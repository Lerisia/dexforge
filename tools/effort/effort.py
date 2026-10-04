# Per dex entry (species, form, gender) -> class, from the rows the owner accepted as proposed (no overrides saved).
import json, csv, collections
exec(open('rows.py').read().split("out = [b for b")[0])   # rebuild targets/branches in memory
rows_json = json.load(open('rows.json'))
cls_by_sd = {r['sd']: r['cls'] for r in rows_json}
KOC = {'tank': '탱커', 'slow-phys': '저속물리', 'slow-spec': '저속특수', 'fast-phys': '고속물리', 'fast-spec': '고속특수'}
out = {}
for r in rows:
    if r['event'] == '1': continue
    finals = [f.split(':') for f in r['finals'].split('|')] if r['finals'] else []
    sp, g = int(r['species']), r['gender']
    if sp in (280, 281) and g == '1': finals = [f for f in finals if f[2] == 'Gardevoir']
    if sp == 361 and g == '0': finals = [f for f in finals if f[2] == 'Glalie']
    if sp == 412: finals = [f for f in finals if f[2].startswith('Mothim' if g == '0' else 'Wormadam')]
    if sp == 677: finals = [f for f in finals if f[2] == ('Meowstic' if g == '0' else 'Meowstic-F')]
    cands = [(f[2], f[3]) for f in finals] if finals else [(r['showdown'], r['stats'])]
    bases = sorted(dict.fromkeys(basis_name(n, st) for n, st in cands), key=lambda b: int(targets[b]['id'][1:].split('f')[0]))
    # the owner's picks for the families whose final forms disagree (2026-10-05)
    CHOSEN = {79: 'Slowbro', 102: 'Exeggutor-Alola', 133: 'Sylveon', 236: 'Hitmonchan', 265: 'Beautifly', 361: 'Glalie', 366: 'Huntail', 789: 'Lunala', 790: 'Lunala'}
    if len(bases) > 1 and sp in CHOSEN:
        pick = CHOSEN[sp]; assert pick in bases, (sp, bases)
        c = targets[pick]['cls']; via = '분기→' + targets[pick]['name'] + ' (주인장)'
    else:
        c = cls_by_sd['|'.join(bases)]
        via = targets[bases[0]]['name'] if len(bases) == 1 else '분기→' + targets[bases[0]]['name']
    key = (int(r['species']), int(r['form']), int(r['gender']))
    if key in out and out[key][0] != c: print('conflict', key, out[key], c)
    out[key] = (c, r['ko'], via)
bysf = {}
for (sp, fo, g), v in out.items(): bysf.setdefault((sp, fo), {})[g] = v
lines = ['species\tform\tsex\tclass\tname\tvia']
for (sp, fo), gs in sorted(bysf.items()):
    if len({v[0] for v in gs.values()}) == 1:
        v = next(iter(gs.values())); lines.append(f"{sp}\t{fo}\t\t{KOC[v[0]]}\t{v[1]}\t{v[2]}")
    else:
        for g, v in sorted(gs.items()): lines.append(f"{sp}\t{fo}\t{'수' if g == 0 else '암'}\t{KOC[v[0]]}\t{v[1]}\t{v[2]}")
open('effort.tsv', 'w').write('\n'.join(lines) + '\n')
print(len(lines) - 1, 'rows;', sum(1 for l in lines[1:] if l.split('\t')[2]), 'by sex;', len(out), 'entries')
print(collections.Counter(l.split('\t')[3] for l in lines[1:]))
for l in lines:
    if l.split('\t')[0] in ('133', '281', '412', '677', '361', '143') or l.startswith('774\t7'): print(' ', l)
