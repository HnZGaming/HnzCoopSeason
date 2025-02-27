﻿using System;
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
            _commandModule.Register(new Command("reload", false, MyPromoteLevel.Admin, Command_ReloadConfig, "reload config."));
            _commandModule.Register(new Command("poi list", false, MyPromoteLevel.None, Command_SendPoiList, "show the list of POIs.\n--gps: create GPS points.\n--gps-remove: remove GPS points.\n--limit N: show N POIs."));
            _commandModule.Register(new Command("poi release", false, MyPromoteLevel.Moderator, Command_ReleasePoi, "release a POI."));
            _commandModule.Register(new Command("poi invade", false, MyPromoteLevel.Moderator, Command_InvadePoi, "invade a POI."));

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

        void Command_ReloadConfig(string args, ulong steamId)
        {
            LoadConfig();
        }

        void Command_SendPoiList(string args, ulong steamId)
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
                if (!VRageUtils.TryGetCharacter(steamId, out character))
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
                sb.AppendLine($"> {poi.Id} -- {poi.State}");
            }

            Communication.ShowScreenMessage(steamId, "Points of Interest", sb.ToString());
        }

        void Command_ReleasePoi(string args, ulong steamId)
        {
            Command_SetPoiState(args, PoiState.Released, steamId);
        }

        void Command_InvadePoi(string args, ulong steamId)
        {
            Command_SetPoiState(args, PoiState.Occupied, steamId);
        }

        void Command_SetPoiState(string id, PoiState state, ulong steamId)
        {
            var poi = GetPoi(id);
            if (poi == null)
            {
                Communication.SendMessage(steamId, Color.Red, $"POI {id} not found.");
                return;
            }

            poi.SetState(state);
        }

        public Poi GetPoi(string id)
        {
            Poi poi;
            _poiMap.TryGetPoi(id, out poi);
            return poi;
        }
    }
}