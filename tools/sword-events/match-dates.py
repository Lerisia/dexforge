#!/usr/bin/env python3
"""Matches every card in 카탈로그.tsv to an event record from the sources (Serebii's event dex, Bulbapedia's event lists)
and writes 날짜.tsv: one line per card, in catalogue order, with the event's start and end dates, description, location,
source and how the match was made. A match needs the trainer ID, or the OT together with the moves and level;
everything else only ranks candidates. Equal-score records of one source are the same card's several windows."""
import pathlib, re, unicodedata
from dataclasses import dataclass, field

HERE = pathlib.Path(__file__).parent
SRC = HERE / 'sources'

def table(path):
    rows = [l.split('\t') for l in path.read_text(encoding='utf-8-sig').splitlines() if l.strip()]
    return rows[0], rows[1:]

_, species = table(SRC / 'species.tsv')
NUM = {r[1]: int(r[0]) for r in species}
_, moves = table(SRC / 'moves.tsv')
MOVE_EN = {r[1]: r[2] for r in moves}

REGION_OK = {  # card region → region groups where its event is listed
    '한국': {'South Korea'}, '일본': {'Japan'}, '영어': {'America', 'Europe', 'Others', 'Global'},
    '': {'America', 'Europe', 'Others', 'Global', 'Japan'},
    '프랑스': {'Europe', 'Global'}, '독일': {'Europe', 'Global'}, '이탈리아': {'Europe', 'Global'}, '스페인': {'Europe', 'Global'},
    '중국(간체)': {'Others', 'Global', 'South Korea', 'Japan'}, '중국(번체)': {'Others', 'Global', 'South Korea', 'Japan'},
}
SEREBII_GAME_GEN = [
    (3, r'Ruby|Sapphire|Emerald|FireRed|LeafGreen|Colosseum|XD'),
    (4, r'Diamond|Pearl|Platinum|HeartGold|SoulSilver'),
    (5, r'Black|White'),
    (6, r'\bX\b|\bY\b|Omega ?Ruby|Alpha ?Sapphire'),
    (7, r'\bSun\b|\bMoon\b|Ultra'),
    (8, r'Sword|Shield|HOME'),
]
# the eighth generation's cards are one card for every language; a Korean save receives any of them in Korean, so the date is
# the Korean window where there was one, then Japan's, then the rest
REGION_RANK = {'South Korea': 0, 'Japan': 1, 'Global': 2, 'America': 3, 'Europe': 4, 'Others': 5, '': 6}
CAPS = {1: 'original', 2: 'hoenn', 3: 'sinnoh', 4: 'unova', 5: 'kalos', 6: 'alola', 7: 'partner', 8: 'world', 9: 'world'}   # Ash's caps by form
CAP_CODES = {'original': 'P1KACHUGET', 'hoenn': 'P1KAADVANCE', 'sinnoh': 'V0LTTACKLEP1KA', 'unova': 'P1KABESTW1SH', 'kalos': 'KAL0SP1KA', 'alola': 'ULTRAP1KA', 'partner': '1CH00SEY0U', 'world': 'K1NP1KA1855'}   # Serebii names them by their codes

@dataclass
class Event:
    source: str
    num: int
    region: str          # America / Europe / Japan / South Korea / Others / Global
    ot: str              # all OT spellings, space separated
    ids: str             # all trainer IDs, space separated
    level: int
    moves: set
    gens: set
    shiny: bool
    start: str
    end: str
    desc: str
    location: str
    extra: dict = field(default_factory=dict)

def norm(s):
    s = unicodedata.normalize('NFKC', s).lower().replace('－', 'ー').replace('ｰ', 'ー').replace('~', '〜')
    return re.sub(r'\s+', '', s)

def tokens(cell):
    toks = {norm(t) for t in re.split(r'\s+|/', cell)}
    toks.add(norm(cell))
    return {t for t in toks if t}

def close(a, b):
    """Levenshtein distance ≤ 2 on names of 4+ characters: Serebii's typos (WCSK14 for WCS14K) and PKHeX's full-width letters."""
    if len(a) < 4 or len(b) < 4 or abs(len(a) - len(b)) > 2: return False
    prev = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        cur = [i]
        for j, cb in enumerate(b, 1):
            cur.append(min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + (ca != cb)))
        prev = cur
    return prev[-1] <= 2

