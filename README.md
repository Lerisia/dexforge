# Dexforge

포켓몬스터 울트라썬의 전국도감 세이브(807종, 954마리)를 받는 사람의 이름과 ID로 만든다.
틀이 되는 세이브를 프로그램이 품고 있고, 실행할 때마다 그 안의 포켓몬을 전부 새로 뽑는다.
포켓몬의 값은 게임이 실제로 뽑는 순서대로, 게임의 난수가 있을 수 있는 상태에서 뽑는다.

한국 본체, 울트라썬, 한국어, 여자 주인공의 세이브만 만든다.

**배포 박스**도 만든다: 3~7세대의 모든 배포 카드를 나라만 다른 것은 하나로 쳐(한국 > 일본 > 미국 > 유럽) 751건, 알은 부화시켜, 미진화체는 최종 진화체까지 더해 928마리를 울트라썬 세이브 하나에. 받은 날은 다섯 출처로 맞춘 배포 기간 안의 하루다. 남는 32칸은 규칙으로 빠진 것 중에서 직접 골라 담는다. 자세한 것은 창의 도움말 '배포 박스' 절.

**소드**도 만든다: 가라르·갑옷섬·왕관설원 세 도감의 전 종과 폼, 663종 760마리를 JKSV 로 복원하는 백업 폴더로. 알이 되는 것은 알(Admiral Fish 가 적은 소드실드의 알 난수 순서대로), 화석은 화석, 전설은 고정 조우와 다이맥스 어드벤처, 환상은 배포 카드다. 자세한 것은 창의 도움말 '소드' 절.

![창](docs/screenshots/filled.png)

## 창으로 쓰기

`Dexforge.exe`를 더블클릭하면 창이 뜬다. 옵션을 칸과 단추로 고르고 **세이브 만들기**를 누른다.
파일 하나로 되어 있어 설치할 것이 없다.

- 아무것도 건드리지 않고 누르면 아래 표의 "안 주면" 값대로 만든다.
- 결과는 **저장할 곳** 안에 `Dexforge-<이름>-<TID>` 폴더로 쓴다. 처음에는 프로그램이 있는 폴더다.
- 만들지 못하면 까닭이 나온다. 프로그램이 멈추면 프로그램 옆에 `Dexforge-오류.txt`가 남는다.
- 오른쪽 위의 **도움말**은 받는 사람이 읽는 설명서다. 글은 `src/Dexforge.Gui/Assets/help.md`에 있고,
  제목(`#`, `##`, `###`), 목록(`-`), 굵은 글씨(`**`)만 쓴다. 고친 뒤에는 다시 빌드한다.

시드(`--seed`)와 틀 고치기(`--refresh`)는 명령어로만 한다.

## 명령어로 쓰기

`Dexforge.Cli`를 인자 없이 실행하면 울트라썬 것을 하나씩 묻는다. 빈 칸으로 두면 괄호 안의 값을 쓴다. 소드는 인자로만 만든다.

```
Dexforge.Cli --name 미월 --sid 1234 --tid 567890 --ball 럭셔리볼 --color 이로치 --ivs 5V --sex 랜덤 --level 최저 --from 2018-01-01 --to 2018-12-31
```

소드는 `--game 소드` 를 앞에 붙인다. 그때 쓰는 옵션은 `--name --sid --tid --ball --color --year --seed --out` 이다.

```
Dexforge.Cli --game 소드 --name 우리 --ball 볼맞춤 --color 이로치 --year 2021
```

배포 박스는 `--game 배포박스` 를 앞에 붙인다. 옵션은 `--name --sid --tid --seed --out --first-days --pick --pick-file` 이고, `--list-picks` 는 남는 칸에 골라 담을 수 있는 것을 키와 함께 늘어놓는다.

```
Dexforge.Cli --game 배포박스 --name 미월 --first-days 7 --pick D:2522,E:1215:26-0
```

