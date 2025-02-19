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
- [x] vanilla economy stations for merchants -- 1x for space, 1x for planetary
- [x] merchant factions -- trader, miner, builder, military
- [ ] vanilla economy store items for merchants
- [ ] vanilla economy contracts for merchants
- [ ] refill merchant contracts
- [ ] refill merchant stores
- [ ] progressive tech tiers
- [ ] respawn near occupied POIs
- [ ] respawn ship data pad with POI info
- [ ] MOTD with recaps

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

### Reusing vanilla economy definitions

- station prefabs: `MyDefinitionManager.Static.GetAllDefinitions<MyStationsListDefinition>()`
- faction types: `MyDefinitionManager.Static.GetAllDefinitions<MyFactionTypeDefinition>()`
- contracts: `MyDefinitionManager.Static.GetContractTypeDefinitions()`

- [x] List of contracts per faction type
- [x] List of contract occurrance weight per faction
- [x] List of economy faction types
- [x] List of orders/offers per faction type
- [x] Max contract count per faction type
- [x] List of prefab names per station type

- Data/ContractTypes_Economy.sbc
  - Definitions/ContractTypes/ContractType
    - Id/TypeId/ContractTypeDefinition
    - Id/SubtypeId/Deliver - MyObjectBuilder_ContractTypeDeliverDefinition
    - Id/SubtypeId/ObtainAndDeliver - MyObjectBuilder_ContractTypeObtainAndDeliverDefinition
      - AvailableItems
    - Id/SubtypeId/Escort - MyObjectBuilder_ContractTypeEscortDefinition
      - PrefabsAttackDrones
      - PrefabsEscortShips
      - DroneBehaviors
    - Id/SubtypeId/Find - MyObjectBuilder_ContractTypeFindDefinition
      - PrefabSearchableGrids
    - Id/SubtypeId/Hunt - MyObjectBuilder_ContractTypeHuntDefinition
    - Id/SubtypeId/Repair - MyObjectBuilder_ContractTypeRepairDefinition
      - PrefabNames
    - ChancesPerFactionType
      - ContractChance/DefintionId/SubtypeId
        - Miner, Trader, Builder, etc
- Data/Stations_Economy.sbc
  - Definitions/Definition - MyObjectBuilder_FactionTypeDefinition
    - Id (Type=MyObjectBuilder_FactionTypeDefinition, Subtype=Miner)
    - Id (Type=MyObjectBuilder_FactionTypeDefinition, Subtype=Trader)
    - Id (Type=MyObjectBuilder_FactionTypeDefinition, Subtype=Builder)
    - Id (Type=MyObjectBuilder_FactionTypeDefinition, Subtype=Pirate)
    - Id (Type=MyObjectBuilder_FactionTypeDefinition, Subtype=Military)
    - ...
    - OfferList
    - OrderList
    - MaxContractCount
  - Definitions/Definition - MyObjectBuilder_StationsListDefinition
    - Id (Type=MyObjectBuilder_StationsListDefinition, SubtypeId=MiningStations)
    - Id (Type=MyObjectBuilder_StationsListDefinition, SubtypeId=OrbitalStations)
    - Id (Type=MyObjectBuilder_StationsListDefinition, SubtypeId=Outposts)
    - Id (Type=MyObjectBuilder_StationsListDefinition, SubtypeId=SpaceStations)
    - StationNames/PrefabName

### Ships for sale

code: `MyGenericFactionTypeStrategy.GenerateGridOffers()`
