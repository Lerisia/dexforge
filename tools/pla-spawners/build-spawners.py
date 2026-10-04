"""One field spawner for every wild species, form and alpha-ness of Legends: Arceus.

Runs in the PLA bot's virtual environment (~/storage/pla-permute-bot/seedtools/.venv), whose plaseed package reads the
game's spawner data out of numba_pokemon_prngs. For each target it keeps the spawner, time of day and weather
in which the target slot takes the largest share of the draw, with every slot of that table at that time and
weather, so the maker can draw a generator seed that lands on the slot and prove it with the same arithmetic
the bot's verifier uses (float32, sequential subtraction).

    cd ~/storage/pla-permute-bot/seedtools && .venv/bin/python ~/storage/dexforge/tools/pla-spawners/build-spawners.py \
        ~/storage/dexforge/src/Dexforge.Core/Data/arceus/spawners.tsv
"""
import sys
sys.path.insert(0, "/home/elyss/storage/pla-permute-bot/seedtools")
import numpy as np
from plaseed.spawners import load_spawners
from plaseed.encounter import TIMES, WEATHERS, LAND_WEATHERS
from plaseed.personal import SPECIES_IDS

out = sys.argv[1]
spawners = load_spawners()
ids = SPECIES_IDS  # name -> national number


def share(slots, index, time, weather):
    weights = [np.float32(s.effective_weight(time, weather)) for s in slots]
    total = sum(weights, np.float32(0))
    if total == 0 or weights[index] == 0:
        return 0.0, weights
    return float(weights[index] / total), weights


best = {}
for sp in spawners:
    land_weathers = [WEATHERS.index(w) for w in LAND_WEATHERS[sp.area]]
    for i, slot in enumerate(sp.slots):
        key = (ids[slot.name], slot.form, slot.is_alpha)
        for t in range(len(TIMES)):
            for w in land_weathers:
                s, weights = share(sp.slots, i, t, w)
                if s == 0:
                    continue
                # prefer the biggest share; among equals, the smallest table and a single-spawn spawner
                rank = (s, -len(sp.slots), sp.max_count == 1)
                if key not in best or rank > best[key][0]:
                    best[key] = (rank, sp, i, t, w, weights)

rows = ["번호\t폼\t우두\t지역\t표\t시간\t날씨\t슬롯\t몫\t레벨\t보장IV\t성별고정\t슬롯표"]
for key in sorted(best):
    rank, sp, i, t, w, weights = best[key]
    slot = sp.slots[i]
    table = "|".join(f"{ids[s.name]}-{s.form}-{int(s.is_alpha)}-{float(wt):g}" for s, wt in zip(sp.slots, weights))
    rows.append("\t".join(map(str, [key[0], key[1], int(key[2]), sp.area, f"{sp.table:016X}", TIMES[t], WEATHERS[w], i,
                                    f"{rank[0]:.4f}", f"{slot.level_min}-{slot.level_max}", slot.guaranteed_ivs,
                                    "" if slot.gender is None else slot.gender, table])))
open(out, "w", encoding="utf-8").write("\n".join(rows) + "\n")
print(len(rows) - 1, "targets")
