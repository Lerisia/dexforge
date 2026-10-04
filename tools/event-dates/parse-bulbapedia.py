#!/usr/bin/env python3
"""Reads the Bulbapedia event lists saved from the Wayback Machine into events.tsv: one line per event box
(species number, level, OT names, ID numbers, moves, the region and the raw availability text, parsed windows, games)."""
import html, pathlib, re

HERE = pathlib.Path(__file__).parent
MONTHS = {m: i + 1 for i, m in enumerate(['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'])}
MONTH_RE = '|'.join(MONTHS)
MOVES = {r.split('\t')[2] for r in (HERE.parent / 'moves.tsv').read_text(encoding='utf-8').splitlines()[1:]}
GAME_GEN = {'R': 3, 'S': 3, 'E': 3, 'FR': 3, 'LG': 3, 'Colo': 3, 'XD': 3, 'D': 4, 'P': 4, 'Pt': 4, 'HG': 4, 'SS': 4,
            'B': 5, 'W': 5, 'B2': 5, 'W2': 5, 'X': 6, 'Y': 6, 'OR': 6, 'AS': 6, 'SM': 7, 'S7': 7, 'M': 7, 'US': 7, 'UM': 7, 'Sun': 7, 'Moon': 7}

def tokens(fragment):
    t = re.sub(r'<[^>]+>', '|', fragment)
    t = html.unescape(t).replace('\xa0', ' ')
    return [x.strip() for x in t.split('|') if x.strip()]

def windows(text):
    """Bulbapedia's availability text → [(start, end), …] as YYYY-MM-DD (day 00 = month only, year-only = -00-00).
    'July 15 to 16, July 21 to 22, 2007' (year at the end covers the earlier pieces), 'December 27, 2010 to January 10 and 11, 2011',
    'March 19 to April 3, April 29 to May 8, 2011', 'November 4, 2011 to November 3, 2012'. Pieces it cannot read come back as (text, '')."""
    t = re.sub(r'\s+,', ',', text).replace(' & ', '; ').replace(' and from ', '; '); t = re.sub(r'\s+', ' ', t).strip().rstrip('.').strip()
    t = re.sub(r'\b(\d{1,2}),? and (\d{1,2})\b', lambda m: f'{m[1]} to {m[2]}' if ' to ' not in t[:m.start()] else m[2], t)
    pieces = [x.strip() for x in re.split(r';|,\s*(?=(?:' + MONTH_RE + r')\b)|\band\b(?=\s+(?:' + MONTH_RE + r')\b)', t) if x.strip()]
    # a piece without a year takes the year of the next piece that has one
    years = [re.search(r'(\d{4})', x) for x in pieces]
    out = []
    for i, p in enumerate(pieces):
        y = years[i][1] if years[i] else next((years[j][1] for j in range(i + 1, len(pieces)) if years[j]), None)
        if y and not years[i]: p = f'{p}, {y}'
        m = re.fullmatch(r'(\w+) (\d{1,2}) to (\d{1,2}), (\d{4})', p)              # July 24 to 31, 2006
        if m and m[1] in MONTHS: out.append((f'{m[4]}-{MONTHS[m[1]]:02d}-{int(m[2]):02d}', f'{m[4]}-{MONTHS[m[1]]:02d}-{int(m[3]):02d}')); continue
        m = re.fullmatch(r'(\w+) (\d{1,2}) to (\w+) (\d{1,2}), (\d{4})', p)        # June 10 to July 5, 2006
        if m and m[1] in MONTHS and m[3] in MONTHS: out.append((f'{m[5]}-{MONTHS[m[1]]:02d}-{int(m[2]):02d}', f'{m[5]}-{MONTHS[m[3]]:02d}-{int(m[4]):02d}')); continue
        m = re.fullmatch(r'(\w+) (\d{1,2}), (\d{4}),? to (\w+) (\d{1,2}), (\d{4})', p)  # December 20, 2009 to January 10, 2010
        if m and m[1] in MONTHS and m[4] in MONTHS: out.append((f'{m[3]}-{MONTHS[m[1]]:02d}-{int(m[2]):02d}', f'{m[6]}-{MONTHS[m[4]]:02d}-{int(m[5]):02d}')); continue
        m = re.fullmatch(r'(\w+) (\d{1,2}), (\d{4})', p)                              # September 30, 2006
        if m and m[1] in MONTHS: d = f'{m[3]}-{MONTHS[m[1]]:02d}-{int(m[2]):02d}'; out.append((d, d)); continue
        m = re.fullmatch(r'(\w+),? (\d{4})', p)                                        # November 2018
        if m and m[1] in MONTHS: out.append((f'{m[2]}-{MONTHS[m[1]]:02d}-00', f'{m[2]}-{MONTHS[m[1]]:02d}-00')); continue
        m = re.fullmatch(r'(\w+) (\d{4}) to (\w+) (\d{4})', p)                        # March 2016 to May 2016
        if m and m[1] in MONTHS and m[3] in MONTHS: out.append((f'{m[2]}-{MONTHS[m[1]]:02d}-00', f'{m[4]}-{MONTHS[m[3]]:02d}-00')); continue
        m = re.fullmatch(r'(\d{4})', p)                                                 # 2007
        if m: out.append((f'{m[1]}-00-00', f'{m[1]}-00-00')); continue
        m = re.fullmatch(r'(\w+) (\d{1,2}), (\d{4}) (?:onwards?|to present|until .*)', p)
        if m and m[1] in MONTHS: out.append((f'{m[3]}-{MONTHS[m[1]]:02d}-{int(m[2]):02d}', '')); continue
        out.append((p, ''))
    return out

