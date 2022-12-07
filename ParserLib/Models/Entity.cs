using ParserLib.Helpers;
using ParserLib.Interfaces;
using System;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ParserLib.Models
{
    public abstract class Entity : ViewModelBase, IEntity
    {
        private Point3D _endPoint;
        private Point3D _startPoint;
        public Point3D StartPoint { get { return _startPoint; } set { _startPoint = value; } }
        public Point3D EndPoint { get { return _endPoint; } set { _endPoint = value; } }
        public PathGeometry GeometryPath { get; set; } = new PathGeometry();

        public virtual Tuple<double, double, double, double> BoundingBox => new Tuple<double, double, double, double>(GeometryPath.Bounds.Left, GeometryPath.Bounds.Right, GeometryPath.Bounds.Bottom, GeometryPath.Bounds.Top);

        public TechnoHelper.ELineType LineColor { get; set; }

        public abstract TechnoHelper.EEntityType EntityType {get;}

        public int SourceLine               { get; set; }
        public bool IsBeamOn                { get; set; }
        public bool Is2DProgram             { get; set; }
        public string OriginalLine { get; set; }

        public abstract void Render(Matrix3D U, Matrix3D Un, bool isRot, double Zradius);
    }
}
