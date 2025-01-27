using System.Windows.Controls;
using NLog;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using Utils.Torch;

namespace HnzPveSeason.Torch
{
    // ReSharper disable once UnusedType.Global
    public sealed class Plugin : TorchPluginBase, IWpfPlugin
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();

        Persistent<Config> _config;
        UserControl _userControl;

        public Config Config => _config.Data;
        public UserControl GetControl() => _config.GetOrCreateUserControl(ref _userControl);

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            var configFilePath = this.MakeFilePath($"{nameof(HnzPveSeason.Torch)}.cfg");
            _config = Persistent<Config>.Load(configFilePath);
            
            Log.Info("loaded");
        }
    }
}