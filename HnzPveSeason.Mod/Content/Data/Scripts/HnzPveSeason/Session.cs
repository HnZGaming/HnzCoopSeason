using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HnzPveSeason.FlashGPS;
using HnzPveSeason.MES;
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
        bool _doneFirstUpdate;

        public override void LoadData()
        {
            base.LoadData();
            Instance = this;

            MyLog.Default.Info("[HnzPveSeason] session loading");

            _poiMap = new PoiMap();
            Communication.Load();

            _commandModule = new CommandModule("pve");
            _commandModule.Load();
            _commandModule.Register(new Command("reload", MyPromoteLevel.Admin, ReloadConfig, "reload config."));
            _commandModule.Register(new Command("poi list", MyPromoteLevel.None, SendPoiList, "show the list of POIs.\n--gps: create GPS points.\n--gps-remove: remove GPS points.\n--limit N: show N POIs."));

            _poiGpsCollection = new PoiGpsCollection();
            _poiGpsCollection.Load();

            if (MyAPIGateway.Session.IsServer)
            {
                MESApi.Load();
                FlashGpsApi.Load(nameof(HnzPveSeason).GetHashCode());
                PlanetCollection.Load();

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
                MESApi.Unload();
                PlanetCollection.Unload();
                _poiMap.Unload();
                OnlineCharacterCollection.Unload();
            }
        }

        void LoadConfig()
        {
            SessionConfig.Load();
            _poiMap.LoadConfig();

            _doneFirstUpdate = false;
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();

            if (!_doneFirstUpdate)
            {
                _doneFirstUpdate = true;
                _poiMap.LoadScene();
            }

            if (MyAPIGateway.Session.GameplayFrameCounter % 60 == 0)
            {
                OnlineCharacterCollection.Update();
            }

            _poiMap.Update();
        }

        void ReloadConfig(string args, ulong steamId)
        {
            LoadConfig();
        }

        void SendPoiList(string args, ulong steamId)
        {
            if (args.Contains("--gps-remove"))
            {
                Communication.SendMessage(steamId, Color.White, "GPS points removed.");
                return;
            }

            var pois = _poiMap.GetAllPois();

            int limit;
            var limitMatch = Regex.Match(args, @"--limit (\d+)");
            if (limitMatch.Success && int.TryParse(limitMatch.Groups[1].Value, out limit))
            {
                IMyCharacter character;
                if (!TryGetCharacter(steamId, out character))
                {
                    Communication.SendMessage(steamId, Color.Red, "No player character found.");
                    return;
                }

                var playerPosition = character.GetPosition();
                pois = pois.OrderBy(p => Vector3D.Distance(p.Position, playerPosition)).Take(limit).ToArray();
            }

            if (args.Contains("--gps"))
            {
                _poiGpsCollection.RemoveAll(steamId);
                _poiGpsCollection.AddAll(steamId, pois);
                Communication.SendMessage(steamId, Color.White, "GPS points added to HUD.");
            }

            var sb = new StringBuilder();
            foreach (var poi in pois)
            {
                sb.AppendLine($"> {poi.Id} -- {poi.CurrentState}");
            }

            Communication.ShowScreenMessage(steamId, "Points of Interest", sb.ToString());
        }

        static bool TryGetCharacter(ulong steamId, out IMyCharacter character)
        {
            var playerId = MyAPIGateway.Players.TryGetIdentityId(steamId);
            character = MyAPIGateway.Players.TryGetIdentityId(playerId)?.Character;
            return character != null;
        }

        public void ReleasePoi(string id)
        {
            MyLog.Default.Info($"[HnzPveSeason] POI released: {id}");
            _poiMap.ReleasePoi(id);
        }
    }
}