GAMES7 = {'X': 6, 'Y': 6, 'OR': 6, 'AS': 6, 'S': 7, 'M': 7, 'US': 7, 'UM': 7, 'SM': 7, 'USUM': 7, 'Sw': 8, 'Sh': 8, 'SwSh': 8, 'BD': 8, 'SP': 8, 'LA': 8, 'HOME': 8}

def parse_new_style(toks, title):
    """Gen 6/7 lists: '#0802', 'Marshadow', 'Level 50', … 'ID:', n, 'OT:', name, moves, 'Games','Method','Region','Location','Duration',
    rows of games/method/region/location/duration, then the wonder card text (its title is what PKHeX calls the card title)."""
    events = []
    starts = [i for i in range(len(toks) - 2) if re.fullmatch(r'#\d{4}', toks[i]) and any(re.match(r'Level \d+', toks[i + k]) for k in range(2, 7) if i + k < len(toks))]
    for n, i in enumerate(starts):
        end = starts[n + 1] if n + 1 < len(starts) else len(toks)
        blk = toks[i:end]
        li = next(k for k in range(2, 7) if re.match(r'Level \d+', blk[k]))
        num = int(blk[0][1:]); name = ' '.join(blk[1:li]); lv = int(blk[li].split()[1])
        def after(label):
            return blk[blk.index(label) + 1] if label in blk else ''
        ids = ' '.join(re.findall(r'\d{4,6}', after('ID:')))
        ot = after('OT:')
        di = blk.index('Duration') if 'Duration' in blk else len(blk)
        moves = [t for t in blk[:di] if t in MOVES][:4]
        tail = blk[di + 1:]
        stop = next((k for k, t in enumerate(tail) if t.startswith('Moves in') or t.startswith('Date received') or t == 'Distribution'), len(tail))
        rows = tail[:stop]
        games = {t for t in rows if t in GAMES7}
        method = ' '.join(dict.fromkeys(t for t in rows if t in ('serial code', 'local wireless', 'infrared', 'online', 'Nintendo Network', 'QR code', 'password', 'in-game')))
        if not method:
            method = ' '.join(dict.fromkeys(t.lower() for t in rows if re.match(r'(?i)serial code|local|internet|wi-fi|online|in-game|pok.mon home|mystery gift', t)))
        gens = {GAMES7[g] for g in games}
        when = [t for t in rows if re.search(MONTH_RE, t) and re.search(r'\d{4}', t)]
        region = ' '.join(dict.fromkeys(t for t in rows if t in ('Korean', 'Japanese', 'American', 'PAL', 'Taiwanese', 'European', 'All regions', 'All', 'all')))
        # Sword and Shield's list names the region by the codes' letter: J (Japan), U (America), E (Europe), K (Korea), T (Taiwan/Hong Kong)
        codes = ' '.join(dict.fromkeys({'J': 'Japanese', 'U': 'American', 'E': 'European', 'K': 'Korean', 'T': 'Taiwanese', 'A': 'All regions'}.get(m[1], '') for t in rows for m in [re.search(r'\((\w) Codes?\)', t)] if m))
        if codes: region = (region + ' ' + codes).strip()
        place = ' '.join(t for t in rows if t not in GAMES7 and t not in when and t not in ('serial code', 'infrared', 'local wireless', 'online', 'Wi-Fi', 'all', 'Korean', 'Japanese', 'American', 'PAL', 'Taiwanese', ',') and not re.fullmatch(r'[,.]', t))
        if place: region = (region + ' ' + place).strip()
        shiny = any('Shiny' in t for t in blk[:6])
        card_titles = ' | '.join(dict.fromkeys(tail[k + 1] for k, t in enumerate(tail[:-1]) if t in ('이상한 카드', 'ふしぎなカード', 'Wonder Card', 'Mystery Gift')))
        wins = []
        for w in when: wins += windows(w.replace(' to ', ' to '))
        # each duration row with the region its method cell names (the codes' letters, or a region word), in order
        LETTER = {'J': 'Japan', 'U': 'America', 'E': 'Europe', 'K': 'South Korea', 'T': 'Others', 'A': 'Global'}
        WORD = {'Korean': 'South Korea', 'Japanese': 'Japan', 'American': 'America', 'PAL': 'Europe', 'European': 'Europe', 'Taiwanese': 'Others', 'All regions': 'Global', 'All': 'Global', 'all': 'Global'}
        paired = []; cur = ''
        for t in rows:
            ms = re.findall(r'(?i)\b([JUEKTA])\b(?= codes?\)|(?= &)|(?= ,))', t) if re.search(r'(?i)codes?\)', t) else []
            ms = [m.upper() for m in ms]
            if ms: cur = ' & '.join(dict.fromkeys(LETTER[m] for m in ms))
            elif t in WORD: cur = WORD[t]
            if re.search(MONTH_RE, t) and re.search(r'\d{4}', t):
                for w in windows(t): paired.append(f"{cur or 'Global'}:{w[0]}~{w[1]}")
        events.append([num, name, lv, ot, ids, '/'.join(dict.fromkeys(moves)), region or ('South Korea' if 'Korean' in title else 'Japan' if 'Japanese' in title else ''),
                       '; '.join(when), '; '.join(w[0] for w in wins), '; '.join(w[1] for w in wins), '/'.join(map(str, sorted(gens))), 'Shiny' if shiny else '', title, card_titles[:300], method, '; '.join(paired)])
    return events