def serebii_events():
    _, rows = table(SRC / 'serebii' / 'events.tsv')
    for r in rows:
        num, ereg, eyear, ename, eshiny, elv, eot, eid, eabil, eitem, enat, emoves, edesc, ekind, eloc, estart, eend, egames = r
        gens = {g for g, pat in SEREBII_GAME_GEN if re.search(pat, egames)}
        yield Event('Serebii', int(num), ereg, eot, eid, int(elv) if elv.isdigit() else 0, {m for m in emoves.split('/') if m},
                    gens, eshiny == 'Shiny', estart, eend, edesc, eloc, {'kind': ekind, 'games': egames, 'year': eyear})

def bulba_region(page, avail):
    a = avail.lower()
    if 'korea' in a or 'Korean' in page: return 'South Korea'
    if 'japan' in a or 'Japanese' in page: return 'Japan'
    if re.search(r'united states|america|canada|u\.s\.', a) or 'American' in page: return 'America'
    if re.search(r'europe|france|germany|italy|spain|united kingdom|uk\b|netherlands|belgium|portugal|austria|switzerland|ireland|sweden|norway|denmark|finland|poland|russia', a) or 'PAL' in page or re.search(r'French|German|Italian|Spanish', page): return 'Europe'
    if re.search(r'taiwan|hong kong|australia|new zealand|singapore|philippines|malaysia|thailand|indonesia|china|latin|mexico|brazil|oceania', a) or 'Taiwanese' in page: return 'Others'
    if a in ('all regions', 'all'): return 'Global'
    if re.search(r'worldwide|all regions|globally', a): return 'Global'
    if 'English' in page: return 'America'
    return ''

def bulba_events():
    path = SRC / 'bulba' / 'events.tsv'
    if not path.exists(): return
    _, rows = table(path)
    for r in rows:
        num, name, lv, ot, ids, emoves, region, when, start, end, gens, shiny, page, card_titles, method, paired = (r + [''] * 3)[:16]
        if paired:
            # Sword and Shield's list: every duration row with its own region (from the codes' letters), one record each
            for piece in paired.split('; '):
                reg, win = piece.split(':', 1); a, b = win.split('~')
                head = page.split(' § ')[-1]
                reg = {'Global': ''}.get(reg, reg) or bulba_region(head, head) or 'Global'
                yield Event('Bulbapedia', int(num), reg, ot.replace(' / ', ' '), ids.replace('/', ' '), int(lv or 0),
                            {m for m in emoves.split('/') if m}, {int(g) for g in gens.split('/') if g}, shiny == 'Shiny', a, b,
                            head, region, {'when': when, 'titles': card_titles, 'kind': method})
            continue
        yield Event('Bulbapedia', int(num), bulba_region(page, region), ot.replace(' / ', ' '), ids.replace('/', ' '), int(lv or 0),
                    {m for m in emoves.split('/') if m}, {int(g) for g in gens.split('/') if g}, shiny == 'Shiny', start, end,
                    page.replace('List of ', ''), region, {'when': when, 'titles': card_titles, 'kind': method})

def namu_events():
    path = SRC / 'namu' / 'events.tsv'
    if not path.exists(): return
    _, rows = table(path)
    for r in rows:
        num, name, lv, ot, ids, emoves, when, place, start, end, title, gen = r
        if not int(num): continue
        yield Event('나무위키', int(num), 'South Korea', re.sub(r'\s*\[\w+\]', '', ot).replace(',', ' '), ids, int(lv or 0),
                    {m for m in emoves.split('/') if m}, {int(gen)} if gen else set(), False, start, end, title, place, {'when': when})

def pokewiki_events():
    path = SRC / 'pokewiki-ja' / 'events.tsv'
    if not path.exists(): return
    _, rows = table(path)
    for r in rows:
        num, name, lv, ot, ids, emoves, region, when, start, end, gen, shiny, page = (r + [''] * 13)[:13]
        if not num.isdigit() or not int(num): continue
        yield Event('ポケモンWiki', int(num), region or 'Japan', ot, ids, int(lv or 0), {m for m in emoves.split('/') if m},
                    {int(gen)} if gen.isdigit() else set(), shiny == 'Shiny', start, end, page, '', {'when': when})

