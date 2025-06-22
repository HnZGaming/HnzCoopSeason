using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzUtils.Commands
{
    public sealed class CommandModule
    {
        public delegate void SendMessageDelegate(ulong steamId, Color color, string message);
        
        readonly ushort _messageHandlerId;
        readonly string _prefix;
        readonly List<Command> _commands;

        public CommandModule(ushort messageHandlerId, string prefix)
        {
            _messageHandlerId = messageHandlerId;
            _prefix = prefix;
            _commands = new List<Command>();
        }

        public event SendMessageDelegate SendMessage;

        public void Load()
        {
            MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEntered;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(_messageHandlerId, OnCommandPayloadReceived);
        }

        public void Unload()
        {
            MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEntered;
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(_messageHandlerId, OnCommandPayloadReceived);
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

            MyLog.Default.Info($"[HnzCoopSeason] command (client) entered by {sender}: {messageText}");
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
                    MyAPIGateway.Multiplayer.SendMessageToServer(_messageHandlerId, data);
                    MyLog.Default.Info($"[HnzCoopSeason] command (client) sent to server: {body}");
                }

                return;
            }

            // fallback: show the list of all commands
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
            MyLog.Default.Info($"[HnzCoopSeason] command (server) received; steam: {steamId}, body: '{body}'");

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
                SendMessage?.Invoke(sender, Color.Red, "Insufficient promote level");
                return;
            }

            if (body.Contains("--help") || body.Contains("-h"))
            {
                SendMessage?.Invoke(sender, Color.White, command.Help);
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
                SendMessage?.Invoke(sender, Color.Red, "Error. Please talk to administrators.");
            }
        }

        static bool ValidateLevel(ulong steamId, MyPromoteLevel level)
        {
            if (steamId == 0) return true; // torch command
            var playerId = MyAPIGateway.Players.TryGetIdentityId(steamId);
            var player = MyAPIGateway.Players.TryGetIdentityId(playerId);
            return player?.PromoteLevel >= level;
        }
    }
}