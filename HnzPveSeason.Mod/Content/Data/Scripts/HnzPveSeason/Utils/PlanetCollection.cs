using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace HnzPveSeason.Utils
{
    public static class PlanetCollection
    {
        static readonly HashSet<MyPlanet> _planets;

        static PlanetCollection()
        {
            _planets = new HashSet<MyPlanet>();
        }

        public static void Load()
        {
            var entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities);
            foreach (var entity in entities)
            {
                OnEntityAdd(entity);
            }

            MyEntities.OnEntityAdd += OnEntityAdd;
            MyEntities.OnEntityRemove += OnEntityRemove;
        }

        public static void Unload()
        {
            MyEntities.OnEntityAdd -= OnEntityAdd;
            MyEntities.OnEntityRemove -= OnEntityRemove;

            _planets.Clear();
        }

        static void OnEntityAdd(IMyEntity entity)
        {
            var planet = entity as MyPlanet;
            if (planet != null)
            {
                _planets.Add(planet);
            }
        }

        static void OnEntityRemove(IMyEntity entity)
        {
            var planet = entity as MyPlanet;
            if (planet != null)
            {
                _planets.Remove(planet);
            }
        }

        public static MyPlanet GetClosestPlanet(Vector3D position)
        {
            var closestPlanet = default(MyPlanet);
            var shortestDistance = double.MaxValue;
            foreach (var planet in _planets)
            {
                var pos = planet.PositionComp.GetPosition();
                var distance = Vector3D.Distance(pos, position);
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    closestPlanet = planet;
                }
            }

            return closestPlanet;
        }
    }
}