| 옵션 | 고를 수 있는 것 | 안 주면 |
|---|---|---|
| `--game` | 울트라썬 / 소드 | 울트라썬 |
| `--year` | (소드) 얻은 해, 2019~2099 | 2021 |
| `--first-days` | (배포 박스) 받은 날을 배포 기간의 처음 n일 안에서; 0이면 기간 전체 | 0 |
| `--pick`, `--pick-file` | (배포 박스) 남는 칸에 담을 것의 키, 쉼표로 / 한 줄에 하나인 파일 | 없음 |
| `--name` | 어버이 이름, 6글자까지 | 미월 |
| `--english` `--japanese` `--chinese` | 외국어판 게임의 어버이 이름, 7·5·6글자까지 | Selene, ミヅキ, 美月 |
| `--sid` `--tid` | SID 네 자리(0000~4294), TID 여섯 자리(게임에 보이는 ID). PKHeX 의 [SID]TID | 무작위 |
| `--ball` | 볼 이름, 또는 `볼맞춤` | 몬스터볼 |
| `--color` | `일반`, `이로치` | 이로치 |
| `--ivs` | `랜덤`, `5V` | 랜덤 |
| `--sex` | `수컷`, `암컷`, `랜덤` | 랜덤 |
| `--level` | `최저`, `100` | 최저 |
| `--from` `--to` | 포켓몬을 얻은 기간의 첫날과 마지막 날 | 2018-01-01 ~ 2018-12-31 |
| `--seed` | 추첨의 시드 | 무작위 |
| `--out` | 결과를 쓸 폴더 | `Dexforge-<이름>-<TID>` |

- **볼**: 볼 이름을 주면 전부 그 볼에 넣고, 그 볼이 안 되는 포켓몬은 몬스터볼에 넣는다. `볼맞춤`은 포켓몬마다 골라 둔 볼이다.
  배포 포켓몬은 어느 쪽이든 카드가 정한 볼이다. 숨겨진 특성 때문에만 안 되는 포켓몬은 일반 특성이 된다.
- **색**: 이로치가 막힌 포켓몬은 늘 일반 색이고, 카드가 이로치로 정한 배포는 늘 이로치다.
- **개체값**: `5V`는 알에서 나온 포켓몬만 다섯 개가 31이다. 배포와 3·4세대 출신은 어느 쪽이든 그 게임이 준 값이다.
  6V는 빼 두었다. 야생에서 잡은 셋(메타몽·물거미·깨비물거미)이 6V인 난수 자리는 10억 개에 하나라, 미리 전부 찾아 둔 뒤 넣는다.
  생성기 안의 `IvChoice.Six`는 남아 있다(알과 31 셋이 보장된 것만 6V). 난입 배틀은 6V를 쉽게 하지 않는다: 연쇄는 31이 넷이 될 때까지만 채운다.
- **성별**: 박스의 포켓몬 중 성별을 바꿀 수 있는 것만 따른다. 무성, 한 성별뿐인 종, 카드나 만남이 정한 것, 암수 모습이 달라 둘 다 넣은 종은 그대로다.
  랜덤은 종의 성비대로 난수가 정한다. 규칙은 `Options/Sexes.cs`.
- **레벨**: `100`이면 박스의 포켓몬이 모두 100이 된다. 기술은 그대로다.
- **파티**의 세 마리는 볼만 따르고 나머지 옵션은 따르지 않는다. 파티는 포켓몬 뱅크로 옮길 수 없어서 쓰임이 없다.
- **기간**: 첫날과 마지막 날을 같게 주면 하루로 통일된다. 3DS 의 시계는 마음대로 바꿀 수 있어서, 2000-01-02 ~ 2099-12-31 안이면 어느 기간이든 된다.
  배포는 배포 기간 안의 날짜다. 모험 시작일은 첫날의 3주 이상 전으로 잡힌다.

몇 초 걸린다.

## 만들어지는 것

- `main` — 세이브 파일
- `만든기록.txt` — 트레이너, 옵션, 그리고 포켓몬마다 값이 나온 난수의 자리

만든 세이브는 쓰기 전에 스스로 검사한다(`Making/Check.cs`). 하나라도 어긋나면 쓰지 않는다.
검사하는 것: 전부 PKHeX 의 적법성 검사를 통과하는지, 틀의 이름과 ID가 남지 않았는지, 고른 옵션대로인지, 날짜의 앞뒤, 게임 기록, 바뀌면 안 되는 블록.