def pokewiki_de_events():
    path = SRC / 'pokewiki-de' / 'events.tsv'
    if not path.exists(): return
    _, rows = table(path)
    for r in rows:
        num, name, lv, ot, ids, emoves, region, when, start, end, gen, shiny, page, nick = (r + [''] * 14)[:14]
        if not num.isdigit() or not int(num): continue
        yield Event('PokéWiki', int(num), region, '' if ot == '(Spieler)' else ot, ids, int(lv or 0), {m for m in emoves.split('/') if m},
                    {int(gen)} if gen.isdigit() else set(), shiny == 'Shiny', start, end, page, '', {'when': when, 'titles': nick})

def score(card, e):
    gen, kind, title, mon, form, shiny, lv, ot, tid, sid, region, ball, egg, kmoves, item, note, file = card[:17]
    cmoves = {MOVE_EN.get(m, m) for m in kmoves.split('/') if m}
    why = []; s = 0
    tid7 = (int(sid or 0) * 65536 + int(tid or 0)) % 1000000 if tid.isdigit() else -1  # what gen 7 and 8 show and the sites list
    ids = {int(x) for x in e.ids.split() if x.isdigit()}
    if tid.isdigit() and ids and (int(tid) in ids or (gen in ('7', '8') and tid7 in ids)): s += 3; why.append('ID')
    spellings = {norm(x) for x in [ot] + list(card[22:24]) if x}   # the eighth generation's cards name the trainer in every language
    anonymous = not ot or egg or tid in ('0', '65535')  # eggs and in-game gifts carry the receiver's name
    pc = 'PCJP' in kind or 'PCNY' in kind  # Pokémon Center gifts: PKHeX fixes no OT (PCNYa…d, or the Japanese shop names)
    ot_toks = tokens(e.ot)
    if spellings and (spellings & ot_toks or spellings & tokens(e.ids)): s += 3; why.append('OT')
    elif spellings and any(close(sp, t) for sp in spellings for t in ot_toks): s += 2; why.append('OT≈')
    elif anonymous and (e.ot in ('Yours', '??', "(Hatcher's)", '', '(Spieler)') or 'player' in e.ot.lower()): s += 2; why.append('OT=받는이')
    elif pc and (e.ot.startswith('PCNY') or re.search(r'トウキョー|ヨコハマ|ナゴヤ|オーサカ|フクオカ|サッポロ|ポケセン|ＰＣ', e.ot) or 'Pokémon Center' in e.desc or 'PCNY' in e.desc): s += 2; why.append('OT=센터')
    if e.level and e.level == int(lv or 0): s += 1; why.append('Lv')
    if cmoves and e.moves:
        frac = len(cmoves & e.moves) / max(len(cmoves), len(e.moves))
        s += 2 * frac
        if frac == 1: why.append('기술')
        elif frac >= 0.5: why.append(f'기술{len(cmoves & e.moves)}/{max(len(cmoves), len(e.moves))}')
    if e.gens and int(gen) in e.gens: s += 2; why.append('세대')
    elif e.gens: s -= 2
    if e.region in REGION_OK.get(region, set()): s += 1; why.append('지역')
    if title and e.extra.get('titles') and norm(title) in {norm(t) for t in e.extra['titles'].split(' | ')}: s += 2; why.append('카드제목')
    if e.shiny == (shiny == 'Always'): s += 0.5
    if not e.start: s -= 1  # a record without a date is of no use here
    return s, why

def accepted(why):
    # a receiver-named egg or Pokémon Center gift is only told apart from its foreign twin by region
    has_ot = 'OT' in why or 'OT≈' in why or (('OT=받는이' in why or 'OT=센터' in why) and '지역' in why)
    moves_ok = '기술' in why or ('기술3/4' in why and '지역' in why) or ('OT=센터' in why and '지역' in why)  # PCJP templates carry no moves
    return ('세대' in why and ('ID' in why or (has_ot and moves_ok and 'Lv' in why))) or ('ID' in why and 'OT' in why) or ('카드제목' in why and (has_ot or 'ID' in why))

