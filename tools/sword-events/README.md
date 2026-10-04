# Sword's distributions

How `src/Dexforge.Core/Data/sword/events.tsv` was made: the eighth generation's gift cards PKHeX carries, one per
distribution (a card already holds every language), with the window each was given out in.

1. The cards: every `WC8` entity in `EncounterEvent.MGDB_G8` that Sword can receive, without the Pokémon HOME gifts
   (those need a HOME tracker). 88 cards, 81 distributions once identical cards are merged.
2. The dates: Serebii's event dex pages (`parse-serebii.py` over `events/dex/NNN.shtml`) and Bulbapedia's
   "List of event Pokémon distributions in Pokémon Sword and Shield" (`parse-bulbapedia.py`, fetched through the Wayback
   Machine). `match-dates.py` matches each card by trainer id, trainer name (in any of the card's languages), level and
   moves, prefers the Korean window, then Japan's, then the rest, and takes the overlap where the two sources differ.
3. `dates-by-hand.tsv`: windows checked against the official announcements where the sources disagreed (the Pokémon
   Center birthday gifts, Ash's Partner Cap Pikachu, the international Zarude).

The scripts expect the layout of `~/storage/swsh-dex/events/` (catalogue, `sources/serebii`, `sources/bulba`) and are kept
here as the record of how the table came to be; they are not run by the program.
