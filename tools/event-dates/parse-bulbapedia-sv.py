#!/usr/bin/env python3
"""Reads Bulbapedia's "List of event Pokémon distributions in Pokémon Scarlet and Violet" (saved from the Wayback Machine)
into events.tsv in the layout the matcher reads. The ninth generation's list is one chronological list, an event a section:
each block carries the Pokémon, its OT and ID, the moves, then rows of games (S, V) · method (serial code, password, internet)
· duration, the notes, and at the end the Wonder Card's number and title — which names the card PKHeX holds, so the match
is by that. The region is read from the section and the notes (a Korean store, a Japanese magazine, a championship)."""
import html, pathlib, re, sys
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[2]))
HERE = pathlib.Path(__file__).parent
MONTHS = {m: i + 1 for i, m in enumerate(['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'])}
MONTH_RE = '|'.join(MONTHS)
MOVES = {r.split('\t')[2] for r in (HERE.parent / 'moves.tsv').read_text(encoding='utf-8-sig').splitlines()[1:]}

def tokens(fragment):
    t = re.sub(r'<[^>]+>', '|', fragment)
    t = html.unescape(t).replace('\xa0', ' ')
    return [x.strip() for x in t.split('|') if x.strip()]

def windows(text):
    """'February 23 to March 31, 2024' → [('2024-02-23', '2024-03-31')]; 'November 21 to 30, 2025'; 'December 7, 2022 to January 31, 2023'; 'June 30, 2023'."""
    t = re.sub(r'\s+', ' ', text).strip().rstrip('.')
    out = []
    for p in [x.strip() for x in re.split(r';', t) if x.strip()]:
        m = re.fullmatch(r'(\w+) (\d{1,2}) to (\d{1,2}), (\d{4})', p)
        if m and m[1] in MONTHS: out.append((f'{m[4]}-{MONTHS[m[1]]:02d}-{int(m[2]):02d}', f'{m[4]}-{MONTHS[m[1]]:02d}-{int(m[3]):02d}')); continue
        m = re.fullmatch(r'(\w+) (\d{1,2}) to (\w+) (\d{1,2}), (\d{4})', p)
        if m and m[1] in MONTHS and m[3] in MONTHS: out.append((f'{m[5]}-{MONTHS[m[1]]:02d}-{int(m[2]):02d}', f'{m[5]}-{MONTHS[m[3]]:02d}-{int(m[4]):02d}')); continue
        m = re.fullmatch(r'(\w+) (\d{1,2}), (\d{4}),? to (\w+) (\d{1,2}), (\d{4})', p)
        if m and m[1] in MONTHS and m[4] in MONTHS: out.append((f'{m[3]}-{MONTHS[m[1]]:02d}-{int(m[2]):02d}', f'{m[6]}-{MONTHS[m[4]]:02d}-{int(m[5]):02d}')); continue
        m = re.fullmatch(r'(\w+) (\d{1,2}), (\d{4})', p)
        if m and m[1] in MONTHS: d = f'{m[3]}-{MONTHS[m[1]]:02d}-{int(m[2]):02d}'; out.append((d, d)); continue
        m = re.fullmatch(r'(\w+) (\d{1,2}), (\d{4}) (?:onwards?|to present)', p)
        if m and m[1] in MONTHS: out.append((f'{m[3]}-{MONTHS[m[1]]:02d}-{int(m[2]):02d}', '')); continue
        out.append((p, ''))
    return out

def region_of(heading, notes):
    """The language of origin the notes state, else the kind of event the heading names."""
    if 'Korean in origin' in notes: return 'South Korea'
    if 'Japanese in origin' in notes: return 'Japan'
    if re.search(r'\bKorea\b|Winter Festa|Pokémon Store|Pokémon Town|Great Pokémon Get Operation|Get Large-scale', heading + ' ' + notes): return 'South Korea'
    if re.search(r'\bJapan\b|CoroCoro|Pokémon Center|PokéDoko|Jump Festa|Project Kabigon|Michina|Pikatto|YOASOBI', heading + ' ' + notes): return 'Japan'
    return 'Global'

