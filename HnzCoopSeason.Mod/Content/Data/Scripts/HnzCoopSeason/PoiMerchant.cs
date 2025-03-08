﻿using System;
using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Library.Utils;
using VRage.ModAPI;
using VRage.ObjectBuilders;
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
            _config = configs[poiId.GetHashCode() % configs.Length];
        }

        void IPoiObserver.Load(IMyCubeGrid[] grids)
        {
            LoadFromSandbox();
            _economyInterval.Initialize();

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

            if (state == PoiState.Occupied)
            {
                Despawn();
            }
        }

        public void Spawn()
        {
            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} Spawn()");

            Despawn();

            var sphere = new BoundingSphereD(_position, SessionConfig.Instance.EncounterRadius);
            var clearance = SessionConfig.Instance.EncounterClearance;
            MatrixD matrix;
            if (!SpawnUtils.TryCalcMatrix(_config.SpawnType, sphere, clearance, out matrix))
            {
                MyLog.Default.Error($"[HnzCoopSeason] poi merchant {_poiId} failed to find position for spawning");
                return;
            }

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
                    callback: () => OnGridSpawned(resultGrids, matrix));
            }
            catch (Exception e)
            {
                _spawnState = SpawnState.Failure;
                MyLog.Default.Error($"[HnzCoopSeason] poi merchant {_poiId} failed to spawn: {e}");
            }
        }

        void OnGridSpawned(List<IMyCubeGrid> resultGrids, MatrixD matrix)
        {
            if (resultGrids.Count == 0)
            {
                MyLog.Default.Error($"[HnzCoopSeason] poi merchant {_poiId} failed to spawn via SpawnPrefab()");
                _spawnState = SpawnState.Failure;

                // debug
                var gps = MyAPIGateway.Session.GPS.Create(_config.Prefab, "", matrix.Translation, true);
                MyAPIGateway.Session.GPS.AddLocalGps(gps);
                return;
            }

            // name manipulation
            var grid = resultGrids[0];
            grid.CustomName = $"[{_faction.Tag}] {grid.CustomName}";

            OnGridSet(grid, false);
        }

        void OnGridSet(IMyCubeGrid grid, bool recovery)
        {
            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} grid set");
            _grid = grid;
            _grid.OnClose += OnGridClosed;
            _grid.UpdateStorageValue(StorageKey, _poiId);
            _spawnState = SpawnState.Success;

            UpdateStore();
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

        public void UpdateStore()
        {
            if (_grid == null) return;

            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} update store items");

            var storeBlocks = _grid.GetFatBlocks<IMyStoreBlock>().Where(b => IsStoreBlock(b)).ToArray();
            if (storeBlocks.Length != 1)
            {
                MyLog.Default.Error($"[HnzCoopSeason] poi merchant {_poiId} invalid store block count");
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
            foreach (var c in SessionConfig.Instance.StoreItems)
            {
                MyDefinitionId id;
                if (!MyDefinitionId.TryParse($"{c.Type}/{c.Subtype}", out id))
                {
                    MyLog.Default.Error($"[HnzCoopSeason] invalid store item config: {c}");
                    continue;
                }

                var existingAmount = existingOffers.GetValueOrDefault(id, 0);
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
            return !(block is IMyVendingMachine);
        }

        static bool IsCargoBlock(IMyCargoContainer block)
        {
            return block.CustomName.Contains("Cargo");
        }

        public override string ToString()
        {
            return $"Merchant({nameof(_poiId)}: {_poiId}, {nameof(_spawnState)}: {_spawnState}, {nameof(_grid)}: '{_grid.Name}')";
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