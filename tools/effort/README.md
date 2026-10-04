# The effort classes

How `src/Dexforge.Core/Data/template/effort.tsv` was made: for every non-event Pokémon of the Ultra Sun dex, which of five
spreads the Effort Ribbon fills — tank (HP, Defense), slow physical or special attacker (the attacking stat, HP), fast
physical or special attacker (the attacking stat, Speed).

1. `Dump.cs` (a console project referencing PKHeX.Core) reads the dex save and prints each Pokémon with its Showdown name,
   base stats and every final form its line reaches, as `dex.tsv`.
2. `classify.py` reads Smogon's gen 7 sets (`https://data.pkmn.cc/sets/gen7.json`) and, per final form, counts what the
   singles sets (Ubers to ZU, Monotype, 1v1, Battle Spot Singles, AG; no doubles, no LC) put 252 into: the two biggest
   stats name the class, the majority wins, the highest tier's first set breaks ties. Forms that differ only in looks use
   the plain species' sets. A base-stat rule was tried and agreed with the sets only 49% of the time, so it is not used.
3. `rows.py` turns that into one row per final form, or per family whose final forms disagree.
4. `effort.py` writes the table, with the owner's picks for the families that fork (Slowbro, Alolan Exeggutor, Sylveon,
   Hitmonchan, Beautifly, Glalie, Huntail, Lunala). Pre-evolutions take the class of the final form they reach for their sex.

## Sword

`Dump8.cs` reads the Sword save with the gen 8 evolution tree, and `effort8.py` writes `Data/sword/effort.tsv`: the final form
each entry reaches in Sword takes its Ultra Sun class where that form is in the Ultra Sun table, and the majority of Smogon's
gen 8 singles sets (`https://data.pkmn.cc/sets/gen8.json`; Ubers to ZU, Monotype, 1v1, Battle Stadium Singles, AG, National
Dex tiers) where it is not — gen 8 species, Galarian forms, and what Ultra Sun only had from cards. The owner's picks for
forking lines carry over; Applin defaults to Flapple, the lower number, until picked.
