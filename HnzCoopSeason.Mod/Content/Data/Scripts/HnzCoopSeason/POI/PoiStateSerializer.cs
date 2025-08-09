using HnzUtils;

namespace HnzCoopSeason.POI
{
    public sealed class PoiStateSerializer
    {
        readonly SandboxDictionaryVariable _sandbox;

        public PoiStateSerializer(string poiId)
        {
            _sandbox = new SandboxDictionaryVariable($"HnzCoopSeason.Poi.{poiId}");
        }

        public PoiState State
        {
            get { return (PoiState)_sandbox.GetValueOrDefault(nameof(State), (int)PoiState.Occupied); }
            set { _sandbox.SetValue(nameof(State), (int)value); }
        }

        public T GetValueOrDefault<T>(string key, T defaultValue)
        {
            return _sandbox.GetValueOrDefault(key, defaultValue);
        }

        public void SetValue<T>(string key, T value)
        {
            _sandbox.SetValue(key, value);
        }

        public void Load()
        {
            _sandbox.Load();
        }

        public void Save()
        {
            _sandbox.Save();
        }
    }
}