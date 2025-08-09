using System.Collections.Generic;
using HnzUtils;

namespace HnzCoopSeason.Merchants
{
    public sealed class MerchantEconomy
    {
        public static readonly MerchantEconomy Instance = new MerchantEconomy();

        readonly Interval _economyInterval;
        readonly Dictionary<string, MerchantStore> _stores;

        MerchantEconomy()
        {
            _economyInterval = new Interval();
            _stores = new Dictionary<string, MerchantStore>();
        }

        public void Load()
        {
            _economyInterval.Initialize();
        }

        public void Unload()
        {
        }

        public void Update()
        {
            var i = SessionConfig.Instance.EconomyUpdateIntervalMinutes * 60 * 60;
            if (!_economyInterval.Update(i)) return;

            UpdateStores();
        }

        public void UpdateStores()
        {
            foreach (var p in _stores)
            {
                p.Value.Update();
            }
        }

        public void AddStore(string id, MerchantStore store)
        {
            _stores.Add(id, store);
        }

        public void RemoveStore(string id)
        {
            _stores.Remove(id);
        }
    }
}