def manual():
    """날짜-수동.tsv: dates I looked up by hand where no source record matched; keyed by generation, species, OT and TID."""
    path = HERE / '날짜-수동.tsv'
    if not path.exists(): return {}
    _, rows = table(path)
    return {(r[0], r[1], r[2], r[3]): r for r in rows}

def best_in(card, events):
    """The best accepted record among events, with the equal-score records of the same source as its windows."""
    best, best_score, best_why, ties = None, 0, [], []
    for e in events:
        s, why = score(card, e)
        if s > best_score: best, best_score, best_why, ties = e, s, why, [e]
        elif best is not None and s == best_score: ties.append(e)
    if best is None or not best.start or not accepted(best_why): return None
    # a record may carry several windows as 'a; b' strings; keep them apart for the cross-check
    windows = sorted({w for t in ties for w in zip(t.start.split('; '), (t.end or '').split('; ') + [''] * 9)})
    windows = [w for w in windows if w[0]]
    return {'event': best, 'score': best_score, 'why': best_why, 'windows': windows,
            'start': '; '.join(w[0] for w in windows), 'end': '; '.join(w[1] for w in windows),
            'desc': ' / '.join(dict.fromkeys(t.desc for t in ties)), 'location': ' / '.join(dict.fromkeys(t.location for t in ties))}

def same_date(a, b):
    """Dates agree when every part both of them know agrees ('2018-11-00' matches '2018-11-21'); open ends match each other."""
    if a in ('', 'No End Date') or b in ('', 'No End Date'): return a in ('', 'No End Date') and b in ('', 'No End Date')
    pa, pb = a.split('-'), b.split('-')
    if len(pa) != 3 or len(pb) != 3: return a == b
    return all(x == y or x in ('00', '?') or y in ('00', '?') for x, y in zip(pa, pb))

def overlap(w1, w2):
    a1, b1 = w1; a2, b2 = w2
    def key(d, late): return d.replace('-00', '-99' if late else '-01') if d not in ('', 'No End Date') else ('9999' if late else '0000')
    return key(a1, False) <= key(b2, True) and key(a2, False) <= key(b1, True)

def dkey(x, late):
    return x.replace('-00', '-99' if late else '-01') if x not in ('', 'No End Date') else ('9999' if late else '0000')

def verified_window(primary, others, v):
    """The window to use: the agreed one when sources agree, the overlap (narrowest) when they only partly agree,
    the primary's when it stands alone. A date inside the overlap is consistent with every source."""
    if v in ('일치', '출처 하나', '수동 확인', '없음', '불일치') or not others:
        if v == '일치':
            for o in others:
                for w1 in primary['windows']:
                    for w2 in o['windows']:
                        if same_date(w1[0], w2[0]) and same_date(w1[1], w2[1]):
                            return (max(w1[0], w2[0], key=lambda x: dkey(x, False)), min(w1[1], w2[1], key=lambda x: dkey(x, True)) if w1[1] and w2[1] else (w1[1] or w2[1]))
        return primary['windows'][0] if primary['windows'] else ('', '')
    best = None
    for o in others:
        for w1 in primary['windows']:
            for w2 in o['windows']:
                if same_date(w1[0], w2[0]) and all(not w[1] for w in o['windows']):  # start-only source
                    cand = w1
                elif overlap(w1, w2):
                    cand = (max(w1[0], w2[0], key=lambda x: dkey(x, False)), min(w1[1], w2[1], key=lambda x: dkey(x, True)) if w1[1] and w2[1] else (w1[1] or w2[1]))
                else: continue
                if best is None or dkey(cand[1], True) < dkey(best[1], True) or (cand[1] == best[1] and dkey(cand[0], False) > dkey(best[0], False)): best = cand
    return best or (primary['windows'][0] if primary['windows'] else ('', ''))