def parse():
    s = (HERE / 'List_of_event_Pokémon_distributions_in_Pokémon_Scarlet_and_Violet.html').read_text(encoding='utf-8', errors='replace')
    body = re.sub(r'<script.*?</script>', '', s[s.find('mw-content-text'):], flags=re.S)
    heads = [(m.start(), m.end(), html.unescape(re.sub(r'<[^>]+>', '', m.group(1))).strip()) for m in re.finditer(r'<h[23][^>]*>(.*?)</h[23]>', body, flags=re.S)]
    events = []
    for i, (start, end, head) in enumerate(heads):
        nxt = heads[i + 1][0] if i + 1 < len(heads) else len(body)
        toks = tokens(body[end:nxt])
        starts = [k for k in range(len(toks) - 2) if re.fullmatch(r'#\d{4}', toks[k])]
        for n, k in enumerate(starts):
            blk = toks[k:starts[n + 1] if n + 1 < len(starts) else len(toks)]
            li = next((j for j in range(1, 8) if j < len(blk) and re.match(r'Level \d+', blk[j])), None)
            if li is None: continue
            num = int(blk[0][1:]); name = ' '.join(x for x in blk[1:li] if not x.startswith('(') and x != '/'); lv = int(blk[li].split()[1])
            def after(label):
                return blk[blk.index(label) + 1] if label in blk else ''
            tid = after('ID:'); ot = after('OT:')
            if ot == 'Met:': ot = ''
            di = blk.index('Duration') if 'Duration' in blk else len(blk)
            moves = [t for t in blk[:di] if t in MOVES][:4]
            tail = blk[di + 1:]
            # the rows of games/method/duration end where the notes begin
            stop = next((j for j, t in enumerate(tail) if t.startswith('This Pokémon') or t.startswith('Date received')), len(tail))
            rows, notes = tail[:stop], tail[stop:]
            method = ' '.join(dict.fromkeys(t for t in rows if t in ('Serial Code', 'Password:', 'Internet', 'Local', 'Infrared')))
            when = [t for t in rows if re.search(MONTH_RE, t) and re.search(r'\d{4}', t)]
            # the section's Wonder Cards are listed at its end: the one whose title names this species, else all of them
            cards = [f'{toks[j]} | {toks[j + 1]}' for j in range(len(toks) - 1) if re.fullmatch(r'Wonder Card \d+', toks[j])]
            mine = [c for c in cards if name.split(' ')[0] in c]
            card = ' / '.join(mine or cards)
            region = region_of(head, ' '.join(notes))
            shiny = 'Shiny' in head or any('Shiny' in t for t in blk[:li])
            wins = []
            for w in when: wins += windows(w)
            paired = '; '.join(f'{region}:{a}~{b}' for a, b in wins)
            events.append([num, name, lv, ot, tid, '/'.join(dict.fromkeys(moves)), region, '; '.join(when), '; '.join(a for a, _ in wins), '; '.join(b for _, b in wins), '9', 'Shiny' if shiny else '',
                           'List of event Pokémon distributions in Pokémon Scarlet and Violet § ' + head, card[:300], method, paired])
    return events

def main():
    out = ['\t'.join(['번호', '이름', '레벨', 'OT', 'ID', '기술', '지역', '기간 원문', '시작', '끝', '세대', '이로치', '페이지', '카드 글', '방법', '지역별 기간'])]
    events = parse()
    for e in events: out.append('\t'.join(str(x).replace('\t', ' ') for x in e))
    (HERE / 'events.tsv').write_text('\n'.join(out) + '\n', encoding='utf-8')
    print(f'{len(events)} events → events.tsv')

if __name__ == '__main__':
    main()
