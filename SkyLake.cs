using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Taimi.UndaDaSea_BlishHUD
{
    // Thank to Teh from the BlishHUD discord for this class 🥰
    public class SkyLake
    {
        private readonly float _waterSurface;
        private readonly float _waterBottom;
        private readonly List<Vector3> bounds;
        private readonly Vector3 center;
        private readonly float radius;
        private float _distance;

        public SkyLake(float waterSurface, float waterBottom, List<Vector3> bounds)
        {
            _waterSurface = waterSurface;
            _waterBottom = waterBottom;
            this.bounds = bounds;
            center = GetCenter();
            radius = GetRadius();
        }

        public float WaterSurface
        {
            get { return _waterSurface; }
        }

        public float Distance
        {
            get { return _distance; }
        }

        //so we don't have to perform the more expensive calcs all the time
        public bool IsNearby(Vector3 playerPos)
        {
            _distance = Vector3.Distance(playerPos, center);
            if (_distance > radius * 1.5) return false; // arbitrary distance

            return true;
        }

        //water check
        public bool IsInWater(Vector3 playerPos)
        {
            //if not within top/bottom, not in water
            if (playerPos.Z < _waterBottom || playerPos.Z > (_waterSurface + 10))
                return false;

            return IsInPolygon(playerPos);
        }

        //Point in Polygon algorithm
        private bool IsInPolygon(Vector3 playerPos)
        {
            var count = bounds.Count;
            bool isInside = false;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                bool isYAboveFirstVertex = bounds[i].Y > playerPos.Y;
                bool isYAboveSecondVertex = bounds[j].Y > playerPos.Y;

                if (isYAboveFirstVertex != isYAboveSecondVertex)
                {
                    float intersectionX = bounds[i].X + (playerPos.Y - bounds[i].Y) / (bounds[j].Y - bounds[i].Y) * (bounds[j].X - bounds[i].X);
                    if (playerPos.X < intersectionX)
                    {
                        isInside = !isInside;
                    }
                }
            }

            return isInside;
        }

        //get center of polygon
        private Vector3 GetCenter()
        {
            Vector3 center = new Vector3(0, 0, 0);

            foreach (var bound in bounds)
            {
                center += bound;
            }

            center /= bounds.Count;
            return center;
        }

        //get radius of polygon
        private float GetRadius()
        {
            float radius = 0;
            foreach (var bound in bounds)
            {
                float distance = Vector3.Distance(bound, center);
                if (distance > radius)
                    radius = distance;
            }

            return radius;
        }
    }
}