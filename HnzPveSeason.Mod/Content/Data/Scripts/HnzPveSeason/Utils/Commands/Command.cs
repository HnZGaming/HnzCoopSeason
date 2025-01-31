using VRage.Game.ModAPI;

namespace HnzPveSeason.Utils.Commands
{
    public sealed class Command
    {
        public Command(string head, MyPromoteLevel level, CommandModule.Callback callback, string help)
        {
            Head = head;
            Help = help;
            Level = level;
            Callback = callback;
        }

        public string Head { get; }
        public string Help { get; }
        public MyPromoteLevel Level { get; }
        public CommandModule.Callback Callback { get; }
    }
}