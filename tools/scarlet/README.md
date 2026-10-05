# The Scarlet plan and balls

`src/Dexforge.Core/Data/scarlet/plan.tsv` is the owner's plan for every species and form the game can hold (950 lines):
where each comes from (야생 wild, 진화 evolved from a wild or static earlier stage, 고정 a fixed symbol, 알 an egg, 레이드 a raid,
교환 an in-game trade, 없음 not in this game), whether it can shine, which are Violet's (버전), and which have a shiny only by a
card (이로치배포). It was drawn up from PKHeX's encounter tables and Serebii's version-exclusive list.

The balls were picked on `ball-picker.html` (a family at a time, with the final evolutions' shiny sprites; the page as
published, without its data file and sprite sheets) and now live in the one table every game shares, `Data/balls.tsv`
(see `tools/balls`).
