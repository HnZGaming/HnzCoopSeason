# HnZ PvE Season

co-op season

## Roadmap

### V1.0

- [x] POIs on planets
- [x] POIs in space
- [x] ork encounter at all POIs
- [x] "POI release" event upon defeating orks
- [x] POI persistency across sessions
- [x] ork encounter on planets
- [x] weighted random spawn groups
- [x] planetary merchant encounter at safe POIs
- [x] make a merchant grid with a contract block
- [x] get reference to the contract block in script
- [x] disable MES cleanup for merchants
- [x] dedi: merchant cleanup check
- [x] merchants acquisition contracts
- [x] merchants acquisition contracts persistency across session
- [x] merchant safezone
- [x] merchant stores selling tech comps
- [x] merchant encounter in space
- [x] keep player grids intact upon npc despawn
- [x] ork invasion at random interval
- [x] vanilla stations for merchants -- 1x for space, 1x for planetary
- [x] merchant factions -- trader, miner, builder, military
- [x] vanilla store items for merchants
- [x] refill merchant stores
- [ ] progressive tech tiers
- [ ] vanilla contracts for merchants
- [ ] refill merchant contracts
- [ ] progressive contracts
- [ ] respawn near occupied POIs
- [ ] respawn ship data pad with POI info
- [ ] MOTD with recaps
- [ ] orks spawn in fleet, as opposed to a single spawn group

### V1.5

- [ ] merchant color override
- [ ] grids for sale by builder merchants
- [ ] wandering merchants random encounter around safe POIs
- [ ] wandering merchants stopping at player bases
- [ ] wandering orks random encounter based on area's tier score
- [ ] faction ranking for POIs released
- [ ] faction ranking for contracts completed
- [ ] faction ranking for tech comps purchased
- [ ] command-based global market with dynamic pricing and tax
- [ ] merchant shops stabilizing global market

### V2.0

tba

## Notes

### Ships for sale

code: `MyGenericFactionTypeStrategy.GenerateGridOffers()`

### Progressive store items

Progression based on:
- number of released POIs
- number of contracts finished
