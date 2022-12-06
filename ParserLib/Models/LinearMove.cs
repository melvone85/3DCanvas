using ParserLib.Interfaces;
using System;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using static ParserLib.Helpers.TechnoHelper;

namespace ParserLib.Models
{
    public class LinearMove : Entity
    {

        private Point3D _endPoint;
        private Point3D _startPoint;

        public override EEntityType EntityType { get => EEntityType.Line; }
        public override Point3D StartPoint { get { return _startPoint; } set { _startPoint = value; } }
        public override Point3D EndPoint { get { return _endPoint; } set { _endPoint = value; } }
        public override Tuple<double, double, double, double> BoundingBox => new Tuple<double, double, double, double>(GeometryPath.Bounds.Left, GeometryPath.Bounds.Right, GeometryPath.Bounds.Bottom, GeometryPath.Bounds.Top);

        public override string ToString()
        {
            return $"Line sp: {StartPoint} ep: {EndPoint}";
        }

        public override void Render(Matrix3D U, Matrix3D Un, bool isRot, double Zradius)
        {
            StartPoint = U.Transform(StartPoint);
            EndPoint = U.Transform(EndPoint);
            OnPropertyChanged("StartPoint");
            OnPropertyChanged("EndPoint");
        }
    }
}
