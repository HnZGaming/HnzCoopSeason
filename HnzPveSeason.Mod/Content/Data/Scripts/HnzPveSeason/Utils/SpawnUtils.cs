using System;
using System.Collections.Generic;
using System.Linq;
using HnzPveSeason.Utils.Pools;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace HnzPveSeason.Utils
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

            foreach (var _position in positions)
            {
                var position = _position;

                // prevent digging into the planet surface
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

            foreach (var _position in positions)
            {
                var position = _position;

                // prevent digging into the planet surface
                var surfacePosition = planet.GetClosestSurfacePointGlobal(_position);
                if (Vector3D.Distance(_position, planetCenter) < Vector3D.Distance(surfacePosition, planetCenter))
                {
                    position = surfacePosition + (surfacePosition - planetCenter).Normalized() * clearance;
                }

                // check for clearance
                var space = new BoundingSphereD(position, clearance);
                if (HasAnyEntitiesInSphere<IMyCubeGrid>(space)) continue;

                return CreateWorld(_position, position - planetCenter);
            }

            return null;
        }

        static MatrixD CreateWorld(Vector3D position, Vector3D up)
        {
            var random = new Random();
            var r = up + new Vector3D(random.NextDouble() + 1, random.NextDouble() + 1, random.NextDouble() + 1);
            var right = Vector3D.Cross(up, r);
            var forward = Vector3D.Cross(right, up);

            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                VRageUtils.AddTemporaryGps("center", Color.White, 20, position);
                VRageUtils.AddTemporaryGps("up", Color.White, 20, position + up.Normalized() * 20);
                VRageUtils.AddTemporaryGps("forward", Color.White, 20, position + forward.Normalized() * 20);
            }

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
    }
}