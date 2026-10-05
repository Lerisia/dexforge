# The distributions' dates (Sword, Scarlet)

How `src/Dexforge.Core/Data/sword/events.tsv` and `src/Dexforge.Core/Data/scarlet/events.tsv` were made: the gift cards
PKHeX carries for the eighth and ninth generations, one per distribution (a card already holds every language), with the
window each was given out in.

1. The cards: every `WC8` entity in `EncounterEvent.MGDB_G8` that Sword can receive, every `WC9` in `MGDB_G9` that Scarlet
   can (`DumpWC9.cs` writes the ninth generation's catalogue in the event box's layout), without the Pokémon HOME gifts
   (those need a HOME tracker). Sword: 88 cards, 81 distributions once identical cards are merged. Scarlet: 113 cards,
   7 of them HOME's, 82 distributions once the copies that differ only in moves or Tera type are merged (the Mew gift was
   rolled at receipt — one card per Tera type — and the maker takes one of them at random).
2. The dates: Serebii's event dex pages (`parse-serebii.py` over `events/dex/NNN.shtml`, four-digit names for the ninth
   generation) and Bulbapedia's "List of event Pokémon distributions in Pokémon Sword and Shield" (`parse-bulbapedia.py`)
   or "... in Pokémon Scarlet and Violet" (`parse-bulbapedia-sv.py`, whose sections end with the Wonder Card's number and
   title, so the card is matched by that too), both fetched through the Wayback Machine. `match-dates.py` matches each
   card by trainer id, trainer name (in any of the card's languages), level, moves and card number, prefers the Korean
   window, then Japan's, then the rest, and takes the overlap where the two sources differ.
3. `dates-by-hand.tsv`: Sword windows checked against the official announcements where the sources disagreed (the Pokémon
   Center birthday gifts, Ash's Partner Cap Pikachu, the international Zarude).
4. `build-list-sv.py` writes Scarlet's table from the catalogue and the matched dates, with the groups of what one code
   hands over together: the known ones (CoroCoro's Paradox pairs, the shiny Koraidon and Miraidon of 2025) and, by the
   owner's rule of 2026-10-05, every set of cards given out over one window by one trainer in one region (the mythical
   trios of the Get campaigns, the birthday gifts). Sword's table carries the same groups, set by the same rule. PKHeX knows itself when each
   ninth-generation card was really handed out (`WC9.GetDistributionWindow`), and the maker keeps the received day inside
   that window where the table's disagrees — thirty of the eighty-two differ by more than a day at one end, Serebii's and
   Bulbapedia's windows usually being the narrower.

The scripts expect the layout of `~/storage/swsh-dex/events/` and `~/storage/sv-dex/events/` (catalogue, `sources/serebii`,
`sources/bulba`) and are kept here as the record of how the tables came to be; they are not run by the program.
