using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace HnzPveSeason
{
    public sealed class Economy
    {
        public static readonly Economy Instance = new Economy();

        readonly Dictionary<string, EconomyFaction> _factions;
        readonly List<string> _factionTypes;

        Economy()
        {
            _factions = new Dictionary<string, EconomyFaction>();
            _factionTypes = new List<string>();
        }

        public void Load()
        {
            MyLog.Default.Error("[HnzPveSeason] Economy.Load()");
            
            foreach (var def in MyDefinitionManager.Static.GetAllDefinitions<MyFactionTypeDefinition>())
            {
                var factionType = def.Id.SubtypeName;
                var myFaction = GetMerchantFactionByType(factionType);
                if (myFaction == null) continue; // pirate

                _factionTypes.Add(factionType);
                _factions[factionType] = new EconomyFaction(def, myFaction);
                MyLog.Default.Info($"[HnzPveSeason] Economy.Load() {factionType}, {myFaction.Tag}");
            }
        }

        public EconomyFaction GetFaction(int index)
        {
            var factionType = _factionTypes[index % _factionTypes.Count];
            return _factions[factionType];
        }

        static IMyFaction GetMerchantFactionByType(string type)
        {
            switch (type)
            {
                case "Trader": return MyAPIGateway.Session.Factions.TryGetFactionByTag("TRADER");
                case "Miner": return MyAPIGateway.Session.Factions.TryGetFactionByTag("MINER");
                case "Builder": return MyAPIGateway.Session.Factions.TryGetFactionByTag("BUILDER");
                case "Military": return MyAPIGateway.Session.Factions.TryGetFactionByTag("MILITARY");
                default: return null; // pirate
            }
        }
    }
}