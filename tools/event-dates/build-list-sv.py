#!/usr/bin/env python3
"""From the ninth generation's catalogue (카탈로그.tsv, DumpWC9.cs) and the dates the matcher settled (날짜.tsv), the table the
program carries: src/Dexforge.Core/Data/scarlet/events.tsv, one line per distribution Scarlet can receive in the game itself.
Copies of one card that differ only in moves or Tera type (Mew's eighteen) are one line; the HOME gifts are left out; a card
only Violet receives is marked; what one code hands over together is given a group, so the maker receives it on one day."""
import csv, pathlib, sys
HERE = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else '.')
OUT = pathlib.Path(__file__).resolve().parents[2] / 'src/Dexforge.Core/Data/scarlet/events.tsv'
# one serial code, several cards (CoroCoro's Paradox pairs, the shiny Koraidon and Miraidon of 2025)
GROUPS = {('36', '고동치는달'): 'corocoro-2023', ('36', '무쇠무인'): 'corocoro-2023',
          ('38', '날개치는머리'): 'corocoro-2024-a', ('38', '무쇠머리'): 'corocoro-2024-a',
          ('38', '우렁찬꼬리'): 'corocoro-2024-b', ('38', '무쇠손'): 'corocoro-2024-b',
          ('38', '사나운버섯'): 'corocoro-2024-c', ('38', '무쇠가시'): 'corocoro-2024-c',
          ('1540', '미라이돈'): 'shiny-paradox-2025', ('1540', '코라이돈'): 'shiny-paradox-2025'}
HDR = ['카드번호', '카드 제목', '포켓몬', '폼', '이로치', '레벨', '볼', '어버이', 'TID', 'SID', '지역단', '시작', '끝', '교차', '설명', '같은 카드', '같은 코드', '버전']

def main():
    cat = list(csv.DictReader(open(HERE / '카탈로그.tsv', encoding='utf-8-sig'), delimiter='\t'))
    dates = {r['파일']: r for r in csv.DictReader(open(HERE / '날짜.tsv', encoding='utf-8-sig'), delimiter='\t')}
    rows, seen = [], {}
    for c in cat:
        if 'HOME' in c['비고']: continue
        d = dates.get(c['파일'])
        key = (c['카드번호'], c['카드 제목'], c['포켓몬'], c['폼'], c['레벨'], c['TID'], c['SID'], c['이로치'], c['볼'])
        if key in seen: seen[key]['같은 카드'] += 1; continue
        start = (d['확정 시작'] or d['시작']) if d else ''; end = (d['확정 끝'] or d['끝']) if d else ''
        row = {'카드번호': c['카드번호'], '카드 제목': c['카드 제목'], '포켓몬': c['포켓몬'], '폼': c['폼'], '이로치': c['이로치'], '레벨': c['레벨'], '볼': c['볼'], '어버이': c['어버이'],
               'TID': c['TID'], 'SID': c['SID'], '지역단': d['지역단'] if d else '', '시작': start, '끝': end, '교차': d['교차'] if d else '없음', '설명': (d['설명'] if d else '')[:120],
               '같은 카드': 1, '같은 코드': GROUPS.get((c['카드번호'], c['포켓몬']), ''), '버전': '바이올렛' if c['비고'].strip() == '바이올렛' else ''}
        seen[key] = row; rows.append(row)
    missing = [r for r in rows if not r['시작']]
    if missing: sys.exit('no date for: ' + ', '.join(f"#{r['카드번호']} {r['카드 제목']}" for r in missing))
    OUT.write_text('\n'.join(['\t'.join(HDR)] + ['\t'.join(str(r[h]) for h in HDR) for r in rows]) + '\n', encoding='utf-8')
    print(f'{len(rows)} distributions → {OUT}')

if __name__ == '__main__':
    main()
