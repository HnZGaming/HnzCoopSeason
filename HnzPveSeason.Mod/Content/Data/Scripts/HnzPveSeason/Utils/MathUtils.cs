﻿using System;
using VRage.Library.Utils;
using VRageMath;

namespace HnzPveSeason.Utils
{
    public static class MathUtils
    {
        public static float GetRandomNormal()
        {
            return (float)MyRandom.Instance.Next(0, 100) / 100;
        }

        public static Vector3D GetRandomUnitDirection()
        {
            var dir = new Vector3D(
                MyRandom.Instance.GetRandomFloat(-1f, 1f),
                MyRandom.Instance.GetRandomFloat(-1f, 1f),
                MyRandom.Instance.GetRandomFloat(-1f, 1f));
            dir.Normalize();

            return dir;
        }

        public static Vector3D GetRandomPosition(BoundingSphereD sphere)
        {
            var randomRadius = sphere.Radius * GetRandomNormal();
            return sphere.Center + GetRandomUnitDirection() * randomRadius;
        }

        public static Vector3D GetRandomPosition(Vector3D center, Vector3D normal, double radius)
        {
            var p = new PlaneD(Vector3D.Zero, normal);
            return p.RandomPoint() * radius + center;
        }

        public static int WeightedRandom(float[] weights)
        {
            var totalWeight = 0f;
            foreach (var weight in weights)
            {
                totalWeight += weight;
            }

            var random = new Random();
            var randomNumber = random.NextDouble() * totalWeight;

            for (var i = 0; i < weights.Length; i++)
            {
                if (randomNumber < weights[i])
                {
                    return i;
                }

                randomNumber -= weights[i];
            }

            return weights.Length - 1;
        }
    }
}