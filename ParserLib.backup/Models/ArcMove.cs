using ParserLib.Interfaces;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using static ParserLib.Helpers.TechnoHelper;

namespace ParserLib.Models
{
    public class ArcMove : ViewModelBase,IArc
    {
        private double degreeToRad = Math.PI / 180;
        private Vector VectorForRotationAngleCalculation = new Vector(1, 0);
        private Vector3D vpn = new Vector3D(0, 0, 1);

        public bool Is2DProgram { get; set; }
        public Point3D EndPoint { get; set; }
        public Point3D ViaPoint { get; set; }
        public Size ArcSize { get; set; }
        public double RotationAngle { get; set; }
        public SweepDirection ArcSweepDirection { get; set; }
        public bool IsLargeArc { get; set; }

        public ELineType LineColor { get; set; }
        public EEntityType EntityType { get => EEntityType.Arc; }
        public int SourceLine { get; set; }

        public string OriginalLine{ get; set; }

        public bool IsStroked { get; set; }
        public bool IsBeamOn { get; set; }
        public bool IsRotating { get; set; }
        public double Radius { get; set; }// raggio della circonferenza a cui appartiene l'arco di circonferenza
        public Vector3D Normal { get; set; }// vettore normale al piano su cui giace l'arco di circonferenze


        public PathGeometry GeometryPath { get; set; }
        public Point3D NormalPoint { get; set; }
        public Point3D CenterPoint { get; set; }
        public Point3D StartPoint { get; set; }

        private double strokeThickness = 1;






        public void Render(Matrix3D U, Matrix3D Un, bool isRot, double Zradius)

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












        //public void RedrawArc()
        //{
        //    var angleBetweenNormalAndViewPlaneNormal = Vector3D.AngleBetween(vpn, Normal);

        //    if (!(Math.Abs(angleBetweenNormalAndViewPlaneNormal - 90) < _ortotolerance) || !IsRotating)
        //    {
        //        Vector3D intersection = Vector3D.CrossProduct(vpn, Normal);
        //        RotationAngle = -Vector.AngleBetween(new Vector(intersection.X, intersection.Y), VectorForRotationAngleCalculation);
        //        ArcSize = new Size(Radius, Math.Abs(Radius * Math.Cos(angleBetweenNormalAndViewPlaneNormal * degreeToRad)));
        //        ArcSweepDirection = Normal.Z >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;
        //    }
        //    else 
        //    {
        //        //TODO cercare di capire perchè in alcuni casi la normale dell'arco è perpendicolare alla vista e il cerchio scompare
        //        //suggerimenti il cerchio/arco diventa una linea e potrebbe essere anche un arco con raggio infinito.
        //        Vector3D intersection = Vector3D.CrossProduct(new Vector3D(0.001,0.001,1), Normal);
        //        RotationAngle = -Vector.AngleBetween(new Vector(intersection.X, intersection.Y), VectorForRotationAngleCalculation);
        //        ArcSize = new Size(Radius, Math.Abs(Radius * Math.Cos(angleBetweenNormalAndViewPlaneNormal * degreeToRad)));
        //        ArcSweepDirection = Normal.Z >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

        //    }
        //}
    }
}