## 값을 뽑는 방식

| 포켓몬 | 난수 | 기준 | 코드 |
|---|---|---|---|
| 알 | TinyMT | 3DSRNGTool | `Rng/Egg.cs` |
| 7세대에서 잡거나 받은 것 | SFMT | 3DSRNGTool | `Rng/Seven.cs`, `Making/Caught7.cs` |
| 3·4세대 출신 | LCRNG, MT19937 | PokeFinder | `Rng/Old.cs`, `Making/Older.cs` |
| 배포 | 카드가 정한 대로 | PKHeX | `Data/Events.cs`, `Making/Generator.cs` |

`만든기록.txt`에 적힌 시드와 번호를 3DSRNGTool(3·4세대는 PokeFinder)에 넣으면 같은 개체가 나온다.

정해 둔 것:

- 빛나는부적이 있다. 파티 3마리만 모험 첫날들이라 없다.
- 선두에 싱크로가 없다.
- 야생은 꿀을 쓴 조우다.
- 알의 부모는 그 종 한 마리와 외국의 메타몽이다(국제교배).
- 버튼은 세이브를 불러온 뒤 30분쯤 안에 누른 것이다.

확인하지 못한 것:

- 1번도로의 턱지충이 Lv4. 3DSRNGTool의 표에는 Lv2~3 풀숲만 있고 PKHeX의 표에는 같은 종 구성의 Lv2~4 풀숲이 따로 있다.
  3DSRNGTool의 칸 배치에 PKHeX의 레벨 범위를 썼다.
- 포켓몬 뱅크와 포켓몬 HOME이 모두 받아 주는지.
- 윈도우에서의 실행. 리눅스 빌드만 돌려 보았고, 창 프로그램은 화면 없이 그려 본 것과 가상 화면에 띄워 본 것까지다.

## 폴더

```
src/
  Dexforge.Core/    생성기 라이브러리
    Options/             고를 수 있는 것과 그 규칙 (Options, Trainer, Ids7, ForeignNames, Sexes)
    Rng/                 게임의 난수와 그 난수로 한 번 만나기 (Seven, Egg, Old)
    Data/                3DSRNGTool 의 표, 배포 카드, 볼별 표, 틀 세이브(template/dex)
    Making/              세이브 만들기와 검사 (Generator, Caught7, Older, Check, Making)
    Sword/               소드: 알 난수(Xoroshiro8, Egg8), 종·폼·출처(Plan8), 볼(Balls8), 만들기(Maker8, Evolve8), 세이브 쓰기(Making8)
    Data/sword/          소드의 틀 세이브(main 과 JKSV 가 함께 쓰는 세 파일), 볼 표(balls.tsv), HOME 트래커(trackers.tsv)
    EventBox/            배포 박스: 카드 열거(Cards), 받는 트레이너(Receivers), 카드→개체(EventMaker), 진화(Evolve7), 골라 담기(Custom), 세이브 쓰기(EventBoxMaking)
    Data/events/         배포 목록(distributions.tsv): 카드 2,630장을 855 배포로 합치고 날짜를 맞춘 표
    Wording/             옵션을 한국어로 알아듣고 말하기
  Dexforge.Cli/     명령어
  Dexforge.Gui/     창 (Avalonia)
tests/
  Dexforge.Core.Tests/   생성기 테스트와 대조값(vectors)
  Dexforge.Cli.Tests/    명령어 테스트 (인자, 하나씩 묻기, 거절, 틀 고치기)
  Dexforge.Gui.Tests/    창 테스트 (화면 없이 창을 띄워 칸을 채우고 단추를 누른다)
tools/
  Reference/    3DSRNGTool 의 원래 소스를 컴파일한 시험대와 대조값 생성 스크립트
  BallTable/    볼별 "넣을 수 없는 수" 표(Data/BallFits.cs) 생성
docs/screenshots/   창의 그림 (DEXGEN_GUI_SHOTS 로 테스트가 찍은 것)
```

## 빌드와 테스트

.NET 10 SDK 가 필요하다.

