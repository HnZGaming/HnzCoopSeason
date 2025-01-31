using System;
using System.Text;
using HnzPveSeason.Utils;
using HnzPveSeason.Utils.Commands;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class Session : MySessionComponentBase
    {
        public static Session Instance { get; private set; }

        PoiMap _poiMap;
        PoiGpsCollection _poiGpsCollection;
        CommandModule _commandModule;

        public override void LoadData()
        {
            base.LoadData();
            Instance = this;

            MyLog.Default.Info("[HnzPveSeason] session loading");

            _poiMap = new PoiMap();
            Communication.Load();

            _commandModule = new CommandModule("pve");
            _commandModule.Load();
            _commandModule.Register(new Command("poi list", MyPromoteLevel.None, SendPoiList, "show the list of POIs.\n--gps: create GPS points.\n--gps-remove: remove GPS points."));

            _poiGpsCollection = new PoiGpsCollection();
            _poiGpsCollection.Load();

            if (MyAPIGateway.Session.IsServer)
            {
                LoadConfig();
            }

            MyLog.Default.Info("[HnzPveSeason] session loaded");
        }

        protected override void UnloadData()
        {
            base.UnloadData();

            _commandModule.Unload();
            _poiGpsCollection.Unload();
            Communication.Unload();

            if (MyAPIGateway.Session.IsServer)
            {
                _poiMap.Unload();
            }
        }

        void LoadConfig()
        {
            SessionConfig.Load();
            _poiMap.LoadConfig();
        }

        void SendPoiList(string args, ulong steamId)
        {
            if (args.Contains("--gps-remove"))
            {
                Communication.SendMessage(steamId, Color.White, "GPS points removed.");
                return;
            }

            var pois = _poiMap.GetAllPois();

            if (args.Contains("--gps"))
            {
                _poiGpsCollection.RemoveAll(steamId);
                _poiGpsCollection.AddAll(steamId, pois);
                Communication.SendMessage(steamId, Color.White, "GPS points added to HUD.");
            }

            var sb = new StringBuilder();
            foreach (var poi in pois)
            {
                sb.AppendLine($"> {poi.Id}");
            }

            Communication.ShowScreenMessage(steamId, "Points of Interest", sb.ToString());
        }
    }
}