def parse_page(path):
    s = path.read_text(encoding='utf-8', errors='replace')
    title = path.stem.replace('_', ' ')
    events = []
    if 'Dex No.' not in s:
        body = re.sub(r'<script.*?</script>', '', s[s.find('mw-content-text'):], flags=re.S)
        # section by section, so that each event carries the heading it sits under (the event's name, often with its country)
        heads = [(m.start(), m.end(), html.unescape(re.sub(r'<[^>]+>', '', m.group(1))).strip()) for m in re.finditer(r'<h[23][^>]*>(.*?)</h[23]>', body, flags=re.S)]
        if not heads: return parse_new_style(tokens(body), title)
        events = []
        for i, (start, end, head) in enumerate(heads):
            nxt = heads[i + 1][0] if i + 1 < len(heads) else len(body)
            for e in parse_new_style(tokens(body[end:nxt]), title):
                e[12] = title + ' § ' + head
                events.append(e)
        return events
    # every event box has a "Dex No." cell; the box is the innermost table around it
    for m in re.finditer(r'Dex No\.', s):
        start = s.rfind('<table', 0, m.start()); end = s.find('</table>', m.end())
        block = s[start:end]
        toks = tokens(block)
        try: di = toks.index('Dex No.')
        except ValueError: continue
        name = toks[di - 1]
        num = int(toks[di + 1]) if di + 1 < len(toks) and toks[di + 1].isdigit() else 0
        lv = next((int(toks[i + 1].rstrip('.')) for i, t in enumerate(toks) if t in ('Lv', 'Lv.') and i + 1 < len(toks) and toks[i + 1].rstrip('.').isdigit()), 0)
        def between(a, bs):
            if a not in toks: return []
            i = toks.index(a) + 1; out = []
            while i < len(toks) and toks[i] not in bs: out.append(toks[i]); i += 1
            return out
        ot_toks = between('OT', {'ID No.', 'Item', 'This Pokémon was available in'})
        TYPES = {'Normal', 'Fire', 'Water', 'Electric', 'Grass', 'Ice', 'Fighting', 'Poison', 'Ground', 'Flying', 'Psychic', 'Bug', 'Rock', 'Ghost', 'Dragon', 'Dark', 'Steel', 'Fairy', '???'}
        ot = ot_toks[0] if ot_toks and ot_toks[0] != '--' else ''  # the first token is the OT; what follows is memo or the moves column
        ids = between('ID No.', {'This Pokémon was available in', 'Item', 'Ability', 'Can be obtained with:'})
        ids = '/'.join(x for x in ids if re.fullmatch(r'\d{4,6}', x))
        joined = '|'.join(toks)
        avail = re.search(r'This Pokémon was available in\|(.*?)\|(?:from|on|in|between|during|since)\|(.*?)\|\.', joined)
        region, when = (avail.group(1), avail.group(2).replace('|', ' ')) if avail else ('', '')
        if not avail:
            m2 = re.search(r'This Pokémon was available(.*?)\|\.', joined)
            when = m2.group(1).replace('|', ' ') if m2 else ''
        moves = [toks[i + 1] for i, t in enumerate(toks[:-1]) if t in TYPES and toks[i + 1] in MOVES]
        moves = '/'.join(dict.fromkeys(moves))
        games = between('Can be obtained with:', set())
        gens = sorted({GAME_GEN[g] for g in games if g in GAME_GEN})
        shiny = 'Shiny' in block[:3000] and 'ShinyIIIStar' in block or 'Shiny' in name
        wins = windows(when)
        after = tokens(s[end:end + 6000])
        card_titles = ' | '.join(dict.fromkeys(after[k + 1] for k, t in enumerate(after[:-1]) if t in ('이상한 카드', 'ふしぎなカード', 'Wonder Card', 'Mystery Gift')))
        method = ('local' if re.search(r'local|PCNY|10th|Journey|Party|Trade and Battle|Japanese event|English event|French event|German event|Italian event|Spanish event', title) else
                  'Wi-Fi' if 'Wi-Fi' in title else 'online' if 'GTS' in title else 'in-game' if 'game-based' in title else 'trade' if 'traded' in title else '')
        events.append([num, name, lv, ot, ids, moves, region, when, '; '.join(w[0] for w in wins), '; '.join(w[1] for w in wins), '/'.join(map(str, gens)), 'Shiny' if shiny else '', title, card_titles[:300], method, ''])
    return events

def main():
    out = ['\t'.join(['번호', '이름', '레벨', 'OT', 'ID', '기술', '지역', '기간 원문', '시작', '끝', '세대', '이로치', '페이지', '카드 글', '방법', '지역별 기간'])]
    n = 0
    for p in sorted(HERE.glob('List_of_*.html')):
        for e in parse_page(p):
            out.append('\t'.join(str(x).replace('\t', ' ') for x in e)); n += 1
    (HERE / 'events.tsv').write_text('\n'.join(out) + '\n', encoding='utf-8')
    print(f'{n} events → events.tsv')

if __name__ == '__main__':
    main()
