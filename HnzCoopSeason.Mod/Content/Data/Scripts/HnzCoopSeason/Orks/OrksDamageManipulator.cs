using System;
using HnzUtils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Orks
{
    public sealed class OrksDamageManipulator
    {
        readonly string _factionTag;
        long _factionFounderId;
        float _scale = 1f;

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
            var level = Session.Instance.GetProgressLevel();
            var c = SessionConfig.Instance.GetProgressionLevel(level);
            _scale = MathHelper.Lerp(c.OrksDamageReductionScaleStart, c.OrksDamageReductionScaleEnd, Session.Instance.GetProgressLevelFraction());
        }

        void BeforeDamage(object target, ref MyDamageInformation info)
        {
            var block = target as IMySlimBlock;
            if (block == null) return;

            long ownerId;
            if (!block.CubeGrid.BigOwners.TryGetElementAt(0, out ownerId)) return;
            if (ownerId != _factionFounderId) return;

            // per-grid scale frozen at spawn time; grids from before a restart fall back to the session level
            float scale;
            if (!OrkDamageReductionScales.TryGet(block.CubeGrid, out scale))
            {
                scale = _scale;
            }

            info.Amount *= 1f / Math.Max(scale, 1f);
        }
    }
}