# The one ball table

`src/Dexforge.Core/Data/balls.tsv` is the ball table every game draws on: for each species and form — by sex where the
owner split it, and by colour where a pick was made for a plain Pokémon — the balls in order of preference
(`문볼>러브러브볼`). A maker tries them from the top and keeps the first the game allows; a Poké Ball is the last resort
of every list and is not written. So a ball picked with a later game in mind falls through to the next in an older one
that has no such ball, and one list serves Ultra Sun, Sword, Scarlet and Z-A alike (owner, 2026-10-05: "볼은 철저히
우선순위제").

`Merge.cs` is how the table was first made, from the four tables that came before it (`sources/`): the Z-A picks of
2026-10-05 first, then Scarlet's picks, then Sword's table, then the national dex's list with the fallbacks it already
carried (`usum-balls.tsv`, the family lines expanded over the template), and last the confirmed drafts of the Scarlet
and Z-A tables — each ball once. A pick made where the game allowed no shiny (a static, a raid, the plain ones of the
Ultra Sun dex) was a pick for a plain Pokémon and sits in the `일반` row; a shiny falls back to it until the owner picks
for shiny ones separately. Run it from this folder with the Dexforge.Core project referenced; it is not run by the program.
