using System.Collections.Generic;
using HnzUtils;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Spawners
{
    public sealed class SpawnMatrixBuilder
    {
        const int MaxTrialCount = 100;
        readonly List<MatrixD> _results = new List<MatrixD>();
        MyPlanet _planet;
        Vector3 PlanetCenter => _planet.WorldMatrix.Translation;

        public BoundingSphereD Sphere;
        public float Clearance;
        public bool SnapToVoxel;
        public int Count;
        public Vector3D? PlayerPosition;

        public IReadOnlyList<MatrixD> Results => _results;

        public bool TryBuild() // called once in lifetime of an instance
        {
            _planet = PlanetCollection.GetClosestPlanet(Sphere.Center);

            var randomPositions = new List<Vector3D>();
            var gravity = VRageUtils.CalculateNaturalGravity(Sphere.Center);
            if (gravity == Vector3.Zero) // deep space
            {
                for (var i = 0; i < MaxTrialCount; i++)
                {
                    var position = MathUtils.GetRandomPositionInSphere(Sphere);
                    randomPositions.Add(position);
                }
            }
            else // planet/moon surface or orbit
            {
                var distance = Vector3.Distance(PlanetCenter, Sphere.Center);
                for (var i = 0; i < MaxTrialCount; i++)
                {
                    var position = MathUtils.GetRandomPositionOnDisk(Sphere.Center, gravity, Sphere.Radius);
                    position = PlanetCenter + Vector3.Normalize(position - PlanetCenter) * distance;
                    randomPositions.Add(position);
                }
            }

            randomPositions.Sort(p => Vector3D.Distance(p, Sphere.Center));

            var positionQueue = new Queue<Vector3D>();
            positionQueue.Enqueue(Sphere.Center);
            foreach (var p in randomPositions)
            {
                positionQueue.Enqueue(p);
            }

            while (_results.Count < Count)
            {
                Vector3D position;
                if (!positionQueue.TryDequeue(out position)) return false;

                var result = TryBuild(position);
                MyLog.Default.Debug($"[HnzCoopSeason] SpawnMatrixBuilder.TryBuild() {position} -> {result}");
            }

            return true;
        }

        ResultType TryBuild(Vector3D position)
        {
            MatrixD matrix;
            var gravity = (Vector3D)VRageUtils.CalculateNaturalGravity(position);
            if (gravity != Vector3.Zero) // planet/moon
            {
                // modify position
                var up = gravity.Normalized() * -1;
                var overground = position + up * Sphere.Radius;

                IHitInfo hitInfo;
                if (!VRageUtils.TryGetFirstRaycastHitInfoByType<MyVoxelBase>(overground, PlanetCenter, out hitInfo))
                {
                    return ResultType.Failure_VoxelNotFound;
                }

                var normalAngle = MathHelper.ToDegrees(Vector3.Angle(hitInfo.Normal, up * -1));
                if (normalAngle > 20) return ResultType.Failure_SteepVoxelSurface;

                var intersection = hitInfo.Position;
                if (SnapToVoxel)
                {
                    position = intersection;
                }
                else
                {
                    // slightly move up from the ground
                    position = intersection + up * (Clearance * 0.5f);
                }
            }

            var sphere = new BoundingSphereD(position, Clearance);

            // check collision with already-built positions
            foreach (var m in _results)
            {
                var existingSphere = new BoundingSphereD(m.Translation, Clearance);
                if (sphere.Contains(existingSphere).ContainsOrIntersects())
                {
                    return ResultType.Failure_PlaceholderCollision;
                }
            }

            // check collision with existing grids
            var entities = new List<MyEntity>();
            MyGamePruningStructure.GetAllEntitiesInSphere(ref sphere, entities, MyEntityQueryType.Both);
            foreach (var e in entities)
            {
                var grid = e as MyCubeGrid;
                if (grid == null) continue;

                var gridAABB = grid.GetPhysicalGroupAABB();
                if (sphere.Contains(gridAABB).ContainsOrIntersects())
                {
                    return ResultType.Failure_CubeGridCollision;
                }
            }

            if (gravity != Vector3.Zero) // planet/moon
            {
                var up = gravity.Normalized() * -1;

                Vector3D forward;
                if (PlayerPosition != null)
                {
                    forward = PlayerPosition.Value - position;
                }
                else
                {
                    forward = Vector3D.CalculatePerpendicularVector(up);
                }

                matrix = MatrixD.CreateWorld(position, forward, up);
            }
            else // deep space
            {
                Vector3D forward;
                if (PlayerPosition != null)
                {
                    forward = PlayerPosition.Value - position;
                }
                else
                {
                    forward = MathUtils.GetRandomUnitDirection();
                }

                var up = Vector3D.CalculatePerpendicularVector(forward);
                matrix = MatrixD.CreateWorld(position, forward, up);
            }

            _results.Add(matrix);
            return ResultType.Success;
        }

        enum ResultType
        {
            Success,
            Failure_VoxelNotFound,
            Failure_PlaceholderCollision,
            Failure_CubeGridCollision,
            Failure_SteepVoxelSurface,
        }
    }
}