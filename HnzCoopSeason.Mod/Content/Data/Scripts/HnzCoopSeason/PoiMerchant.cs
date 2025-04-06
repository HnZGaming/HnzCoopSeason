using System;
using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Library.Utils;
using VRage.ModAPI;
using VRage.Serialization;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class PoiMerchant : IPoiObserver
    {
        const float SafezoneRadius = 75f;
        static readonly Guid StorageKey = Guid.Parse("8e562067-5807-49a0-9d7d-108febcece97");

        readonly string _poiId;
        readonly Vector3D _position;
        readonly IMyFaction _faction;
        readonly string _variableKey;
        readonly Interval _economyInterval;
        readonly PoiMerchantConfig _config;
        long _safeZoneId;
        IMyCubeGrid _grid;
        PoiState _poiState;
        SpawnState _spawnState;

        public PoiMerchant(string poiId, Vector3D position, IMyFaction faction, PoiMerchantConfig[] configs)
        {
            _poiId = poiId;
            _position = position;
            _faction = faction;
            _variableKey = $"HnzCoopSeason.PoiMerchant.{_poiId}";
            _economyInterval = new Interval();
            _config = configs[Math.Abs(poiId.GetHashCode()) % configs.Length];
        }

        void IPoiObserver.Load(IMyCubeGrid[] grids)
        {
            LoadFromSandbox();
            _economyInterval.Initialize();

            // load existing grid
            var myGrids = grids.Where(g => IsMyGrid(g)).ToArray();
            for (var i = 0; i < myGrids.Length; i++)
            {
                if (i > 0) // shouldn't happen
                {
                    myGrids[i].Close();
                    MyLog.Default.Error($"[HnzCoopSeason] poi merchant {_poiId} found multiple grids; closing except one");
                    continue;
                }

                MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} recovering existing grid");
                OnGridSet(myGrids[i], true);
            }
        }

        void IPoiObserver.Unload(bool sessionUnload)
        {
            if (!sessionUnload)
            {
                Despawn();
            }
        }

        void IPoiObserver.Update()
        {
            UpdateStation();
            UpdateEconomy();
            UpdatePower();
        }

        void UpdateStation()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % 60 != 0) return;
            if (_poiState != PoiState.Released) return;
            if (_spawnState != SpawnState.Idle) return;

            var sphere = new BoundingSphereD(_position, SessionConfig.Instance.EncounterRadius);
            if (!OnlineCharacterCollection.ContainsPlayer(sphere)) return;

            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} player nearby");

            Spawn();
        }

        void UpdateEconomy()
        {
            if (_grid == null) return;

            var i = SessionConfig.Instance.EconomyUpdateInterval * 60;
            if (!_economyInterval.Update(i)) return;

            UpdateStore();
        }

        void IPoiObserver.OnStateChanged(PoiState state)
        {
            _poiState = state;

            if (state != PoiState.Released)
            {
                Despawn();
            }
        }

        public void Spawn()
        {
            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} Spawn()");

            Despawn();

            var matrixBuilder = new SpawnMatrixBuilder
            {
                Sphere = new BoundingSphereD(_position, SessionConfig.Instance.EncounterRadius),
                Clearance = SessionConfig.Instance.EncounterClearance,
                SnapToVoxel = _config.SpawnType == SpawnType.PlanetaryStation,
                Count = 1,
                PlayerPosition = null,
            };

            if (!matrixBuilder.TryBuild())
            {
                MyLog.Default.Error($"[HnzCoopSeason] poi merchant {_poiId} failed to find position for spawning");
                return;
            }

            var matrix = matrixBuilder.Results[0];
            matrix.Translation += matrix.Up * _config.OffsetY;

            try
            {
                _spawnState = SpawnState.Processing;

                var resultGrids = new List<IMyCubeGrid>();
                var ownerId = _faction.FounderId;
                MyAPIGateway.PrefabManager.SpawnPrefab(
                    resultGrids, _config.Prefab, matrix.Translation, matrix.Forward, matrix.Up,
                    ownerId: ownerId,
                    spawningOptions: SpawningOptions.RotateFirstCockpitTowardsDirection,
                    callback: () => OnGridSpawned(resultGrids));
            }
            catch (Exception e)
            {
                _spawnState = SpawnState.Failure;
                MyLog.Default.Error($"[HnzCoopSeason] poi merchant {_poiId} failed to spawn: {e}");
            }
        }

        void OnGridSpawned(List<IMyCubeGrid> resultGrids)
        {
            if (resultGrids.Count == 0)
            {
                MyLog.Default.Error($"[HnzCoopSeason] poi merchant {_poiId} failed to spawn via SpawnPrefab()");
                _spawnState = SpawnState.Failure;
                return;
            }

            // name manipulation
            var grid = resultGrids[0];
            grid.CustomName = $"[{_faction.Tag}] {grid.CustomName}";
            ReplaceContractBlockWithShipyard(grid);

            OnGridSet(grid, false);
        }

        void ReplaceContractBlockWithShipyard(IMyCubeGrid grid)
        {
            var contractBlock = grid.GetFatBlocks<IMyTerminalBlock>().FirstOrDefault(b => b.IsContractBlock());
            if (contractBlock == null)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] poi merchant {_poiId} no contract blocks found");
                return;
            }

            var ob = contractBlock.GetObjectBuilderCubeBlock(false);
            var shipyardBuilder = new MyObjectBuilder_Projector
            {
                SubtypeName = "MES-Blocks-ShipyardTerminal",
                Name = "Shipyard",
                BlockOrientation = ob.BlockOrientation,
                Min = ob.Min,
                ColorMaskHSV = ob.ColorMaskHSV,
                Owner = ob.Owner,
            };

            grid.RemoveBlock(contractBlock.SlimBlock);

            var shipyard = grid.AddBlock(shipyardBuilder, true).FatBlock;
            var storage = shipyard.Storage = new MyModStorageComponent();
            storage.SetValue(Guid.Parse("88334d52-3f3b-47cb-83c7-426fbc0553fa"), "MERC-Shipyard-Profile");
        }

        void OnGridSet(IMyCubeGrid grid, bool recovery)
        {
            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} grid set");
            _grid = grid;
            _grid.OnClose += OnGridClosed;
            _grid.UpdateStorageValue(StorageKey, _poiId);
            _spawnState = SpawnState.Success;

            UpdateStore(!recovery);
            SetUpSafezone();
            SaveToSandbox();

            // disable contract blocks
            var contractBlocks = new List<IMySlimBlock>();
            _grid.GetBlocks(contractBlocks, b => b.FatBlock?.IsContractBlock() ?? false);
            foreach (var b in contractBlocks)
            {
                ((IMyFunctionalBlock)b.FatBlock).Enabled = false;
            }

            if (!recovery) // new spawn
            {
                Session.Instance.OnMerchantDiscovered(_poiId, grid.GetPosition());
            }
        }

        void Despawn()
        {
            if (_grid == null) return;

            _grid.Close();
            _grid = null;
            RemoveSafezone();
            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} despawned");
        }

        void OnGridClosed(IMyEntity grid)
        {
            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} grid closed");
            grid.OnClose -= OnGridClosed;
            _spawnState = SpawnState.Idle;
        }

        void SetUpSafezone()
        {
            MySafeZone safezone;
            if (VRageUtils.TryGetEntityById(_safeZoneId, out safezone))
            {
                MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} safezone already exists");
                return;
            }

            safezone = (MySafeZone)MySessionComponentSafeZones.CrateSafeZone(
                _grid.WorldMatrix,
                MySafeZoneShape.Sphere,
                MySafeZoneAccess.Blacklist,
                null, null, SafezoneRadius, true, true,
                name: $"poi-{_poiId}");

            MySessionComponentSafeZones.AddSafeZone(safezone);
            _safeZoneId = safezone.EntityId;
            SaveToSandbox();
            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} safezone created");
        }

        void RemoveSafezone()
        {
            MySafeZone safezone;
            if (!VRageUtils.TryGetEntityById(_safeZoneId, out safezone))
            {
                MyLog.Default.Warning($"[HnzCoopSeason] poi merchant {_poiId} safezone not found");
                return;
            }

            MySessionComponentSafeZones.RemoveSafeZone(safezone);
            safezone.Close();
            MyEntities.Remove(safezone);

            _safeZoneId = 0;
            SaveToSandbox();
            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} safezone removed");
        }

        public void UpdateStore(bool fill = false)
        {
            if (_grid == null) return;

            MyLog.Default.Debug("[HnzCoopSeason] poi merchant {0} update store items; fill: {1}", _poiId, fill ? "o" : "x");

            var storeBlocks = _grid.GetFatBlocks<IMyStoreBlock>().Where(b => IsStoreBlock(b)).ToArray();
            if (storeBlocks.Length != 1)
            {
                MyLog.Default.Error($"[HnzCoopSeason] poi merchant {_poiId} invalid store block count; blocks: {storeBlocks.ToStringSeq()}");
                return;
            }

            var cargoBlocks = _grid.GetFatBlocks<IMyCargoContainer>().Where(b => IsCargoBlock(b)).ToArray();
            if (cargoBlocks.Length == 0)
            {
                MyLog.Default.Error($"[HnzCoopSeason] poi merchant {_poiId} no cargo container blocks");
                return;
            }

            var storeBlock = storeBlocks[0];
            var inventory = cargoBlocks[0].GetInventory(0);

            var allExistingItems = new List<IMyStoreItem>();
            storeBlock.GetStoreItems(allExistingItems);

            var existingOffers = new Dictionary<MyDefinitionId, int>();
            foreach (var item in allExistingItems)
            {
                if (item.Item == null) continue; // shouldn't happen
                if (item.StoreItemType != StoreItemTypes.Offer) continue;

                var id = item.Item.Value.ToDefinitionId();
                existingOffers[id] = item.Amount;
            }

            storeBlock.ClearItems();

            var allItems = new Dictionary<MyDefinitionId, int>();
            foreach (var kvp in GetStoreItemConfigs())
            {
                var id = kvp.Key;
                var c = kvp.Value;

                var existingAmount = existingOffers.GetValueOrDefault(id, 0);
                if (fill)
                {
                    existingAmount += c.MaxAmount;
                }

                var fillAmount = (int)MathHelper.Lerp(c.MinAmountPerUpdate, c.MaxAmountPerUpdate, MyRandom.Instance.NextDouble());
                var amount = Math.Min(existingAmount + fillAmount, c.MaxAmount);
                var item = storeBlock.CreateStoreItem(id, amount, c.PricePerUnit, StoreItemTypes.Offer);
                storeBlock.InsertStoreItem(item);
                allItems.Increment(id, amount);

                MyLog.Default.Debug("[HnzCoopSeason] UpdateStoreItems(); item: {0}, origin: {1}, delta: {2}", id, existingAmount, amount, fillAmount);
            }

            inventory.Clear();
            foreach (var kvp in allItems)
            {
                var builder = new MyObjectBuilder_Component { SubtypeName = kvp.Key.SubtypeName };
                inventory.AddItems(kvp.Value, builder);
            }
        }

        void UpdatePower()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % 60 != 0) return;
            if (_grid == null) return;

            foreach (var battery in _grid.GetFatBlocks<MyBatteryBlock>())
            {
                battery.CurrentStoredPower = battery.MaxStoredPower;
            }
        }

        void SaveToSandbox()
        {
            var data = new SerializableDictionary<string, object>
            {
                [nameof(_safeZoneId)] = _safeZoneId,
            };
            MyAPIGateway.Utilities.SetVariable(_variableKey, data);
        }

        void LoadFromSandbox()
        {
            SerializableDictionary<string, object> data;
            if (MyAPIGateway.Utilities.GetVariable(_variableKey, out data))
            {
                var dictionary = data.Dictionary;
                _safeZoneId = dictionary.GetValueOrDefault(nameof(_safeZoneId), (long)0);
            }
        }

        bool IsMyGrid(IMyCubeGrid grid)
        {
            if (grid.Closed) return false;
            if (grid.MarkedForClose) return false;

            string value;
            if (!grid.TryGetStorageValue(StorageKey, out value)) return false;

            //MyLog.Default.Info($"[HnzCoopSeason] grid name: {grid.CustomName}, value: '{value}', to: '{_poiId}'");
            return value == _poiId;
        }

        static bool IsStoreBlock(IMyStoreBlock block)
        {
            return block.BlockDefinition.SubtypeName == "StoreBlock";
        }

        static bool IsCargoBlock(IMyCargoContainer block)
        {
            return block.CustomName.Contains("Cargo");
        }

        static Dictionary<MyDefinitionId, StoreItemConfig> GetStoreItemConfigs()
        {
            var items = SessionConfig.Instance.StoreItems;
            var results = new Dictionary<MyDefinitionId, StoreItemConfig>();
            foreach (var c in items)
            {
                MyDefinitionId id;
                if (!MyDefinitionId.TryParse($"{c.Type}/{c.Subtype}", out id))
                {
                    MyLog.Default.Error($"[HnzCoopSeason] invalid store item config: {c}");
                    continue;
                }

                if (results.ContainsKey(id))
                {
                    MyLog.Default.Error($"[HnzCoopSeason] duplicate store item config: {c}");
                    continue;
                }

                results.Add(id, c);
            }

            return results;
        }

        public override string ToString()
        {
            return $"Merchant({nameof(_poiId)}: {_poiId}, {nameof(_spawnState)}: {_spawnState}, {nameof(_grid)}: '{_grid?.Name ?? "--"}')";
        }

        enum SpawnState
        {
            Idle,
            Processing,
            Success,
            Failure,
        }
    }
}