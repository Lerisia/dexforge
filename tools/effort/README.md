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