def id_date(gen, tid, sid):
    """Many Korean and Japanese cards carry the start date in the ID: gen 7 as YYMMDD (170919), gen 4–5 as MMDDY (12160 = Dec 16 2010).
    Returns (yy, mm, dd) with yy None for the one-digit year, or None."""
    if not tid.isdigit(): return None
    if gen in ('7', '8'):
        t7 = (int(sid or 0) * 65536 + int(tid)) % 1000000
        yy, mm, dd = t7 // 10000, t7 // 100 % 100, t7 % 100
        if 1 <= mm <= 12 and 1 <= dd <= 31 and 13 <= yy <= 26: return (2000 + yy, mm, dd)
        return None
    t = int(tid)
    if t >= 100000: return None
    mm, dd, y = t // 1000, t // 10 % 100, t % 10
    if 1 <= mm <= 12 and 1 <= dd <= 31: return (y, mm, dd)
    return None

def id_matches_start(gen, tid, sid, start):
    d = id_date(gen, tid, sid)
    if not d or not start or not re.match(r'\d{4}-\d{2}-\d{2}', start): return False
    y, m, dd = int(start[:4]), int(start[5:7]), int(start[8:10])
    return m == d[1] and dd == d[2] and (y == d[0] if d[0] > 100 else y % 10 == d[0])

METHOD = {  # what the sources call it → 현장(wireless/infrared/in person) · 시리얼 · 인터넷 · 글로벌링크 · 게임내 · QR · 교환
    'in-life': '현장', 'in life': '현장', 'local': '현장', 'local wireless': '현장', 'infrared': '현장', 'local wireless infrared': '현장', 'infrared local wireless': '현장',
    'serial code': '시리얼', 'password': '시리얼', 'serial code password': '시리얼',
    'wi-fi': '인터넷', 'online': '인터넷', 'nintendo network': '인터넷', 'wi-fi online': '인터넷', 'online nintendo network': '인터넷',
    'global link': '글로벌링크', 'in-game': '게임내', 'qr code': 'QR', 'trade': '교환',
}
def method_of(e):
    k = (e.extra.get('kind') or '').strip().lower()
    return METHOD.get(k, '현장' if 'local' in k or 'infrared' in k else '시리얼' if 'serial' in k else '인터넷' if 'online' in k or 'network' in k or 'wi-fi' in k else '')

def verdict(primary, others):
    """일치 when another source has the same window; 시작 일치 when a start-only source agrees on the start;
    부분 일치 when only the start agrees or the windows overlap; 불일치 otherwise."""
    if not others: return '출처 하나'
    best = '불일치'
    for o in others:
        start_only = all(not w[1] for w in o['windows'])
        for w1 in primary['windows']:
            for w2 in o['windows']:
                if same_date(w1[0], w2[0]) and same_date(w1[1], w2[1]): return '일치'
                if same_date(w1[0], w2[0]) and start_only: best = '시작 일치' if best != '일치' else best
                elif same_date(w1[0], w2[0]) or overlap(w1, w2): best = '부분 일치' if best not in ('시작 일치',) else best
    return best

