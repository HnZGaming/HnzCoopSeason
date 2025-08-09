using System;
using HnzCoopSeason.Merchants;
using HnzUtils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.POI.Reclaim
{
    public sealed class PoiMerchant
    {
        readonly string _poiId;
        readonly Vector3D _position;
        readonly MerchantSpawner _spawner;
        Merchant _merchant;
        PoiState _poiState;

        public PoiMerchant(string poiId, Vector3D position, MerchantConfig[] configs)
        {
            _poiId = poiId;
            _position = position;
            _spawner = new MerchantSpawner(poiId, configs);
        }

        public void Load(IMyCubeGrid[] grids)
        {
            _spawner.OnMerchantFound += OnMerchantFound;

            _spawner.TryFind(grids);
        }

        public void Unload(bool sessionUnload)
        {
            _spawner.OnMerchantFound -= OnMerchantFound;

            if (!sessionUnload)
            {
                Despawn();
            }
        }

        public void Save()
        {
            _merchant?.Save();
        }

        public void Update()
        {
            TrySpawn();
            _merchant?.Update();
        }

        void TrySpawn()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % 60 != 0) return;
            if (_poiState != PoiState.Released) return;
            if (_spawner.Spawning) return;
            if (_merchant != null && !(_merchant.Grid?.Closed ?? true)) return;

            var sphere = new BoundingSphereD(_position, SessionConfig.Instance.EncounterRadius);
            if (!OnlineCharacterCollection.ContainsPlayer(sphere)) return;

            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} player nearby");

            Spawn(Math.Abs(_poiId.GetHashCode()));
        }

        public void OnStateChanged(PoiState state)
        {
            _poiState = state;

            if (state != PoiState.Released)
            {
                Despawn();
            }
        }

        public bool TryGetPosition(out Vector3D position)
        {
            if (_merchant != null && _poiState == PoiState.Released)
            {
                position = _merchant.Grid.GetPosition();
                return true;
            }

            position = default(Vector3D);
            return false;
        }

        public void Spawn(int configIndex)
        {
            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} Spawn()");

            Despawn();

            _spawner.TrySpawn(configIndex, _position);
        }

        void OnMerchantFound(Merchant merchant, bool spawned)
        {
            _merchant = merchant;
            _merchant.Load();

            if (spawned)
            {
                Session.SendNotification(_poiId.GetHashCode(), "Merchant Discovery", Color.Green, merchant.Grid.GetPosition(), 10, "Merchant has been discovered!");
            }
        }

        void Despawn()
        {
            if (_merchant == null) return;

            MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_poiId} grid closing");

            _merchant.Unload();
            _merchant = null;
        }

        public override string ToString()
        {
            return $"Merchant({nameof(_poiId)}: {_poiId}')";
        }
    }
}