using System;
using System.Collections.Generic;
using HnzPveSeason.Utils.Pools;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;

namespace HnzPveSeason.Utils
{
    public static class SpawnUtils
    {
        public static MatrixD? TryCalcMatrix(SpawnEnvironment environment, BoundingSphereD sphere, float clearance)
        {
            switch (environment)
            {
                case SpawnEnvironment.Planet: return TryCalcSurfaceMatrix(sphere, clearance);
                case SpawnEnvironment.Space: return TryCalcSpaceMatrix(sphere, clearance);
                default: throw new InvalidOperationException($"unsupported spawn environment: {environment}");
            }
        }

        static MatrixD? TryCalcSurfaceMatrix(BoundingSphereD sphere, float clearance)
        {
            var gravity = VRageUtils.CalculateNaturalGravity(sphere.Center);
            var spawnPlane = new PlaneD(sphere.Center, gravity);
            var planet = PlanetCollection.GetClosestPlanet(sphere.Center);

            var positions = new List<Vector3D> { sphere.Center };
            for (var i = 0; i < 100; i++)
            {
                var position = MathUtils.GetRandomPosition(spawnPlane, sphere.Radius);
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
            var gravity = VRageUtils.CalculateNaturalGravity(sphere.Center);
            var spawnPlane = new PlaneD(sphere.Center, gravity);

            var positions = new List<Vector3D> { sphere.Center };
            for (var i = 0; i < 100; i++)
            {
                var position = MathUtils.GetRandomPosition(spawnPlane, sphere.Radius);
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
                foreach (var entity in entities)
                {
                    if (entity is T)
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                ListPool<MyEntity>.Release(entities);
            }
        }
    }
}