```
dotnet build -c Release
dotnet test -c Release                                    # 1분쯤
DEXGEN_SLOW=1 dotnet test -c Release                      # 이로치 6V 시험까지, 3분쯤
DEXGEN_GUI_SHOTS=<폴더> dotnet test -c Release            # 창의 그림도 그 폴더에 남긴다
dotnet test -c Release --collect:"XPlat Code Coverage"    # 라인 커버리지 (TestResults/…/coverage.cobertura.xml)
```

배포용 단일 파일:

```
dotnet publish src/Dexforge.Gui -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish/win-x64
```

명령어판은 `src/Dexforge.Cli`로 같은 명령을 쓴다. 남에게 줄 때는 exe 하나면 된다.

테스트가 보는 것:

- 우리가 뽑은 값이 3DSRNGTool과 PokeFinder의 값과 같은지 (`tests/Dexforge.Core.Tests/vectors`)
- 옵션의 이름을 알아듣는지, 안 되는 값을 거절하는지 (명령어의 인자와 하나씩 묻기까지)
- 만든 세이브가 온전한지, 고른 옵션대로인지
- 만든 세이브의 포켓몬을 기록된 난수 자리에서 다시 뽑으면 같은 값이 나오는지
- 틀이 온전한지
- 창이 정해 둔 기본값으로 열리는지, 채운 대로 읽는지, 단추를 누르면 온전한 세이브가 써지는지, 안 되는 값을 까닭과 함께 거절하는지

## 틀을 바꿨을 때

틀은 `src/Dexforge.Core/Data/template/dex`이고 빌드할 때 프로그램 안에 들어간다. 틀을 바꾸면 다시 빌드해야 한다.

틀에 포켓몬을 새로 넣었다면 그 값도 게임의 난수대로 뽑아 둔다. 날짜, 볼, 어버이는 그대로 두고 알과 잡은 포켓몬의 값만 다시 뽑는다.

```
Dexforge.Cli --refresh <세이브> <결과 폴더> [--only 590,591]
```

틀의 포켓몬이나 볼 규칙이 바뀌면 창이 보여 주는 "그 볼에 넣을 수 없는 수" 표도 다시 만든다. 볼마다 세이브를 만들어 세므로 3분쯤 걸린다.

```
dotnet run -c Release --project tools/BallTable > src/Dexforge.Core/Data/BallFits.cs
```

새로운 종류의 포켓몬(새 배포, 새 구세대 포획)이 틀에 들어오면 `Data/Events.cs`나 `Making/Older.cs`에 그 규칙을 먼저 넣어야 한다.

## 대조값을 다시 만들 때

`tests/Dexforge.Core.Tests/vectors`의 7세대 파일은 3DSRNGTool의 원래 소스를 고치지 않고 컴파일해 뽑은 값이다.

```
cd tools/Reference
git clone https://github.com/wwwwwwzx/3DSRNGTool
dotnet build -c Release
python3 make-vectors.py ../../tests/Dexforge.Core.Tests/vectors
```

`Data/SevenTable.cs`와 `Data/SevenAreas.cs`도 이 시험대의 `list`·`areas` 모드가 내놓은 그 도구의 표를 옮긴 것이다.

## 출처와 라이선스

이 프로그램은 GPLv3 이다(`LICENSE`). PKHeX.Core 가 GPLv3 이라 프로그램을 남에게 주면 소스도 같은 조건으로 내놓아야 한다.

- [PKHeX.Core](https://github.com/kwsch/PKHeX) — GPLv3. 세이브를 읽고 쓰는 일과 적법성 검사.
- [3DSRNGTool](https://github.com/wwwwwwzx/3DSRNGTool) — MIT. 7세대의 생성 순서와 장소 표. 저장소에는 넣지 않고 대조값을 만들 때만 받는다.
- [PokeFinder](https://github.com/Admiral-Fish/PokeFinder) — GPLv3. 3·4세대의 대조값.
- [Avalonia](https://github.com/AvaloniaUI/Avalonia) 11.3 — MIT. 창.
- [Pretendard JP](https://github.com/orioncactus/pretendard) 1.3.9 — SIL OFL 1.1. 창의 글꼴(한글·가나·한자가 다 있는 판).
  라이선스 전문은 `src/Dexforge.Gui/Assets/Pretendard-LICENSE.txt`.
