using System;
using System.Collections.Generic;
using System.Linq;
using FlashGps;
using HnzCoopSeason.POI;
using HnzCoopSeason.Spawners;
using HnzUtils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Library.Utils;
using VRage.ModAPI;
using VRage.Serialization;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Merchants
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
        readonly PoiMerchantConfig[] _configs;
        long _safeZoneId;
        IMyCubeGrid _grid;
        PoiState _poiState;
        SpawnState _spawnState;
        IMyProjector _shipyard;

        public PoiMerchant(string poiId, Vector3D position, IMyFaction faction, PoiMerchantConfig[] configs)
        {
            _poiId = poiId;
            _position = position;
            _faction = faction;
            _variableKey = $"HnzCoopSeason.PoiMerchant.{_poiId}";
            _economyInterval = new Interval();
            _configs = configs;
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
            UpdateShipyardSignal();
        }

        void UpdateStation()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % 60 != 0) return;
            if (_poiState != PoiState.Released) return;
            if (_spawnState != SpawnState.Idle) return;

            var sphere = new BoundingSphereD(_position, SessionConfig.Instance.EncounterRadius);
            if (!OnlineCharacterCollection.ContainsPlayer(sphere)) return;

            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} player nearby");

            Spawn(Math.Abs(_poiId.GetHashCode()));
        }

        void UpdateEconomy()
        {
            if (_grid == null) return;
            if (_grid.Closed) return;

            var i = SessionConfig.Instance.EconomyUpdateIntervalMinutes * 60 * 60;
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

        bool IPoiObserver.TryGetPosition(out Vector3D position)
        {
            var hasGrid = _grid != null && !_grid.Closed;
            if (hasGrid && _poiState == PoiState.Released)
            {
                position = _grid.GetPosition();
                return true;
            }

            position = default(Vector3D);
            return false;
        }

        public void Spawn(int configIndex)
        {
            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} Spawn()");

            Despawn();

            var config = _configs[configIndex % _configs.Length];

            var matrixBuilder = new SpawnMatrixBuilder
            {
                Sphere = new BoundingSphereD(_position, SessionConfig.Instance.EncounterRadius),
                Clearance = SessionConfig.Instance.EncounterClearance,
                SnapToVoxel = config.SpawnType == SpawnType.PlanetaryStation,
                Count = 1,
                PlayerPosition = null,
            };

            if (!matrixBuilder.TryBuild())
            {
                MyLog.Default.Error($"[HnzCoopSeason] poi merchant {_poiId} failed to find position for spawning");
                return;
            }


            var matrix = matrixBuilder.Results[0];
            // matrix.Translation += matrix.Up * config.OffsetY; // Move up

            try
            {
                _spawnState = SpawnState.Processing;

                var resultGrids = new List<IMyCubeGrid>();
                var ownerId = _faction.FounderId;
                MyAPIGateway.PrefabManager.SpawnPrefab(
                    resultGrids, config.Prefab, matrix.Translation, matrix.Forward, matrix.Up,
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

            var grid = resultGrids[0];
            OnGridSet(grid, false);
        }

        void OnGridSet(IMyCubeGrid grid, bool recovery)
        {
            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} grid set");
            _grid = grid;
            _grid.OnClose += OnGridClosed;
            _grid.UpdateStorageValue(StorageKey, _poiId);
            var blocks = _grid.GetFatBlocks<IMyStoreBlock>();
            var firstBlock = blocks.FirstOrDefault();
            var pos = _grid.WorldMatrix.Translation;
            var gravDown = VRageUtils.CalculateNaturalGravity(pos);
            var gravA = gravDown.Length();

            MatrixD aligned_matrix;
            if (firstBlock != null && gravA > 0) // On planet grav field only
            {
                aligned_matrix = firstBlock.WorldMatrix;

                var gridUp = -gravDown / gravA; // Norm
                var gridRight = aligned_matrix.Right;
                var gridForward = Vector3D.Cross(gridUp, gridRight);

                var newMat = new MatrixD(_grid.WorldMatrix);
                newMat.Forward = gridForward;
                newMat.Right = gridRight;
                newMat.Up = gridUp;

                var planet = MyGamePruningStructure.GetClosestPlanet(pos);
                var surfPts = planet.GetClosestSurfacePointGlobal(pos);
                newMat.Translation = surfPts;

                _grid.SetWorldMatrix(newMat);
            }
            else
            {
                MyLog.Default.Warning($"[HnzCoopSeason] poi merchant {_poiId} alignment store block not found!!");
            }

            _spawnState = SpawnState.Success;

            if (!recovery)
            {
                // change name
                grid.CustomName = $"[{_faction.Tag}] {grid.CustomName}";
            }

            ActivateShipyards(grid);

            UpdateStore(!recovery);
            SetUpSafezone();
            SaveToSandbox();

            if (!recovery) // new spawn
            {
                Session.Instance.OnMerchantDiscovered(_poiId, grid.GetPosition());
            }
        }

        void ActivateShipyards(IMyCubeGrid grid)
        {
            _shipyard = grid.GetFatBlocks<IMyProjector>().FirstOrDefault(b => b.BlockDefinition.SubtypeId == "MES-Blocks-ShipyardTerminal");
            if (_shipyard == null)
            {
                MyLog.Default.Error("[HnzCoopSeason] shipyard not found");
                return;
            }

            var storage = _shipyard.Storage = new MyModStorageComponent();
            storage.SetValue(Guid.Parse("88334d52-3f3b-47cb-83c7-426fbc0553fa"), "MERC-Shipyard-Profile");

            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} shipyard: '{_shipyard.EntityId}'");
        }

        void Despawn()
        {
            if (_grid == null) return;

            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} grid closing");

            _grid.Close();
            _grid = null;
        }

        void OnGridClosed(IMyEntity grid)
        {
            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} grid closed");
            grid.OnClose -= OnGridClosed;
            _spawnState = SpawnState.Idle;
            _shipyard = null;
            _grid = null;
            RemoveSafezone();
        }

        void SetUpSafezone()
        {
            MySafeZone safezone;
            if (VRageUtils.TryGetEntityById(_safeZoneId, out safezone))
            {
                RemoveSafezone();
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
                MyLog.Default.Warning($"[HnzCoopSeason] poi merchant {_poiId} safezone not found; safezone id: {_safeZoneId}");
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
            if (_grid.Closed) return;

            foreach (var battery in _grid.GetFatBlocks<MyBatteryBlock>())
            {
                battery.CurrentStoredPower = battery.MaxStoredPower;
            }
        }

        void UpdateShipyardSignal()
        {
            if (_grid == null) return;
            if (_grid.Closed) return;
            if (_shipyard == null) return;

            const int DurationSecs = 10;
            if (MyAPIGateway.Session.GameplayFrameCounter % 60 * DurationSecs != 0) return;

            FlashGpsApi.Send(new FlashGpsApi.Entry
            {
                Id = _shipyard.EntityId,
                EntityId = _shipyard.EntityId,
                Name = "- Shipyard Block -\n    Buying grids!",
                Position = _shipyard.GetPosition(),
                Color = Color.Cyan,
                Duration = DurationSecs,
                Radius = 1000,
                Mute = true,
                Description = "Shipyard block allows you to sell grids for space credits"
            });

            MyLog.Default.Debug($"[HnzCoopSeason] poi merchant {_poiId} shipyard signal sent");
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