using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason.Utils.Commands
{
    public sealed class CommandModule
    {
        public delegate void Callback(string args, ulong steamId);

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
        }

        public void Unload()
        {
            MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEntered;
            _commands.Clear();
        }

        public void Register(Command command)
        {
            _commands.Add(command);
        }

        void OnMessageEntered(ulong sender, string messageText, ref bool sendToOthers)
        {
            var prefix = $"/{_prefix} ";
            if (!messageText.StartsWith(prefix)) return;

            MyLog.Default.Info($"[HnzPveSeason] command entered by {sender}: {messageText}");

            var body = messageText.Substring(prefix.Length);
            foreach (var command in _commands)
            {
                if (body.StartsWith(command.Head))
                {
                    ProcessCommand(sender, command, body);
                    return;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Commands for {_prefix}:");
            foreach (var command in _commands)
            {
                sb.AppendLine($"{command.Head}: {command.Help}");
            }

            Communication.SendMessage(sender, Color.White, sb.ToString());
        }

        void ProcessCommand(ulong sender, Command command, string body)
        {
            if (!ValidateLevel(sender, command.Level))
            {
                Communication.SendMessage(sender, Color.Red, "Insufficient promote level");
                return;
            }

            if (body.Contains("--help") || body.Contains("-h"))
            {
                Communication.SendMessage(sender, Color.White, command.Help);
                return;
            }

            try
            {
                var args = body.Substring(command.Head.Length).Trim();
                command.Callback(args, sender);
            }
            catch (Exception e)
            {
                MyLog.Default.Error($"[HnzPveSeason] command {_prefix} {command.Head}: {command.Head} error: {e}");
                Communication.SendMessage(sender, Color.Red, "Error. Please talk to administrators.");
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