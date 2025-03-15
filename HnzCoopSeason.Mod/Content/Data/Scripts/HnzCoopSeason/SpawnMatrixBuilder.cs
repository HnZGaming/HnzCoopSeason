using System;
using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.Utils;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class SpawnMatrixBuilder
    {
        const int MaxTrialCount = 100;
        readonly List<MatrixD> _results = new List<MatrixD>();

        public BoundingSphereD Sphere;
        public float Clearance;
        public bool SnapToVoxel;
        public int Count;
        public Vector3D? PlayerPosition;

        public IReadOnlyList<MatrixD> Results => _results;

        public bool TryBuild()
        {
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

            var positionQueue = new Queue<Vector3D>();
            positionQueue.Enqueue(Sphere.Center);
            foreach (var p in randomPositions.OrderBy(p => Vector3D.Distance(p, Sphere.Center)))
            {
                positionQueue.Enqueue(p);
            }

            var voxelMaps = new List<MyVoxelBase>();
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref Sphere, voxelMaps);

            while (_results.Count < Count)
            {
                Vector3D position;
                if (!positionQueue.TryDequeue(out position)) return false;

                MatrixD matrix;
                var gravity = (Vector3D)VRageUtils.CalculateNaturalGravity(position);
                var up = gravity.Normalized() * -1;
                if (up != Vector3.Zero) // planet/moon
                {
                    // modify position
                    var overground = position + up * Sphere.Radius;
                    var underground = position + up * (Sphere.Radius * -1);
                    var line = new LineD(overground, underground);
                    Vector3D intersection;
                    if (!TryGetVoxelIntersection(line, voxelMaps, out intersection)) continue;
                    if (Sphere.Contains(intersection) != ContainmentType.Contains) continue;

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

                // check collision
                var sphere = new BoundingSphereD(position, Clearance);
                var entities = new List<MyEntity>();
                MyGamePruningStructure.GetAllEntitiesInSphere(ref sphere, entities, MyEntityQueryType.Both);
                if (entities.Any(e => e is IMyCubeGrid)) continue;

                if (up != Vector3.Zero) // planet/moon
                {
                    Vector3D forward;
                    if (PlayerPosition != null)
                    {
                        forward = PlayerPosition.Value - position;
                    }
                    else
                    {
                        up.CalculatePerpendicularVector(out forward);
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

                    forward.CalculatePerpendicularVector(out up);
                    matrix = MatrixD.CreateWorld(position, forward, up);
                }

                _results.Add(matrix);
            }

            return true;
        }

        static bool TryGetVoxelIntersection(LineD line, IEnumerable<MyVoxelBase> voxels, out Vector3D intersection)
        {
            foreach (var v in voxels)
            {
                Vector3D? i;
                if (v.GetIntersectionWithLine(ref line, out i) && i != null)
                {
                    intersection = i.Value;
                    return true;
                }
            }

            intersection = Vector3D.Zero;
            return false;
        }
    }
}