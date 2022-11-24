using ParserLib.Helpers;
using ParserLib.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ParserLib.Models
{
    public class PolyMoves : IPoly
    {
        public int Sides { get;set; }
        public Point3D ViaPoint { get;set; }
        public Point3D NormalPoint { get;set; }
        public Point3D CenterPoint { get;set; }
        public Size ArcSize { get;set; }
        public Vector3D Normal { get;set; }
        public SweepDirection ArcSweepDirection { get;set; }
        public double Radius { get;set; }
        public double RotationAngle { get;set; }
        public bool IsLargeArc { get;set; }
        public bool IsStroked { get;set; }
        public bool IsRotating { get;set; }
        public TechnoHelper.ELineType LineColor { get;set; }

        public TechnoHelper.EEntityType EntityType => TechnoHelper.EEntityType.Poly;

        public int SourceLine { get;set; }
        public bool IsBeamOn { get;set; }
        public Point3D StartPoint { get;set; }
        public Point3D EndPoint { get;set; }
        public bool Is2DProgram { get;set; }
        public string OriginalLine { get;set; }
        public PathGeometry GeometryPath { get;set; }
        public Point3D VertexPoint { get; set; }
        public List<ILine> Lines { get; set; }

        public void Render(Matrix3D U, Matrix3D Un, bool isRot, double Zradius)
        {
            foreach (var item in Lines)
            {
                item.Render(U,Un, isRot, Zradius);
            }
        }
    }
}
