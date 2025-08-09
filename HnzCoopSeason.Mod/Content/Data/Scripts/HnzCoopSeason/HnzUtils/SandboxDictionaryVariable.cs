using Sandbox.ModAPI;
using VRage.Serialization;

namespace HnzUtils
{
    public sealed class SandboxDictionaryVariable
    {
        readonly string _key;
        SerializableDictionary<string, object> _data;

        public SandboxDictionaryVariable(string key)
        {
            _key = key;
        }

        public void Load()
        {
            if (!MyAPIGateway.Utilities.GetVariable(_key, out _data))
            {
                _data = new SerializableDictionary<string, object>();
            }
        }

        public T GetValueOrDefault<T>(string key, T defaultValue)
        {
            return _data.Dictionary.GetValueOrDefault(key, defaultValue);
        }

        public void SetValue<T>(string key, T value)
        {
            _data.Dictionary[key] = value;
        }

        public void Save()
        {
            MyAPIGateway.Utilities.SetVariable(_key, _data);
        }

        public override string ToString()
        {
            return _data?.Dictionary?.ToStringDic() ?? "<empty>";
        }
    }
}