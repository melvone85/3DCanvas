﻿using ParserLib.Helpers;
using ParserLib.Interfaces;
using System;
using System.Windows.Media.Media3D;

namespace ParserLib.Models
{
    public class KeyholeMoves : Entity,IKeyhole
    {

        private double xMin = double.PositiveInfinity;
        private double xMax = double.NegativeInfinity;
        private double yMin = double.PositiveInfinity;
        private double yMax = double.NegativeInfinity;

        public CircularEntity Arc1 {get; set; }
        public CircularEntity Arc2 {get; set; }

        public Entity LeadIn { get; set; }
        public Entity Line1 {get; set; }
        public Entity Line2 {get; set; }

        public override Tuple<double, double, double, double> BoundingBox {
            get
            {

                xMin = Math.Min(Arc1.GeometryPath.Bounds.Left, xMin);
                xMin = Math.Max(Arc2.GeometryPath.Bounds.Left, xMin);
                xMin = Math.Min(Line1.GeometryPath.Bounds.Left, xMin);
                xMin = Math.Max(Line2.GeometryPath.Bounds.Left, xMin);

                xMax = Math.Min(Arc1.GeometryPath.Bounds.Right, xMax);
                xMax = Math.Max(Arc2.GeometryPath.Bounds.Right, xMax);
                xMax = Math.Min(Line1.GeometryPath.Bounds.Right, xMax);
                xMax = Math.Max(Line2.GeometryPath.Bounds.Right, xMax);

                yMin = Math.Min(Arc1.GeometryPath.Bounds.Bottom, yMin);
                yMin = Math.Max(Arc2.GeometryPath.Bounds.Bottom, yMin);
                yMin = Math.Min(Line1.GeometryPath.Bounds.Bottom, yMin);
                yMin = Math.Max(Line2.GeometryPath.Bounds.Bottom, yMin);

                yMax = Math.Min(Arc1.GeometryPath.Bounds.Top, yMax);
                yMax = Math.Max(Arc2.GeometryPath.Bounds.Top, yMax);
                yMax = Math.Min(Line1.GeometryPath.Bounds.Top, yMax);
                yMax = Math.Max(Line2.GeometryPath.Bounds.Top, yMax);
                return new Tuple<double, double, double, double>(xMin, xMax, yMin, yMax);
            }
        }

        public override TechnoHelper.EEntityType EntityType => TechnoHelper.EEntityType.Keyhole;


        public override void Render(Matrix3D U, Matrix3D Un, bool isRot, double Zradius)
        {
            if (Arc1 != null)
                Arc1.Render(U, Un, isRot, Zradius);
            if (Arc2 != null)
                Arc2.Render(U, Un, isRot, Zradius);
            if (Line1 != null)
                Line1.Render(U, Un, isRot, Zradius);
            if (Line2 != null)
                Line2.Render(U, Un, isRot, Zradius);
            if (LeadIn != null)
                LeadIn.Render(U, Un, isRot, Zradius);
            //throw new NotImplementedException();
        }
    }
}
