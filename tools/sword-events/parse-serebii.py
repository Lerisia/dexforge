#!/usr/bin/env python3
"""Reads the Serebii event dex pages (NNN.html) into events.tsv: one line per event block, with the region heading
and year heading the block sits under, the OT/ID/level/moves, and the start and end dates."""
import html, pathlib, re, sys
from datetime import date

HERE = pathlib.Path(__file__).parent
MONTHS = {m: i + 1 for i, m in enumerate(['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'])}

def text(fragment):
    t = re.sub(r'<br\s*/?>', '\n', fragment)
    t = re.sub(r'<[^>]+>', '', t)
    return html.unescape(t).replace('\xa0', ' ').strip()

def parse_date(s):
    """'30 September 2006' → 2006-09-30; 'September 2006' → 2006-09-00 (day unknown); anything else kept as text."""
    s = s.strip()
    m = re.fullmatch(r'(\d{1,2})(?:st|nd|rd|th)? (\w+),? (\d{4})', s)
    if m and m.group(2) in MONTHS:
        return f'{int(m.group(3)):04d}-{MONTHS[m.group(2)]:02d}-{int(m.group(1)):02d}'
    m = re.fullmatch(r'(\w+),? (\d{4})', s)
    if m and m.group(1) in MONTHS:
        return f'{int(m.group(2)):04d}-{MONTHS[m.group(1)]:02d}-00'
    m = re.fullmatch(r'(\d{4})', s)
    if m:
        return f'{m.group(1)}-00-00'
    return s

def cell(block, head):
    m = re.search(r'<td class="detailhead">' + re.escape(head) + r'</td><td>(.*?)</td>', block, re.S)
    return text(m.group(1)) if m else ''

def parse_page(path):
    s = path.read_text(encoding='utf-8', errors='replace')
    num = int(path.stem)
    # headings between blocks: region names in <u>, years as <p><font ...><b>2006</b>
    pieces = re.split(r'(<table class="eventpoke">)', s)
    events, region, year = [], '', ''
    for i in range(0, len(pieces)):
        chunk = pieces[i]
        if chunk == '<table class="eventpoke">':
            continue
        # the text before a block carries the headings; the block itself ends at its closing </table><br />
        if i >= 2 and pieces[i - 1] == '<table class="eventpoke">':
            end = chunk.find('</table><br />')
            block, rest = chunk[:end], chunk[end:]
            events.append(parse_block(num, block, region, year))
        else:
            rest = chunk
        for h in re.findall(r'<u>(.*?)</u>', rest):
            h = text(h)
            if re.fullmatch(r'\d{4}', h): year = h
            else: region = h
    return events

def parse_block(num, block, region, year):
    name_m = re.search(r'<td class="label">(.*?)</td>', block, re.S)
    name = text(name_m.group(1)) if name_m else ''
    shiny = bool(re.search(r'src="/Shiny/', block))  # the sprite is the shiny one
    level_m = re.search(r'<td class="label">Level (\d+)', block)
    level = int(level_m.group(1)) if level_m else 0
    ot, tid, ability = cell(block, 'OT:'), cell(block, 'ID:'), cell(block, 'Ability:')
    item_m = re.search(r'Hold Item:</td></tr><tr><td colspan="2">(.*?)</td>', block, re.S)
    item = text(item_m.group(1)) if item_m else ''
    nature_m = re.search(r'<td class="column">(.*?)</td><td class="column"><table', block, re.S)
    nature = text(nature_m.group(1)).replace('\n', ' / ') if nature_m else ''
    moves_m = re.search(r'<table  width="100">(.*?)</table>', block, re.S)
    moves = [text(x) for x in re.findall(r'<td >(.*?)</td>', moves_m.group(1), re.S)] if moves_m else []
    moves = '/'.join(m for m in moves if m)
    desc_m = re.search(r'Location</td>\s*</tr>\s*<tr>\s*<td>(.*?)</td>\s*<td>(.*?)</td>\s*<td>(.*?)</td>', block, re.S)
    desc, kind, location = (text(desc_m.group(1)), text(desc_m.group(2)), text(desc_m.group(3))) if desc_m else ('', '', '')
    date_m = re.search(r'End Date</td>\s*</tr>\s*<tr>\s*<td>(.*?)</td>\s*<td>(.*?)</td>', block, re.S)
    start, end = (parse_date(text(date_m.group(1))), parse_date(text(date_m.group(2)))) if date_m else ('', '')
    games_m = re.search(r'Games Available</td>\s*<td >(.*?)</td>', block, re.S)
    games = text(games_m.group(1)) if games_m else ''
    return [num, region, year, name, 'Shiny' if shiny else '', level, ot, tid, ability, item, nature, moves, desc, kind, location, start, end, games]

def main():
    out = ['\t'.join(['번호', '지역단', '연도단', '이름', '이로치', '레벨', 'OT', 'ID', '특성', '도구', '성격', '기술', '설명', '종류', '장소', '시작', '끝', '게임'])]
    files = sorted(HERE.glob('[0-9][0-9][0-9].html'))
    n = 0
    for p in files:
        for e in parse_page(p):
            out.append('\t'.join(str(x).replace('\t', ' ').replace('\n', ' ') for x in e)); n += 1
    (HERE / 'events.tsv').write_text('\n'.join(out) + '\n', encoding='utf-8')
    print(f'{len(files)} pages, {n} events → events.tsv')

if __name__ == '__main__':
    main()
