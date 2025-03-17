using System;
using System.Collections.Generic;
using HnzCoopSeason.Utils;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class SpawnMatrixBuilder
    {
        const int MaxTrialCount = 100;
        readonly List<MatrixD> _results = new List<MatrixD>();
        readonly List<MyVoxelBase> _voxels = new List<MyVoxelBase>();

        public BoundingSphereD Sphere;
        public float Clearance;
        public bool SnapToVoxel;
        public int Count;
        public Vector3D? PlayerPosition;

        public IReadOnlyList<MatrixD> Results => _results;

        public bool TryBuild() // called once in lifetime of an instance
        {
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref Sphere, _voxels);

            var randomPositions = new List<Vector3D>();
            var mainGravity = VRageUtils.CalculateNaturalGravity(Sphere.Center);
            for (var i = 0; i < MaxTrialCount; i++)
            {
                if (mainGravity == Vector3.Zero) // deep space
                {
                    var position = MathUtils.GetRandomPositionInSphere(Sphere);
                    randomPositions.Add(position);
                }
                else // planet/moon surface or orbit
                {
                    var position = MathUtils.GetRandomPositionOnDisk(Sphere.Center, mainGravity, Sphere.Radius);
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

                var rt = TryBuild(position);
                VRageUtils.AddTemporaryGps($"{rt}", rt == ResultType.Success ? Color.Green : Color.Yellow, 5, position);
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
                var underground = position + up * (Sphere.Radius * -1);
                var line = new LineD(overground, underground);
                Vector3D intersection;
                if (!VRageUtils.TryGetVoxelIntersection(line, _voxels, out intersection)) return ResultType.Failure_VoxelNotFound;

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

            // check collision with existing grids in the proximity
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
        }
    }
}