using System.Text;
using NLog;
using Sandbox.ModAPI;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using VRageMath;

namespace HnzCoopSeason.Torch
{
    // ReSharper disable once UnusedType.Global
    public sealed class Commands : CommandModule
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        // must match the mod's const
        static readonly ushort MessageHandlerId = (ushort)"HnzCoopSeason.CommandModule".GetHashCode();

        [Command("coop")]
        [Permission(MyPromoteLevel.Moderator)]
        // ReSharper disable once UnusedMember.Global
        public void Command()
        {
            Context.Respond("Note: this is a moderator command. Player command is `/coop`.", Color.Yellow);

            var text = string.Join(" ", Context.Args);
            var data = Encoding.UTF8.GetBytes(text);
            MyAPIGateway.Multiplayer.SendMessageToServer(MessageHandlerId, data);
            Log.Info($"command body sent to mod: '{text}'");
        }
    }
}