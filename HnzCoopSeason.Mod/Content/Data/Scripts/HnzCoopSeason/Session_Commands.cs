using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HnzCoopSeason.Missions;
using HnzCoopSeason.Utils;
using HnzCoopSeason.Utils.Commands;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed partial class Session
    {
        void InitializeCommands()
        {
            _commandModule.Register(new Command("reload", false, MyPromoteLevel.Admin, Command_ReloadConfig, "reload config."));
            _commandModule.Register(new Command("poi list", false, MyPromoteLevel.None, Command_SendPoiList, "show the list of POIs.\n--gps: create GPS points.\n--gps-remove: remove GPS points.\n--limit N: show N POIs."));
            _commandModule.Register(new Command("poi release", false, MyPromoteLevel.Moderator, Command_ReleasePoi, "release a POI."));
            _commandModule.Register(new Command("poi occupy", false, MyPromoteLevel.Moderator, Command_OccupyPoi, "invade a POI."));
            _commandModule.Register(new Command("poi invade", false, MyPromoteLevel.Moderator, Command_InvadePoi, "invade a POI."));
            _commandModule.Register(new Command("poi spawn", false, MyPromoteLevel.Moderator, Command_Spawn, "spawn grids at a POI given encounter config index."));
            _commandModule.Register(new Command("poi print", false, MyPromoteLevel.Moderator, Command_PrintPoi, "print out the POI state."));
            _commandModule.Register(new Command("stores update", false, MyPromoteLevel.Moderator, Command_UpdateStores, "update all merchant stores."));
            _commandModule.Register(new Command("poi spectate", false, MyPromoteLevel.Moderator, Command_SpectatePoi, "move the spectator camera to a POI."));
            _commandModule.Register(new Command("print", false, MyPromoteLevel.Moderator, Command_Print, "print out the game state."));
            _commandModule.Register(new Command("revenge", false, MyPromoteLevel.Moderator, Command_Revenge, "spawn revenge orks"));
            _commandModule.Register(new Command("mission list", false, MyPromoteLevel.Moderator, Command_ListMission, "list missions"));
            _commandModule.Register(new Command("mission update", false, MyPromoteLevel.Moderator, Command_UpdateMission, "update mission progress"));
        }

        void Command_ReloadConfig(string args, ulong steamId)
        {
            LoadConfig();
        }

        void Command_SendPoiList(string args, ulong steamId)
        {
            if (args.Contains("--gps-remove"))
            {
                PoiMapDebugView.RemoveAll(steamId);
                SendMessage(steamId, Color.White, "GPS points removed.");
                return;
            }

            var pois = _poiMap.AllPois;

            int limit;
            var limitMatch = Regex.Match(args, @"--limit (\d+)");
            if (limitMatch.Success && int.TryParse(limitMatch.Groups[1].Value, out limit))
            {
                IMyCharacter character;
                if (!VRageUtils.TryGetCharacter(steamId, out character))
                {
                    SendMessage(steamId, Color.Red, "No player character found.");
                    return;
                }

                var playerPosition = character.GetPosition();
                pois = pois.OrderBy(p => Vector3D.Distance(p.Position, playerPosition)).Take(limit).ToArray();
            }

            if (args.Contains("--gps"))
            {
                PoiMapDebugView.ReplaceAll(steamId, pois);
                SendMessage(steamId, Color.White, "GPS points added to HUD.");
            }
            else
            {
                var sb = new StringBuilder();
                foreach (var poi in pois)
                {
                    sb.AppendLine($"> {poi.Id} -- {poi.State}");
                }

                MissionScreen.Send(steamId, "Points of Interest", sb.ToString(), true);
            }
        }

        void Command_ReleasePoi(string args, ulong steamId)
        {
            Command_SetPoiState(args, PoiState.Released, steamId);
        }

        void Command_OccupyPoi(string args, ulong steamId)
        {
            Command_SetPoiState(args, PoiState.Occupied, steamId);
        }

        void Command_InvadePoi(string args, ulong steamId)
        {
            if (!SetPoiState(args, PoiState.Invaded))
            {
                SendMessage(steamId, Color.Red, $"POI {args} not found or already set to the same state");
            }
        }

        void Command_SetPoiState(string poiId, PoiState state, ulong steamId)
        {
            if (!SetPoiState(poiId, state))
            {
                SendMessage(steamId, Color.Red, $"POI {poiId} not found or already set to state {state}.");
            }
        }

        void Command_Spawn(string text, ulong steamId)
        {
            var parts = text.Split(' ');
            var poiId = parts[0];
            var configIndex = parts[1].ParseIntOrDefault(0);

            Poi poi;
            if (!_poiMap.TryGetPoi(poiId, out poi))
            {
                SendMessage(steamId, Color.Red, $"POI {poiId} not found.");
                return;
            }

            if (poi.State == PoiState.Occupied)
            {
                var ork = poi.Observers.OfType<PoiOrk>().First();
                ork.Spawn(configIndex);
            }
            else
            {
                var merchant = poi.Observers.OfType<PoiMerchant>().First();
                merchant.Spawn(configIndex);
            }
        }

        void Command_UpdateStores(string text, ulong steamId)
        {
            foreach (var poi in _poiMap.AllPois)
            {
                var merchant = poi.Observers.OfType<PoiMerchant>().First();
                merchant.UpdateStore();
            }
        }

        void Command_SpectatePoi(string poiId, ulong steamId)
        {
            PoiSpectatorCamera.SendPosition(poiId, steamId);
        }

        void Command_PrintPoi(string poiId, ulong steamId)
        {
            Poi poi;
            if (!_poiMap.TryGetPoi(poiId, out poi))
            {
                SendMessage(steamId, Color.Red, $"POI {poiId} not found.");
                return;
            }

            MissionScreen.Send(steamId, "Print", poi.ToString(), true);
        }

        void Command_Revenge(string args, ulong steamId)
        {
            MyLog.Default.Info($"[HnzCoopSeason] revenge; args: '{args}'");
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);

            int configIndex;
            if (!int.TryParse(args, out configIndex))
            {
                SendMessage(steamId, Color.Red, $"invalid config index: {args}");
                return;
            }

            PoiOrkConfig config;
            if (!SessionConfig.Instance.Orks.TryGetElementAt(configIndex, out config))
            {
                SendMessage(steamId, Color.Red, $"invalid config index: {args}");
                return;
            }

            var playerId = MyAPIGateway.Players.TryGetIdentityId(steamId);
            var character = MyAPIGateway.Players.TryGetIdentityId(playerId)?.Character;
            if (character == null)
            {
                SendMessage(steamId, Color.Red, $"character not found: {steamId}");
                return;
            }

            RevengeOrkManager.Instance.Spawn(character.GetPosition(), config.SpawnGroupNames);
        }

        void Command_ListMission(string args, ulong steamId)
        {
            var level = int.Parse(args);
            var missions = MissionService.ReadMissions(level);
            var xml = MyAPIGateway.Utilities.SerializeToXML(missions);
            MissionScreen.Send(steamId, "Missions", xml, true);
        }

        void Command_UpdateMission(string args, ulong steamId)
        {
            var parts = args.Split(' ');
            var level = int.Parse(parts[0]);
            var id = int.Parse(parts[1]);
            var progress = int.Parse(parts[2]);
            MissionService.Instance.UpdateMission(level, id, progress);
            SendMessage(steamId, Color.White, $"done: {level}, {id}, {progress}");
        }

        void Command_Print(string args, ulong steamId)
        {
            if (args.Trim() == "config")
            {
                var xml = MyAPIGateway.Utilities.SerializeToXML(SessionConfig.Instance);
                MissionScreen.Send(steamId, "Print", xml, true);
                return;
            }

            MissionScreen.Send(steamId, "Print", ToString(), true);
        }
    }
}