using System;
using System.Collections.Generic;
using System.Linq;
using HnzPveSeason.Utils.Pools;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason.Utils
{
    public static class SpawnUtils
    {
        public static MatrixD? TryCalcMatrix(SpawnEnvironment environment, BoundingSphereD sphere, float clearance)
        {
            switch (environment)
            {
                case SpawnEnvironment.PlanetSurface: return TryCalcSurfaceMatrix(sphere, clearance);
                case SpawnEnvironment.PlanetOrbit: return TryCalcOrbitMatrix(sphere, clearance);
                case SpawnEnvironment.Space: return TryCalcSpaceMatrix(sphere, clearance);
                default: throw new InvalidOperationException($"unsupported spawn environment: {environment}");
            }
        }

        static MatrixD? TryCalcSurfaceMatrix(BoundingSphereD sphere, float clearance)
        {
            var planet = PlanetCollection.GetClosestPlanet(sphere.Center);
            var planetCenter = planet.PositionComp.GetPosition();
            var normal = sphere.Center - planetCenter;

            var positions = new List<Vector3D> { sphere.Center };
            for (var i = 0; i < 100; i++)
            {
                var position = MathUtils.GetRandomPosition(sphere.Center, normal, sphere.Radius);
                position = planet.GetClosestSurfacePointGlobal(position);
                positions.Add(position);
            }

            foreach (var position in positions)
            {
                // check for clearance
                var space = new BoundingSphereD(position, clearance);
                if (HasAnyEntitiesInSphere<IMyEntity>(space)) continue;

                return CalcMatrix(position);
            }

            return null;
        }

        static MatrixD? TryCalcOrbitMatrix(BoundingSphereD sphere, float clearance)
        {
            var planet = PlanetCollection.GetClosestPlanet(sphere.Center);
            var planetCenter = planet.PositionComp.GetPosition();
            var normal = sphere.Center - planetCenter;

            var positions = new List<Vector3D> { sphere.Center };
            for (var i = 0; i < 5; i++)
            {
                var position = MathUtils.GetRandomPosition(sphere.Center, normal, sphere.Radius);
                positions.Add(position);
            }

            foreach (var position in positions)
            {
                var p = position;

                // prevent digging into the planet surface
                var surfacePosition = planet.GetClosestSurfacePointGlobal(position);
                if (Vector3D.Distance(position, planetCenter) < Vector3D.Distance(surfacePosition, planetCenter))
                {
                    p = surfacePosition + (surfacePosition - planetCenter).Normalized() * clearance;
                }

                // check for clearance
                var space = new BoundingSphereD(p, clearance);
                if (HasAnyEntitiesInSphere<IMyCubeGrid>(space)) continue;

                return CalcMatrix(p);
            }

            return null;
        }

        static MatrixD? TryCalcSpaceMatrix(BoundingSphereD sphere, float clearance)
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

                return CalcMatrix(position);
            }

            return null;
        }

        static MatrixD CalcMatrix(Vector3D position)
        {
            var gravity = VRageUtils.CalculateNaturalGravity(position);
            return MatrixD.CreateWorld(position, Vector3.Forward, gravity * -1f);
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