using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using static ParserLib.Helpers.TechnoHelper;

namespace ParserLib.Models
{
    public class ArcMove : CircularEntity
    {
        private double strokeThickness = 1;
        private double degreeToRad = Math.PI / 180;
        private Vector3D vpn = new Vector3D(0, 0, 1);
        private Vector VectorForRotationAngleCalculation = new Vector(1, 0);

        public override EEntityType EntityType { get => EEntityType.Arc; }

        public override void Render(Matrix3D U, Matrix3D Un, bool isRot, double Zradius)
        {
            Normal = Un.Transform(Normal);
            Normal = Vector3D.Multiply(1 / Normal.Length, Normal);

            CenterPoint = U.Transform(CenterPoint);
            StartPoint = U.Transform(StartPoint);
            EndPoint = U.Transform(EndPoint);
            Radius *= Zradius;

            var angleBetweenNormalAndViewPlaneNormal = Vector3D.AngleBetween(vpn, Normal);

            Vector3D intersection = Vector3D.CrossProduct(vpn, Normal);
            intersection = Vector3D.Multiply(1 / intersection.Length, intersection);

            double e = Radius * degreeToRad * Math.Abs(angleBetweenNormalAndViewPlaneNormal - 90);
            RotationAngle = -Vector.AngleBetween(new Vector(intersection.X, intersection.Y), VectorForRotationAngleCalculation);

            if (e < strokeThickness / 2)
            {
                Point3D StartPointDump = StartPoint;
                Point3D EndPointDump = EndPoint;
                bool IsLargeDump = IsLargeArc;

                Point3D SP = StartPoint;
                Point3D EP = EndPoint;

                Vector3D vZ = Normal;
                Vector3D vY = intersection;
                Vector3D vX = Vector3D.CrossProduct(Normal, intersection);
                vX = Vector3D.Multiply(1 / vX.Length, vX);

                Point3D Px = new Point3D(1, 0, 0);
                Point3D Py = new Point3D(0, 1, 0);
                Point3D Pz = new Point3D(0, 0, 1);

                Px = (Point3D)Point3D.Subtract(Px, CenterPoint);
                Px = new Point3D(Vector3D.DotProduct((Vector3D)Px, vX), Vector3D.DotProduct((Vector3D)Px, vY), Vector3D.DotProduct((Vector3D)Px, vZ));

                Py = (Point3D)Point3D.Subtract(Py, CenterPoint);
                Py = new Point3D(Vector3D.DotProduct((Vector3D)Py, vX), Vector3D.DotProduct((Vector3D)Py, vY), Vector3D.DotProduct((Vector3D)Py, vZ));

                Pz = (Point3D)Point3D.Subtract(Pz, CenterPoint);
                Pz = new Point3D(Vector3D.DotProduct((Vector3D)Pz, vX), Vector3D.DotProduct((Vector3D)Pz, vY), Vector3D.DotProduct((Vector3D)Pz, vZ));

                Point3D revCenterPoint = new Point3D(-CenterPoint.X, -CenterPoint.Y, -CenterPoint.Z);
                revCenterPoint = new Point3D(Vector3D.DotProduct((Vector3D)revCenterPoint, vX), Vector3D.DotProduct((Vector3D)revCenterPoint, vY), Vector3D.DotProduct((Vector3D)revCenterPoint, vZ));

                SP = (Point3D)Point3D.Subtract(SP, CenterPoint);
                SP = new Point3D(Vector3D.DotProduct((Vector3D)SP, vX), Vector3D.DotProduct((Vector3D)SP, vY), Vector3D.DotProduct((Vector3D)SP, vZ));
                EP = (Point3D)Point3D.Subtract(EP, CenterPoint);
                EP = new Point3D(Vector3D.DotProduct((Vector3D)EP, vX), Vector3D.DotProduct((Vector3D)EP, vY), Vector3D.DotProduct((Vector3D)EP, vZ));

                double SP_ang = Math.Atan2(SP.Y, SP.X);
                double EP_ang = Math.Atan2(EP.Y, EP.X);

                if (SP_ang < 0) { SP_ang = Math.PI * 2 + SP_ang; }
                if (EP_ang < 0) { EP_ang = Math.PI * 2 + EP_ang; }

                double maxY = Radius;
                double minY = -Radius;

                int qIni = (int)(SP_ang / (Math.PI / 2)) + 1;
                int qEnd = (int)(EP_ang / (Math.PI / 2)) + 1;

                if (((qIni == qEnd) && (!IsLargeArc)) || (qIni == 1 && qEnd == 4) || (qIni == 3 && qEnd == 2)) { maxY = Math.Max(SP.Y, EP.Y); minY = Math.Min(SP.Y, EP.Y); }
                else if ((qIni == 2 && qEnd == 1) || (qIni == 2 && qEnd == 4) || (qIni == 3 && qEnd == 1) || (qIni == 3 && qEnd == 4)) { maxY = Radius; minY = Math.Min(SP.Y, EP.Y); }
                else if ((qIni == 1 && qEnd == 3) || (qIni == 1 && qEnd == 2) || (qIni == 4 && qEnd == 3) || (qIni == 4 && qEnd == 2)) { maxY = Math.Max(SP.Y, EP.Y); minY = -Radius; }

                SP = new Point3D(0, minY, 0);
                EP = new Point3D(0, maxY, 0);

                Vector3D vX_ = Point3D.Subtract(Px, revCenterPoint);
                vX_ = Vector3D.Multiply(1 / vX_.Length, vX_);

                Vector3D vY_ = Point3D.Subtract(Py, revCenterPoint);
                vY_ = Vector3D.Multiply(1 / vY_.Length, vY_);

                Vector3D vZ_ = Point3D.Subtract(Pz, revCenterPoint);
                vZ_ = Vector3D.Multiply(1 / vZ_.Length, vZ_);

                double A1 = Vector3D.AngleBetween(vX_, vY_);

                SP = (Point3D)Point3D.Subtract(SP, revCenterPoint);
                SP = new Point3D(Vector3D.DotProduct((Vector3D)SP, vX_), Vector3D.DotProduct((Vector3D)SP, vY_), Vector3D.DotProduct((Vector3D)SP, vZ_));

                EP = (Point3D)Point3D.Subtract(EP, revCenterPoint);
                EP = new Point3D(Vector3D.DotProduct((Vector3D)EP, vX_), Vector3D.DotProduct((Vector3D)EP, vY_), Vector3D.DotProduct((Vector3D)EP, vZ_));

                StartPoint = SP;
                EndPoint = EP;
                IsLargeArc = false;
                ArcSize = new Size(1000, 1);
                ArcSweepDirection = Normal.Z >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

                OnPropertyChanged("StartPoint");
                OnPropertyChanged("EndPoint");
                OnPropertyChanged("RotationAngle");
                OnPropertyChanged("ArcSize");
                OnPropertyChanged("ArcSweepDirection");
                OnPropertyChanged("IsLargeArc");

                StartPoint = StartPointDump;
                EndPoint = EndPointDump;
                IsLargeArc = IsLargeDump;
            }
            else
            {
                ArcSize = new Size(Radius, Math.Abs(Radius * Math.Cos(angleBetweenNormalAndViewPlaneNormal * degreeToRad)));
                ArcSweepDirection = Normal.Z >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

                OnPropertyChanged("StartPoint");
                OnPropertyChanged("EndPoint");
                OnPropertyChanged("RotationAngle");
                OnPropertyChanged("ArcSize");
                OnPropertyChanged("ArcSweepDirection");
                OnPropertyChanged("IsLargeArc");
            }
        }

        public override string ToString()
        {
            return $"Arc sp: {StartPoint} vp: {ViaPoint} ep: {EndPoint}";
        }
    }
}