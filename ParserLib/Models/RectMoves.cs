using ParserLib.Helpers;
using ParserLib.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ParserLib.Models
{
    public class RectMoves : IRect
    {
        private double xMin = double.PositiveInfinity;
        private double xMax = double.NegativeInfinity;
        private double yMin = double.PositiveInfinity;
        private double yMax = double.NegativeInfinity;

        public Point3D SidePoint { get;set; }
        public Point3D CenterPoint { get;set; }
        public Point3D VertexPoint { get;set; }
        public List<Entity> Lines { get;set; }
        public Point3D StartPoint { get;set; }
        public Point3D EndPoint { get;set; }
        public PathGeometry GeometryPath { get;set; }
        public TechnoHelper.ELineType LineColor { get;set; }

        public TechnoHelper.EEntityType EntityType => TechnoHelper.EEntityType.Rect;

        public int SourceLine { get;set; }
        public bool IsBeamOn { get;set; }
        public bool Is2DProgram { get;set; }
        public string OriginalLine { get;set; }

        public void Render(Matrix3D U, Matrix3D Un, bool isRot, double Zradius)
        {
            foreach (var item in Lines)
            {
                item.Render(U, Un, isRot, Zradius);
            }
        }

        public Tuple<double, double, double, double> BoundingBox
        {
            get
            {

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
    }
}
