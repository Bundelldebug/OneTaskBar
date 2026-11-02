using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class SquircleGeometry
    {
        // The supplied Samsung Browser silhouette follows a superellipse near n=2.8.
        // Sampling that curve avoids the long straight sides of a rounded rectangle.
        internal const double Exponent = 2.8;
        internal static readonly string PathData = BuildPath();
        static readonly Geometry unit = Geometry.Parse(PathData);
        static Point At(int index, int count)
        {
            double angle = index * Math.PI * 2 / count;
            double x = Math.Sin(angle), y = -Math.Cos(angle), power = 2 / Exponent;
            return new Point(50 + 50 * SignedPower(x, power), 50 + 50 * SignedPower(y, power));
        }

        static double SignedPower(double value, double power)
        {
            return Math.Abs(value) < 1e-12 ? 0 : Math.Sign(value) * Math.Pow(Math.Abs(value), power);
        }

        static string Coordinates(Point value)
        {
            return value.X.ToString("0.#####", CultureInfo.InvariantCulture) + "," + value.Y.ToString("0.#####", CultureInfo.InvariantCulture);
        }

        static string BuildPath()
        {
            const int count = 96;
            var path = new StringBuilder("M" + Coordinates(At(0, count)));
            for (int i = 0; i < count; i++)
            {
                Point previous = At(i - 1, count), start = At(i, count), end = At(i + 1, count), next = At(i + 2, count);
                var first = start + (end - previous) / 6;
                var second = end - (next - start) / 6;
                path.Append(" C").Append(Coordinates(first)).Append(" ").Append(Coordinates(second)).Append(" ").Append(Coordinates(end));
            }

            return path.Append(" Z").ToString();
        }

        internal static Geometry Create(Rect bounds)
        {
            var shape = unit.Clone();
            shape.Transform = new MatrixTransform(bounds.Width / 100, 0, 0, bounds.Height / 100, bounds.X, bounds.Y);
            shape.Freeze();
            return shape;
        }
    }
}
