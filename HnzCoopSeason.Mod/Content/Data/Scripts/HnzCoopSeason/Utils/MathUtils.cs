using System;
using VRage.Library.Utils;
using VRageMath;

namespace HnzCoopSeason.Utils
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

        public static Vector3D GetRandomPositionInSphere(BoundingSphereD sphere)
        {
            var randomRadius = sphere.Radius * GetRandomNormal();
            return sphere.Center + GetRandomUnitDirection() * randomRadius;
        }

        public static Vector3D GetRandomPositionOnDisk(Vector3D center, Vector3D normal, double radius)
        {
            normal = Vector3D.Normalize(normal); // Ensure the normal is unit length

            // Generate two perpendicular vectors (tangent vectors) on the plane
            var tangent1 = Vector3D.Cross(normal, Vector3D.Right);
            if (tangent1.LengthSquared() < 1e-6) // If the normal was parallel to Right, use Up instead
            {
                tangent1 = Vector3D.Cross(normal, Vector3D.Up);
            }

            tangent1.Normalize();

            var tangent2 = Vector3D.Cross(normal, tangent1);
            tangent2.Normalize();

            // Generate a random point inside a disk of radius `radius`
            var theta = MyRandom.Instance.NextDouble() * Math.PI * 2; // Random angle
            var r = Math.Sqrt(MyRandom.Instance.NextDouble()) * radius; // Uniform distribution in disk

            // Compute final position
            var randomPoint = center + tangent1 * (r * Math.Cos(theta)) + tangent2 * (r * Math.Sin(theta));

            return randomPoint;
        }

        public static int WeightedRandom(float[] weights)
        {
            var totalWeight = 0f;
            foreach (var weight in weights)
            {
                totalWeight += weight;
            }

            var randomValue = MyRandom.Instance.NextFloat() * totalWeight;

            var cumulative = 0f;
            for (var i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (randomValue < cumulative)
                {
                    return i;
                }
            }

            return weights.Length - 1;
        }

        public static bool ContainsOrIntersects(this ContainmentType self)
        {
            switch (self)
            {
                case ContainmentType.Disjoint: return false;
                case ContainmentType.Contains: return true;
                case ContainmentType.Intersects: return true;
                default: throw new InvalidOperationException();
            }
        }
    }
}