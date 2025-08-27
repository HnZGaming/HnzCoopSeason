using System;
using System.Linq;
using GridStorage.API;
using HnzUtils;
using HnzUtils.Pools;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason
{
    // NOTE this module runs in both server/clients.
    public sealed class CoopGridTakeover
    {
        public static readonly CoopGridTakeover Instance = new CoopGridTakeover();

        readonly SceneEntityObserver<IMyCubeGrid> _gridObserver = new SceneEntityObserver<IMyCubeGrid>(true);

        public event Action<long> OnTakeoverStateChanged;

        public void Load()
        {
            _gridObserver.OnEntityAdded += OnGridAdded;
            _gridObserver.OnEntityRemoved += OnEntityRemoved;
            _gridObserver.Load();
        }

        public void Unload()
        {
            _gridObserver.OnEntityAdded -= OnGridAdded;
            _gridObserver.OnEntityRemoved -= OnEntityRemoved;
            _gridObserver.Unload();
        }

        public void FirstUpdate()
        {
            _gridObserver.EnumerateScene();
        }

        public static long GetPlayerGroup(long playerId)
        {
            var playerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId);
            return playerFaction?.FactionId ?? playerId;
        }

        public static bool TryLoadTakeoverState(IMyCubeGrid grid, out TakeoverState state)
        {
            state = null;

            string str;
            if (!grid.TryGetStorageValue(TakeoverState.ModStorageKey, out str)) return false;

            state = MyAPIGateway.Utilities.SerializeFromXML<TakeoverState>(str);
            return true;
        }

        void OnGridAdded(IMyCubeGrid grid)
        {
            grid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;
            OnBlockOwnershipChanged(grid);
        }

        void OnEntityRemoved(IMyCubeGrid grid)
        {
            grid.OnBlockOwnershipChanged -= OnBlockOwnershipChanged;
        }

        void OnBlockOwnershipChanged(IMyCubeGrid grid)
        {
            try
            {
                var state = ComputeTakeover(grid);
                MyLog.Default.Info($"[HnzCoopSeason] takeover state updated; grid: '{grid.DisplayName}' -> {state.CanTakeOver}, {state.TakeoverPlayerGroup}");

                var stateXml = MyAPIGateway.Utilities.SerializeToXML(state);
                grid.UpdateStorageValue(TakeoverState.ModStorageKey, stateXml);
            }
            catch (Exception e)
            {
                MyLog.Default.Error($"[HnzCoopSeason] failed to update takeover storage; error: {e}");
                return;
            }

            OnTakeoverStateChanged?.Invoke(grid.EntityId);
        }

        static TakeoverState ComputeTakeover(IMyCubeGrid grid)
        {
            LangUtils.AssertNull(grid, "grid null");

            var state = new TakeoverState
            {
                CanTakeOver = false,
                TakeoverPlayerGroup = 0,
                Controllers = Array.Empty<long>(),
            };

            // owned by nobody -> can take over
            if ((grid.BigOwners?.Count ?? 0) == 0)
            {
                state.CanTakeOver = true;
                return state;
            }

            // owned by player -> can't take over
            var gridOwnerId = grid.BigOwners[0];
            if (VRageUtils.GetOwnerType(gridOwnerId) == GridOwnerType.Player) return state;

            // control-type blocks
            var blocks = HashSetPool<IMyTerminalBlock>.Instance.Get();
            blocks.UnionWith(grid.GetFatBlocks<IMyRemoteControl>());
            blocks.UnionWith(grid.GetFatBlocks<IMyCockpit>().Where(b => b.CanControlShip));

            state.Controllers = blocks.Select(b => ToPlayerGroupId(b.OwnerId)).ToArray();
            MyLog.Default.Debug($"[HnzCoopSeason] ownership for '{grid.DisplayName}': {state.Controllers.ToStringSeq()}, blocks: {blocks.Select(b => b.BlockDefinition.SubtypeId).ToStringSeq()}");

            HashSetPool<IMyTerminalBlock>.Instance.Release(blocks);

            // no control blocks -> can take over
            if (state.Controllers.Length == 0)
            {
                state.CanTakeOver = true;
                return state;
            }

            var uniquePlayerGroups = HashSetPool<long>.Instance.Get();
            uniquePlayerGroups.UnionWith(state.Controllers);
            uniquePlayerGroups.Remove(0); // !!

            // all control blocks are unowned -> can take over
            if (uniquePlayerGroups.Count == 0)
            {
                state.CanTakeOver = true;
                return state;
            }

            // one player group (not NPC) owns all control blocks -> they can take over
            long singlePlayerGroup;
            if (uniquePlayerGroups.Count == 1 &&
                uniquePlayerGroups.TryGetFirst(out singlePlayerGroup) &&
                !IsNpc(singlePlayerGroup))
            {
                state.CanTakeOver = true;
                state.TakeoverPlayerGroup = singlePlayerGroup;
                return state;
            }

            HashSetPool<long>.Instance.Release(uniquePlayerGroups);

            // multiple player groups own control blocks -> can't take over
            return state;
        }

        static long ToPlayerGroupId(long ownerId)
        {
            if (ownerId == 0) return 0;

            var faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId);
            return faction?.FactionId ?? ownerId;
        }

        static bool IsNpc(long playerGroup)
        {
            var faction = MyAPIGateway.Session.Factions.TryGetFactionById(playerGroup);
            if (faction == null) return false; // player that doesn't partake in a faction

            return faction.FactionType != MyFactionTypes.PlayerMade;
        }
    }
}