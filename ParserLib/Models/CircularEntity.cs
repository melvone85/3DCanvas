using ParserLib.Interfaces;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using static ParserLib.Helpers.TechnoHelper;

namespace ParserLib.Models
{
    public class CircularEntity : Entity, IArc
    {
        public bool IsStroked { get; set; }

        public bool IsRotating { get; set; }

        public bool IsLargeArc { get; set; }

        public double Radius { get; set; }// raggio della circonferenza a cui appartiene l'arco di circonferenza

        public double RotationAngle { get; set; }

        public Vector3D Normal { get; set; }// vettore normale al piano su cui giace l'arco di circonferenze

        public Point3D ViaPoint { get; set; }

        public Point3D NormalPoint { get; set; }

        public Point3D CenterPoint { get; set; }

        public Size ArcSize { get; set; }

        public SweepDirection ArcSweepDirection { get; set; }

        public override EEntityType EntityType => throw new NotImplementedException();

        public override void Render(Matrix3D U, Matrix3D Un, bool isRot, double Zradius)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return $"Arc sp: {StartPoint} vp: {ViaPoint} ep: {EndPoint}";
        }
    }
}