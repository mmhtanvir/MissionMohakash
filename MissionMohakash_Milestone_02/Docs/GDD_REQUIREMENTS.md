# GDD requirements (read from Junior_Astronaut_Mission_Trainer_GDD.pdf, draft v1.0, 6 Oct 2026, 56 pages)

Working title **OUTPOST ZERO**, subtitle Junior Astronaut Mission Trainer. One astronaut, Moon and Mars outposts;
"engineering trade-offs, not resource bars, are the game". Ages 12-18. Loop: Plan -> Build -> Operate -> Explore -> Collect Data ->
Manage Resources -> Face Problem -> Repair/Adapt -> Complete Objective -> Evaluate.

## Missions (exact names)
1. **First Light** (Moon, Connecting Ridge): 72 h, 125 Cr / 540 kg. Deploy SEIS >= 8 cells from HAB, powered >= 24 h; log 1 radiation
   reading; survive 72 h. Secondary: 2 regolith samples, O2 >= 3 kg at end, no suit damage. Hazards: 6 h shadow window, minor SPE, suit dust.
2. **Into the Shadow** (Moon, Faustini): 120 h, 230 Cr / 900 kg. Find ice with SPEC, 2 ice-soil samples, >= 10 L water via WEU; relay; GEN option.
3. **Jezero Air** (Mars): 148 h, 270 Cr / 850 kg. MOX-P >= 20 h, >= 1.2 kg O2, deploy WX, 3 samples; 12 min comm delay; dust.
4. **Arcadia Endurance** (Mars): 247 h, 340 Cr / 1000 kg. >= 40 L water; dust storm, 3-sol blackout; greenhouse.

## Systems used by Mission 1 (all GAME UNITS)
- Time: 1-minute tick; pause / 1x / 2x / 4x; auto-slow on warnings.
- Power: SOL-S +1.0 PW peak x sun x (1-dust); BAT 10 PWh, max 2 PW out; priority 1 life support, 2 suit, 3 comms, 4 science, 6 rover charging.
- Life support: OGS 1.0 PW, 0.10 kg O2/h, 1.125 L water per kg O2; crew 0.035 kg O2/h, 0.042 kg CO2/h, 3 L water/day, 104 kcal/h;
  SCR 0.06 kg CO2/h, cartridge 3 kg; cabin 4 kg O2 + 8 kg per tank; leak 0.002 kg/h; O2 = 0 for 2 h -> failure.
- Suit A: O2 4.5 h, battery 4.2 h, CO2 4.0 h; shortest timer = EVA limit; 30-min reserve; walk-back circle.
- Rover ROV-SC: 6 PWh, 0.12 PWh per cell, x1.3 in shadow, max slope 20°.
- Hazard ladder: Warning -> Degradation -> Critical -> Recovery -> Failure; no instant random death.
- Score (§26): objectives 30, science 15, safety 12, efficiency 10, equipment 8, exploration 5, optional 10, sustainability 5, time 5;
  pass 50, stars 50/70/85; failed capped at 40 ("Learning Mode").
- Report (§25.1): timeline, cause chain, two alternative decisions.

## Still open vs the GDD
Construction grid (§12), real LOLA terrain + illumination (§28), Data Sources screen with manifest, Judge Mode, hover glossary,
map states / exploration layers, Mission 2-4.
