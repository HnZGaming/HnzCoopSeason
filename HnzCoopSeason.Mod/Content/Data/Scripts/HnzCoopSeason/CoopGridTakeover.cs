using System;
using System.Linq;
using GridStorage.API;
using HnzUtils;
using HnzUtils.Pools;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason
{
    public sealed class CoopGridTakeover
    {
        public static readonly CoopGridTakeover Instance = new CoopGridTakeover();

        readonly SceneEntityObserver<IMyCubeGrid> _gridObserver = new SceneEntityObserver<IMyCubeGrid>();

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
            var state = ComputeTakeover(grid);
            //MyLog.Default.Info($"[HnzCoopSeason] takeover state updated; grid: '{grid.DisplayName}' -> {state.CanTakeOver}, {state.TakeoverPlayerGroup}, {state.PlayerGroups.ToStringSeq()}");
            var stateXml = MyAPIGateway.Utilities.SerializeToXML(state);
            grid.UpdateStorageValue(TakeoverState.ModStorageKey, stateXml);
        }

        static TakeoverState ComputeTakeover(IMyCubeGrid grid)
        {
            LangUtils.AssertNull(grid, "grid null");

            var state = new TakeoverState();

            // owned by nobody -> can take over
            if ((grid.BigOwners?.Count ?? 0) == 0)
            {
                state.CanTakeOver = true;
                return state;
            }

            // owned by player -> can't take over
            var gridOwnerId = grid.BigOwners[0];
            if (VRageUtils.GetOwnerType(gridOwnerId) == GridOwnerType.Player)
            {
                state.CanTakeOver = false;
                return state;
            }

            // control-type blocks
            var blocks = HashSetPool<IMyTerminalBlock>.Instance.Get();
            blocks.UnionWith(grid.GetFatBlocks<IMyRemoteControl>());
            blocks.UnionWith(grid.GetFatBlocks<IMyCockpit>());

            state.PlayerGroups = blocks.Select(b => ToPlayerGroupId(b.OwnerId)).ToArray();
            MyLog.Default.Info($"[HnzCoopSeason] ownership: {state.PlayerGroups.ToStringSeq()}");

            HashSetPool<IMyTerminalBlock>.Instance.Release(blocks);

            // no control blocks -> can take over
            if (state.PlayerGroups.Length == 0)
            {
                state.CanTakeOver = true;
                return state;
            }

            var uniquePlayerGroups = HashSetPool<long>.Instance.Get();
            uniquePlayerGroups.UnionWith(state.PlayerGroups);
            uniquePlayerGroups.Remove(0); // !!

            // all control blocks are unowned -> can take over
            if (uniquePlayerGroups.Count == 0)
            {
                state.CanTakeOver = true;
                return state;
            }

            // one player group owns all control blocks -> they can take over
            if (uniquePlayerGroups.Count == 1)
            {
                state.CanTakeOver = true;
                state.TakeoverPlayerGroup = uniquePlayerGroups.First();
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
    }
}