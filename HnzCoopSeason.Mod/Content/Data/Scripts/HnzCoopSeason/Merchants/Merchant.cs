using HnzCoopSeason.HnzUtils;
using HnzUtils;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason.Merchants
{
    public sealed class Merchant
    {
        readonly string _id;
        readonly SandboxDictionaryVariable _sandbox;
        readonly SafeZone _safeZone;
        MerchantShipyard _shipyard;

        public Merchant(IMyCubeGrid grid, string id)
        {
            Grid = grid;
            _id = id;
            _sandbox = new SandboxDictionaryVariable($"HnzCoopSeason.Merchant.{_id}");
            _safeZone = new SafeZone(id);
        }

        public IMyCubeGrid Grid { get; }

        public void Load()
        {
            _sandbox.Load();

            _safeZone.SafezoneId = _sandbox.GetValueOrDefault("_safezoneId", 0);
            _safeZone.Create(Grid.GetPosition(), 75f);
            _sandbox.SetValue("_safezoneId", _safeZone.SafezoneId);

            if (MerchantShipyard.TryFind(Grid, out _shipyard))
            {
                _shipyard.Load();
                MyLog.Default.Info($"[HnzCoopSeason] merchant {_id} shipyard loaded");
            }

            var store = new MerchantStore(_id, Grid);
            store.Update(true);
            MerchantEconomy.Instance.AddStore(_id, store);

            Grid.OnClose += OnGridClosed;
        }

        public void Unload()
        {
            Grid?.Close();
            MerchantEconomy.Instance.RemoveStore(_id);
        }

        void OnGridClosed(IMyEntity grid)
        {
            MyLog.Default.Info($"[HnzCoopSeason] merchant {_id} grid closed");
            grid.OnClose -= OnGridClosed;
            _safeZone.Remove();
            _shipyard = null;
        }

        public void Save()
        {
            _sandbox.Save();
        }

        public void Update()
        {
            UpdatePower();
            _shipyard?.Update();
        }

        void UpdatePower()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % 60 != 0) return;
            if (Grid == null) return;
            if (Grid.Closed) return;

            foreach (var battery in Grid.GetFatBlocks<MyBatteryBlock>())
            {
                battery.CurrentStoredPower = battery.MaxStoredPower;
            }
        }
    }
}