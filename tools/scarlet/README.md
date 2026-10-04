# The Scarlet plan and balls

`src/Dexforge.Core/Data/scarlet/plan.tsv` is the owner's plan for every species and form the game can hold (950 lines):
where each comes from (야생 wild, 진화 evolved from a wild or static earlier stage, 고정 a fixed symbol, 알 an egg, 레이드 a raid,
교환 an in-game trade, 없음 not in this game), whether it can shine, which are Violet's (버전), and which have a shiny only by a
card (이로치배포). It was drawn up from PKHeX's encounter tables and Serebii's version-exclusive list.

`balls.tsv` is the ball for each entry: the owner's picks from the ball-picker page (a family at a time, with the final
evolutions' shiny sprites), the drafts from the Ultra Sun and Sword saves where the owner confirmed them, and the sex rules
given in chat. `ball-picker.html` is the page as published (its data file and sprite sheets are not kept here).
