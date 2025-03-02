using System;
using System.Collections.Generic;
using System.Linq;
using HnzPveSeason.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Serialization;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class PoiMerchant : IPoiObserver
    {
        const float SafezoneRadius = 75f;
        readonly string _poiId;
        readonly Vector3D _position;
        readonly MesStaticEncounter _encounter;
        readonly string _variableKey;
        readonly EconomyFaction _faction;
        readonly Interval _economyInterval;
        HashSet<long> _contractIds;
        long _safeZoneId;
        IMyCubeGrid _grid;

        public PoiMerchant(string poiId, Vector3D position, MesStaticEncounterConfig[] configs, EconomyFaction faction)
        {
            _poiId = poiId;
            _position = position;
            _faction = faction;
            _encounter = new MesStaticEncounter($"{poiId}-merchant", "[MERCHANTS]", configs, position, _faction.Tag, true);
            _variableKey = $"HnzPveSeason.PoiMerchant.{_poiId}";
            _contractIds = new HashSet<long>();
            _economyInterval = new Interval();
        }

        public int LastPlayerVisitFrame { get; private set; }

        void IPoiObserver.Load(IMyCubeGrid[] grids)
        {
            _encounter.OnGridSet += OnGridSet;
            _encounter.OnGridUnset += OnGridUnset;

            LoadFromSandbox();

            _encounter.Load(grids, true, false);
            _economyInterval.Initialize();
        }

        void IPoiObserver.Unload(bool sessionUnload)
        {
            _encounter.Unload(sessionUnload);

            _encounter.OnGridSet -= OnGridSet;
            _encounter.OnGridUnset -= OnGridUnset;
        }

        void IPoiObserver.Update()
        {
            _encounter.Update();

            UpdateLastVisitedTime();

            // update economy
            if (_grid != null && _economyInterval.Update(SessionConfig.Instance.EconomyUpdateInterval * 60))
            {
                UpdateStore();
                UpdateContracts();
            }
        }

        void IPoiObserver.OnStateChanged(PoiState state)
        {
            _encounter.SetActive(state == PoiState.Released);

            if (state == PoiState.Occupied)
            {
                _encounter.Despawn();
            }

            if (state == PoiState.Released)
            {
                LastPlayerVisitFrame = MyAPIGateway.Session.GameplayFrameCounter;
            }
        }

        void OnGridSet(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} merchant grid set");
            _grid = grid;

            UpdateStore();
            UpdateContracts();
            SetUpSafezone();
            SaveToSandbox();
        }

        void OnGridUnset(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} merchant grid unset");

            if (_grid != grid)
            {
                throw new InvalidOperationException("unsetting grid that isn't set");
            }

            _grid = null;

            // DisposeContracts(); // note: let the game handle this
            RemoveSafezone();
            SaveToSandbox();
        }

        void UpdateLastVisitedTime()
        {
            // every 10 seconds
            if (MyAPIGateway.Session.GameplayFrameCounter % (60 * 10) != 0) return;

            var poi = Session.Instance.GetPoi(_poiId);
            if (poi.State != PoiState.Released) return;

            var sphere = new BoundingSphereD(_position, _encounter.Config.Area);
            if (!OnlineCharacterCollection.ContainsPlayer(sphere)) return;

            LastPlayerVisitFrame = MyAPIGateway.Session.GameplayFrameCounter;
            MyLog.Default.Debug($"[HnzPveSeason] POI {_poiId} merchant last visit time updated");
        }

        void UpdateContracts()
        {
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} update contracts");

            // find contract blocks
            IMyCubeBlock block;
            if (!TryGetSingleBlock(_grid, b => b?.IsContractBlock() ?? false, out block))
            {
                MyLog.Default.Error($"[HnzPveSeason] POI {_poiId} invalid contract block count");
                return;
            }

            if (!_faction.IsMyBlock(block))
            {
                MyLog.Default.Error($"[HnzPveSeason] POI {_poiId} invalid contract block ownership");
                return;
            }

            _faction.UpdateContracts(block.EntityId, _contractIds);
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} contracts updated");
        }

        void SetUpSafezone()
        {
            MySafeZone safezone;
            if (VRageUtils.TryGetEntityById(_safeZoneId, out safezone))
            {
                MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} safezone already exists");
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
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} safezone created");
        }

        void RemoveSafezone()
        {
            MySafeZone safezone;
            if (!VRageUtils.TryGetEntityById(_safeZoneId, out safezone))
            {
                MyLog.Default.Warning($"[HnzPveSeason] POI {_poiId} safezone not found");
                return;
            }

            MySessionComponentSafeZones.RemoveSafeZone(safezone);
            safezone.Close();
            MyEntities.Remove(safezone);

            _safeZoneId = 0;
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} safezone removed");
        }

        void UpdateStore()
        {
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} update store items");

            // sell tech comps
            IMyStoreBlock storeBlock;
            if (!TryGetSingleBlock(_grid, b => b?.IsStoreBlock() ?? false, out storeBlock))
            {
                MyLog.Default.Error($"[HnzPveSeason] POI {_poiId} invalid store block count");
                return;
            }

            if (!_faction.IsMyBlock(storeBlock))
            {
                MyLog.Default.Error($"[HnzPveSeason] POI {_poiId} invalid store block ownership");
                return;
            }

            _faction.UpdateStoreItems(storeBlock);
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} store initialized");
        }

        void SaveToSandbox()
        {
            var data = new SerializableDictionary<string, object>
            {
                [nameof(_contractIds)] = string.Join(";", _contractIds),
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
                _contractIds = dictionary.GetValueOrDefault(nameof(_contractIds), "0").Split(',').Select(long.Parse).ToSet();
                _safeZoneId = dictionary.GetValueOrDefault(nameof(_safeZoneId), (long)0);
            }
        }

        static bool TryGetSingleBlock<T>(IMyCubeGrid grid, Func<IMyCubeBlock, bool> f, out T block) where T : class, IMyCubeBlock
        {
            block = null;
            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks, b => f(b.FatBlock));
            if (blocks.Count != 1) return false;

            block = (T)blocks[0].FatBlock;
            return true;
        }
    }
}