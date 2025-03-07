using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Utils.Commands
{
    public sealed class CommandModule
    {
        public delegate void Callback(string args, ulong steamId);

        static readonly ushort MessageHandlerId = (ushort)"HnzCoopSeason.CommandModule".GetHashCode();

        readonly string _prefix;
        readonly List<Command> _commands;

        public CommandModule(string prefix)
        {
            _prefix = prefix;
            _commands = new List<Command>();
        }

        public void Load()
        {
            MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEntered;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(MessageHandlerId, OnCommandPayloadReceived);
        }

        public void Unload()
        {
            MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEntered;
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(MessageHandlerId, OnCommandPayloadReceived);
            _commands.Clear();
        }

        public void Register(Command command)
        {
            _commands.Add(command);
        }

        void OnMessageEntered(ulong sender, string messageText, ref bool sendToOthers)
        {
            var prefix = $"/{_prefix}";
            if (!messageText.StartsWith(prefix)) return;

            var body = messageText.Substring(prefix.Length).Trim();
            MyLog.Default.Info($"[HnzCoopSeason] command entered by {sender}: '{messageText}', body: '{body}'");
            sendToOthers = false;

            var body = messageText.Substring(prefix.Length).Trim();
            foreach (var command in _commands)
            {
                if (!body.StartsWith(command.Head)) continue;

                if (command.Local || MyAPIGateway.Session.IsServer)
                {
                    ProcessCommand(sender, command, body);
                }
                else
                {
                    var data = Encoding.UTF8.GetBytes(body);
                    MyAPIGateway.Multiplayer.SendMessageToServer(MessageHandlerId, data);
                }

                return;
            }

            // fallback: show the list of all commands
            MyLog.Default.Info($"[HnzCoopSeason] command not found; message: '{messageText}', showing the command list");

            var sb = new StringBuilder();
            sb.AppendLine($"Commands for {_prefix}:");
            foreach (var command in _commands)
            {
                sb.AppendLine($"{command.Head}: {command.Help}");
            }

            MyAPIGateway.Utilities.ShowMessage("COOP", sb.ToString());
        }

        void OnCommandPayloadReceived(ushort id, byte[] load, ulong steamId, bool sentFromServer)
        {
            var body = Encoding.UTF8.GetString(load);
            MyLog.Default.Info($"[HnzCoopSeason] command (server) received: {body}");

            foreach (var command in _commands)
            {
                if (!body.StartsWith(command.Head)) continue;
                if (command.Local) continue;

                ProcessCommand(steamId, command, body);
                return;
            }
        }

        void ProcessCommand(ulong sender, Command command, string body)
        {
            if (!ValidateLevel(sender, command.Level))
            {
                Session.SendMessage(sender, Color.Red, "Insufficient promote level");
                return;
            }

            if (body.Contains("--help") || body.Contains("-h"))
            {
                Session.SendMessage(sender, Color.White, command.Help);
                return;
            }

            try
            {
                var args = body.Substring(command.Head.Length).Trim();
                command.Callback(args, sender);
            }
            catch (Exception e)
            {
                MyLog.Default.Error($"[HnzCoopSeason] command {_prefix} {command.Head}: {command.Head} error: {e}");
                Session.SendMessage(sender, Color.Red, "Error. Please talk to administrators.");
            }
        }

        static bool ValidateLevel(ulong steamId, MyPromoteLevel level)
        {
            var playerId = MyAPIGateway.Players.TryGetIdentityId(steamId);
            var player = MyAPIGateway.Players.TryGetIdentityId(playerId);
            return player?.PromoteLevel >= level;
        }
    }
}