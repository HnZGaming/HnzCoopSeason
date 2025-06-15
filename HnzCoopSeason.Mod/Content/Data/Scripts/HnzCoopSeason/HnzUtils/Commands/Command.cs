using VRage.Game.ModAPI;

namespace HnzUtils.Commands
{
    public sealed class Command
    {
        public delegate void CommandCallback(string args, ulong steamId);

        public Command(string head, bool local, MyPromoteLevel level, CommandCallback callback, string help)
        {
            Head = head;
            Local = local;
            Help = help;
            Level = level;
            Callback = callback;
        }

        public string Head { get; }
        public bool Local { get; }
        public string Help { get; }
        public MyPromoteLevel Level { get; }
        public CommandCallback Callback { get; }
    }
}