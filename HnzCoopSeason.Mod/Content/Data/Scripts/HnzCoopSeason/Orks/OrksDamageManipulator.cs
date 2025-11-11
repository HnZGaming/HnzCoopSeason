using System;
using HnzUtils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason.Orks
{
    public sealed class OrksDamageManipulator
    {
        readonly string _factionTag;
        long _factionFounderId;
        int _level;


        public OrksDamageManipulator(string factionTag)
        {
            _factionTag = factionTag;
        }

        public void OnFirstFrame()
        {
            var faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(_factionTag);
            if (faction == null)
            {
                throw new InvalidOperationException($"Faction not found: '{_factionTag}'");
            }

            _factionFounderId = faction.FounderId;
            MyLog.Default.Info($"[HnzCoopSeason] orks damage manipulation; orks founder: {_factionFounderId}");

            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, BeforeDamage);
        }

        public void OnEveryFrame()
        {
            _level = Session.Instance.GetProgressLevel();
        }

        void BeforeDamage(object target, ref MyDamageInformation info)
        {
            var block = target as IMySlimBlock;
            if (block == null) return;

            long ownerId;
            if (!block.CubeGrid.BigOwners.TryGetElementAt(0, out ownerId)) return;
            if (ownerId != _factionFounderId) return;

            var magnitude = _level * SessionConfig.Instance.OrksDamageManipulationScale;
            info.Amount *= 1f / Math.Max(magnitude, 1f);
        }
    }
}