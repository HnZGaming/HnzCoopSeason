using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HnzCoopSeason.Utils;
using HnzCoopSeason.Utils.Commands;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
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
            _commandModule.Register(new Command("poi invade", false, MyPromoteLevel.Moderator, Command_InvadePoi, "invade a POI."));
            _commandModule.Register(new Command("poi spawn", false, MyPromoteLevel.Moderator, Command_Spawn, "spawn grids at a POI given encounter config index."));
            _commandModule.Register(new Command("stores update", false, MyPromoteLevel.Moderator, Command_UpdateStores, "update all merchant stores."));
            _commandModule.Register(new Command("poi spectate", true, MyPromoteLevel.Moderator, Command_SpectatePoi, "move the spectator camera to a POI."));
            _commandModule.Register(new Command("print", false, MyPromoteLevel.Moderator, Command_Print, "print out the game state."));
        }

        void Command_ReloadConfig(string args, ulong steamId)
        {
            LoadConfig();
        }

        void Command_SendPoiList(string args, ulong steamId)
        {
            if (args.Contains("--gps-remove"))
            {
                PoiGpsView.RemoveAll(steamId);
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
                PoiGpsView.ReplaceAll(steamId, pois);
                SendMessage(steamId, Color.White, "GPS points added to HUD.");
            }
            else
            {
                var sb = new StringBuilder();
                foreach (var poi in pois)
                {
                    sb.AppendLine($"> {poi.Id} -- {poi.State}");
                }

                MissionScreenView.ShowScreenMessage(steamId, "Points of Interest", sb.ToString(), true);
            }
        }

        void Command_ReleasePoi(string args, ulong steamId)
        {
            Command_SetPoiState(args, PoiState.Released, steamId);
        }

        void Command_InvadePoi(string args, ulong steamId)
        {
            Command_SetPoiState(args, PoiState.Occupied, steamId);
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
                merchant.Spawn();
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
            PoiSpectatorCamera.RequestPosition(poiId);
        }

        void Command_Print(string args, ulong steamId)
        {
            MissionScreenView.ShowScreenMessage(steamId, "Print", _poiMap.ToString(), true);
        }
    }
}