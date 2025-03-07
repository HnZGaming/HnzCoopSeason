using System;
using System.Collections.Generic;
using HnzCoopSeason.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Library.Utils;
using VRage.Serialization;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class PoiMerchant : IPoiObserver
    {
        const float SafezoneRadius = 75f;
        readonly string _poiId;
        readonly MesStaticEncounter _encounter;
        readonly string _variableKey;
        readonly Interval _economyInterval;
        long _safeZoneId;
        IMyCubeGrid _grid;

        public PoiMerchant(string poiId, Vector3D position, IMyFaction faction, MesStaticEncounterConfig[] configs)
        {
            _poiId = poiId;
            _variableKey = $"HnzCoopSeason.PoiMerchant.{_poiId}";
            _economyInterval = new Interval();

            var config = configs[poiId.GetHashCode() % configs.Length];
            _encounter = new MesStaticEncounter($"{poiId}-merchant", "[MERCHANTS]", new[] { config }, position, faction.Tag, true);
        }

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
        }

        void OnGridSet(IMyCubeGrid grid, bool recovery)
        {
            MyLog.Default.Info($"[HnzCoopSeason] POI {_poiId} merchant grid set");
            _grid = grid;

            UpdateStore();
            UpdateContracts();
            SetUpSafezone();
            SaveToSandbox();

            if (!recovery) // new spawn
            {
                Session.Instance.OnMerchantDiscovered(_poiId, grid.GetPosition());
            }
        }

        void OnGridUnset(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzCoopSeason] POI {_poiId} merchant grid unset");

            if (_grid != grid)
            {
                throw new InvalidOperationException("unsetting grid that isn't set");
            }

            _grid = null;

            // DisposeContracts(); // note: let the game handle this
            RemoveSafezone();
            SaveToSandbox();
        }

        void UpdateContracts()
        {
            MyLog.Default.Info($"[HnzCoopSeason] POI {_poiId} update contracts");

            // find contract blocks
            IMyCubeBlock block;
            if (!TryGetSingleBlock(_grid, b => b?.IsContractBlock() ?? false, out block))
            {
                //MyLog.Default.Error($"[HnzCoopSeason] POI {_poiId} invalid contract block count");
                return;
            }

            // disable contract blocks until we implement custom contracts
            ((IMyFunctionalBlock)block).Enabled = false;
        }

        void SetUpSafezone()
        {
            MySafeZone safezone;
            if (VRageUtils.TryGetEntityById(_safeZoneId, out safezone))
            {
                MyLog.Default.Info($"[HnzCoopSeason] POI {_poiId} safezone already exists");
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
            MyLog.Default.Info($"[HnzCoopSeason] POI {_poiId} safezone created");
        }

        void RemoveSafezone()
        {
            MySafeZone safezone;
            if (!VRageUtils.TryGetEntityById(_safeZoneId, out safezone))
            {
                MyLog.Default.Warning($"[HnzCoopSeason] POI {_poiId} safezone not found");
                return;
            }

            MySessionComponentSafeZones.RemoveSafeZone(safezone);
            safezone.Close();
            MyEntities.Remove(safezone);

            _safeZoneId = 0;
            MyLog.Default.Info($"[HnzCoopSeason] POI {_poiId} safezone removed");
        }

        void UpdateStore()
        {
            MyLog.Default.Info($"[HnzCoopSeason] POI {_poiId} update store items");

            IMyStoreBlock storeBlock;
            if (!TryGetSingleBlock(_grid, b => b?.IsStoreBlock() ?? false, out storeBlock))
            {
                MyLog.Default.Error($"[HnzCoopSeason] POI {_poiId} invalid store block count");
                return;
            }

            var allExistingItems = new List<IMyStoreItem>();
            storeBlock.GetStoreItems(allExistingItems);

            var existingOffers = new Dictionary<MyDefinitionId, int>();
            var existingOrders = new Dictionary<MyDefinitionId, int>();
            foreach (var item in allExistingItems)
            {
                if (item.Item == null) continue; // shouldn't happen

                var id = item.Item.Value.ToDefinitionId();
                var dic = item.StoreItemType == StoreItemTypes.Offer ? existingOffers : existingOrders;
                dic[id] = item.Amount;
            }

            storeBlock.ClearItems();

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

                MyLog.Default.Debug("[HnzCoopSeason] UpdateStoreItems() offer; item: {0}, origin: {1}, delta: {2}", id, existingAmount, amount, fillAmount);
            }
        }

        public void ForceSpawn()
        {
            _encounter.ForceSpawn(0);
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

        static bool TryGetSingleBlock<T>(IMyCubeGrid grid, Func<IMyCubeBlock, bool> f, out T block) where T : class, IMyCubeBlock
        {
            block = null;
            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks, b => f(b.FatBlock));
            if (blocks.Count != 1) return false;

            block = (T)blocks[0].FatBlock;
            return true;
        }

        public override string ToString()
        {
            return $"Merchant({nameof(_poiId)}: {_poiId}, {nameof(_encounter)}: {_encounter})";
        }
    }
}