def main():
    _, cards = table(HERE / '카탈로그.tsv')
    hand = manual()
    by_num = {}
    for e in list(serebii_events()) + list(bulba_events()) + list(namu_events()) + list(pokewiki_events()) + list(pokewiki_de_events()):
        by_num.setdefault(e.num, []).append(e)
    out = ['\t'.join(['파일', '세대', '포켓몬', '어버이', 'TID', '지역', '시작', '끝', '설명', '종류', '장소', '게임', '지역단', '출처', '근거', '점수', '출처 OT', '출처 ID', '교차', '다른 출처', '확정 시작', '확정 끝', '방법'])]
    matched = 0; by_gen = {}; by_source = {}; verdicts = {}
    for c in cards:
        gen, kind, title, mon, form, shiny, lv, ot, tid, sid, region, ball, egg, kmoves, item, note, file = c[:17]
        events = by_num.get(NUM[mon], [])
        per_source = {}
        for src in ('Serebii', 'Bulbapedia', '나무위키', 'ポケモンWiki', 'PokéWiki'):
            pool = [e for e in events if e.source == src]
            m = None
            if gen == '8':
                pool = [e for e in pool if 8 in e.gens or not e.gens]   # Serebii's pages carry every generation's events
                # a card with a trainer id is only ever the records that carry it, when the source has any
                tid6 = (int(sid or 0) * 65536 + int(tid or 0)) % 1000000 if tid.isdigit() else 0
                if tid6:
                    with_id = [e for e in pool if str(tid6) in e.ids.split()]
                    if with_id: pool = with_id
                # Ash's caps share one id: the cap tells them apart (the event's name or description names it)
                if mon == '피카츄' and form.isdigit() and int(form) in CAPS:
                    cap = CAPS[int(form)]
                    named = [e for e in pool if cap in (e.desc + ' ' + e.extra.get('titles', '') + ' ' + e.location).lower() or CAP_CODES[cap] in e.desc]
                    if named: pool = named
                # the best region that has an accepted record: Korea, then Japan, then the rest — but only among records
                # that score within a point of the best, so a weak match in a preferred region never beats a strong one
                top = max((score(c, e)[0] for e in pool), default=0)
                strong = [e for e in pool if score(c, e)[0] >= top - 1]
                for rank in sorted({REGION_RANK.get(e.region, 6) for e in strong}):
                    m = best_in(c, [e for e in strong if REGION_RANK.get(e.region, 6) == rank])
                    if m: break
            else: m = best_in(c, pool)
            if m: per_source[src] = m
        h = hand.get((gen, mon, ot, tid))
        if h:
            primary = {'windows': [(h[4], h[5])], 'start': h[4], 'end': h[5], 'desc': h[6], 'location': '', 'why': [h[7]], 'score': 0,
                       'event': Event('수동', NUM[mon], '', ot, tid, 0, set(), set(), False, h[4], h[5], h[6], '')}
            others = list(per_source.values())
        elif per_source:
            # the eighth generation: the preferred region first, across sources, then the score
            src_best = (min(per_source, key=lambda k: (REGION_RANK.get(per_source[k]['event'].region, 6), -per_source[k]['score'])) if gen == '8'
                        else max(per_source, key=lambda k: per_source[k]['score']))
            primary = per_source[src_best]
            others = [v for k, v in per_source.items() if k != src_best]
            # a window is only checked against the same country's record: another country's dates are a different window
            # (Bulbapedia's Sword and Shield list often names no country at all — such a record may belong to any)
            if gen == '8': others = [o for o in others if o['event'].region == primary['event'].region or (o['event'].source == 'Bulbapedia' and o['event'].region == 'Global')]
        else:
            primary = None
        if primary:
            v = '수동 확인' if h else verdict(primary, others)  # a hand-entered date was read off a source table myself
            if v == '출처 하나' and any(id_matches_start(gen, tid, sid, w[0]) for w in primary['windows']): v = 'ID 날짜 일치'  # the trainer ID spells the start date
            e = primary['event']
            matched += 1; by_gen[gen] = by_gen.get(gen, 0) + 1; by_source[e.source] = by_source.get(e.source, 0) + 1; verdicts[v] = verdicts.get(v, 0) + 1
            other_txt = ' | '.join(f"{o['event'].source} {o['start']}~{o['end']}" for o in others)
            vw = verified_window(primary, others, v)
            method = next((m for m in [method_of(e)] + [method_of(o['event']) for o in others] if m), '')
            out.append('\t'.join([file, gen, mon, ot, tid, region or '미상', primary['start'], primary['end'], primary['desc'], e.extra.get('kind', ''), primary['location'],
                                  e.extra.get('games', ''), e.region, e.source, '+'.join(primary['why']), f"{primary['score']:.1f}", e.ot, e.ids, v, other_txt, vw[0], vw[1], method]))
        else:
            out.append('\t'.join([file, gen, mon, ot, tid, region or '미상', '', '', '', '', '', '', '', '', '후보 없음', '0', '', '', '없음', '', '', '', '']))
            verdicts['없음'] = verdicts.get('없음', 0) + 1
    (HERE / '날짜.tsv').write_text('\n'.join(out) + '\n', encoding='utf-8-sig')
    print(f'{matched} / {len(cards)} cards matched ({", ".join(f"{g}세대 {n}" for g, n in sorted(by_gen.items()))}; '
          f'{", ".join(f"{k} {v}" for k, v in by_source.items())}) → 날짜.tsv')
    print('교차검증: ' + ', '.join(f'{k} {v}' for k, v in sorted(verdicts.items(), key=lambda x: -x[1])))

if __name__ == '__main__':
    main()
