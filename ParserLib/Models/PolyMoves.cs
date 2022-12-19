using ParserLib.Helpers;
using ParserLib.Interfaces;
using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace ParserLib.Models
{
    public class PolyMoves : ArcMove, IPoly
    {
        public int Sides { get; set; }

        public Point3D VertexPoint { get; set; }

        public List<Entity> Lines { get; set; }

        public Entity LeadIn { get; set; }

        public override TechnoHelper.EEntityType EntityType => TechnoHelper.EEntityType.Poly;

        public override Tuple<double, double, double, double> BoundingBox
        {
            get
            {
                double xMin = double.PositiveInfinity;
                double xMax = double.NegativeInfinity;
                double yMin = double.PositiveInfinity;
                double yMax = double.NegativeInfinity;
                foreach (var item in Lines)
                {
                    xMin = Math.Min(item.GeometryPath.Bounds.Left, xMin);
                    xMax = Math.Max(item.GeometryPath.Bounds.Right, xMax);
                    yMin = Math.Min(item.GeometryPath.Bounds.Bottom, yMin);
                    yMax = Math.Max(item.GeometryPath.Bounds.Top, yMax);
                }

                return new Tuple<double, double, double, double>(xMin, xMax, yMin, yMax);
            }
        }

        public override void Render(Matrix3D U, Matrix3D Un, bool isRot, double Zradius)
        {

            if (LeadIn != null)
                LeadIn.Render(U, Un, isRot, Zradius);
            foreach (var item in Lines)
            {
                item.Render(U, Un, isRot, Zradius);
            }
        }
    }
}