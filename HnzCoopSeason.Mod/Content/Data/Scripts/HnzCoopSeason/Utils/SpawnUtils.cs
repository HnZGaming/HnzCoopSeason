using System;
using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.Utils.Pools;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Utils
{
    public static class SpawnUtils
    {
        public static MatrixD? TryCalcSurfaceMatrix(BoundingSphereD sphere, float clearance)
        {
            var planet = PlanetCollection.GetClosestPlanet(sphere.Center);
            var planetCenter = planet.PositionComp.GetPosition();
            var normal = sphere.Center - planetCenter;

            var positions = new List<Vector3D> { sphere.Center };
            for (var i = 0; i < 5; i++)
            {
                var position = MathUtils.GetRandomPositionOnPlane(sphere.Center, normal, sphere.Radius);
                positions.Add(position);
            }

            positions = positions.OrderBy(p => Vector3D.Distance(p, sphere.Center)).ToList();

            foreach (var _position in positions)
            {
                var position = _position;

                // no spawning inside the voxel
                position = planet.GetClosestSurfacePointGlobal(position);

                // check for clearance
                var space = new BoundingSphereD(position, clearance);
                var blocked = HasAnyEntitiesInSphere<IMyCubeGrid>(space);

                if (!MyAPIGateway.Utilities.IsDedicated)
                {
                    var name = $"position ({(blocked ? 'x' : 'o')})";
                    var color = blocked ? Color.Red : Color.Blue;
                    VRageUtils.AddTemporaryGps(name, color, 20, position);
                }

                if (blocked) continue;

                return CreateWorld(position, position - planetCenter);
            }

            return null;
        }

        public static MatrixD? TryCalcOrbitMatrix(BoundingSphereD sphere, float clearance)
        {
            var planet = PlanetCollection.GetClosestPlanet(sphere.Center);
            var planetCenter = planet.PositionComp.GetPosition();
            var normal = sphere.Center - planetCenter;

            var positions = new List<Vector3D> { sphere.Center };
            for (var i = 0; i < 5; i++)
            {
                var position = MathUtils.GetRandomPositionOnPlane(sphere.Center, normal, sphere.Radius);
                positions.Add(position);
            }

            positions = positions.OrderBy(p => Vector3D.Distance(p, sphere.Center)).ToList();

            foreach (var _position in positions)
            {
                var position = _position;

                // no spawning inside the voxel
                var surfacePosition = planet.GetClosestSurfacePointGlobal(position);
                position = surfacePosition + (surfacePosition - planetCenter).Normalized() * clearance;

                // check for clearance
                var space = new BoundingSphereD(position, clearance);
                if (HasAnyEntitiesInSphere<IMyCubeGrid>(space)) continue;

                return CreateWorld(position, position - planetCenter);
            }

            return null;
        }

        static MatrixD CreateWorld(Vector3D position, Vector3D up)
        {
            var random = new Random();
            var r = up + new Vector3D(random.NextDouble() + 1, random.NextDouble() + 1, random.NextDouble() + 1);
            var right = Vector3D.Cross(up, r);
            var forward = Vector3D.Cross(right, up);
            return MatrixD.CreateWorld(position, forward.Normalized(), up.Normalized());
        }

        public static MatrixD? TryCalcSpaceMatrix(BoundingSphereD sphere, float clearance)
        {
            var positions = new List<Vector3D> { sphere.Center };
            for (var i = 0; i < 100; i++)
            {
                var position = MathUtils.GetRandomPosition(sphere);
                positions.Add(position);
            }

            positions = positions.OrderBy(p => Vector3D.Distance(p, sphere.Center)).ToList();

            foreach (var position in positions)
            {
                // check for clearance
                var space = new BoundingSphereD(position, clearance);
                if (HasAnyEntitiesInSphere<IMyEntity>(space)) continue;

                return MatrixD.CreateWorld(position);
            }

            return null;
        }

        static bool HasAnyEntitiesInSphere<T>(BoundingSphereD sphere) where T : IMyEntity
        {
            var entities = ListPool<MyEntity>.Get();
            try
            {
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, entities);
                return entities.OfType<T>().Any();
            }
            finally
            {
                ListPool<MyEntity>.Release(entities);
            }
        }

        public static bool TryCalcMatrix(SpawnType type, BoundingSphereD sphere, float clearance, out MatrixD matrix)
        {
            switch (type)
            {
                case SpawnType.PlanetaryShip: return Cnv(TryCalcOrbitMatrix(sphere, clearance), out matrix);
                case SpawnType.PlanetaryStation: return Cnv(TryCalcSurfaceMatrix(sphere, clearance), out matrix);
                case SpawnType.SpaceShip: return Cnv(TryCalcSpaceMatrix(sphere, clearance), out matrix);
                case SpawnType.SpaceStation: return Cnv(TryCalcSpaceMatrix(sphere, clearance), out matrix);
                default: throw new InvalidOperationException($"Unknown spawn type: {type}");
            }
        }

        static bool Cnv(MatrixD? m, out MatrixD om)
        {
            if (m == null)
            {
                om = MatrixD.Identity;
                return false;
            }

            om = m.Value;
            return true;
        }
    }
}