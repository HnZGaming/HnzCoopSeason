using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace HnzUtils
{
    public sealed class LocalGpsCollection<K> : IEnumerable<IMyGps>
    {
        readonly Dictionary<K, IMyGps> _allGps = new Dictionary<K, IMyGps>();

        public IEnumerator<IMyGps> GetEnumerator() => _allGps.Values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Clear()
        {
            _allGps.Clear();
        }

        public bool TryGet(K key, out IMyGps gps)
        {
            return _allGps.TryGetValue(key, out gps);
        }

        public void Add(K key, IMyGps gps)
        {
            _allGps.Add(key, gps);
            MyAPIGateway.Session.GPS.AddLocalGps(gps);
        }

        public void RemoveExceptFor(IEnumerable<K> keys)
        {
            var hashset = new HashSet<K>();
            hashset.UnionWith(keys);

            foreach (var k in _allGps.Keys.ToArray())
            {
                if (!hashset.Contains(k))
                {
                    var gps = _allGps[k];
                    MyAPIGateway.Session.GPS.RemoveLocalGps(gps);
                    _allGps.Remove(k);
                }
            }
        }
    }
}