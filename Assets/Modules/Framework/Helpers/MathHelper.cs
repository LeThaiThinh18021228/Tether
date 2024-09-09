using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Framework
{
    public static class MathHelper
    {
        public static double DistancePointToLine(Vector2 point, Vector2 linePoint1, Vector2 linePoint2)
        {
            double x0 = point.x;
            double y0 = point.y;
            double x1 = linePoint1.x;
            double y1 = linePoint1.y;
            double x2 = linePoint2.x;
            double y2 = linePoint2.y;

            // Calculate the numerator
            double numerator = Math.Abs((y2 - y1) * x0 - (x2 - x1) * y0 + x2 * y1 - y2 * x1);

            // Calculate the denominator
            double denominator = Math.Sqrt(Math.Pow(y2 - y1, 2) + Math.Pow(x2 - x1, 2));

            // Calculate the distance
            double distance = numerator / denominator;

            return distance;
        }
    }
}
