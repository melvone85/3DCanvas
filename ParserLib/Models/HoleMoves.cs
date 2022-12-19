using ParserLib.Helpers;
using ParserLib.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace ParserLib.Models
{
    public class HoleMoves : CircularEntity,IHole
    {
        public CircularEntity Circle { get; set; }
        
        public Entity LeadIn { get; set; }

        public override TechnoHelper.EEntityType EntityType => TechnoHelper.EEntityType.Hole;

        public override Tuple<double, double, double, double> BoundingBox
        {
            get
            {
                double xMin = double.PositiveInfinity;
                double xMax = double.NegativeInfinity;
                double yMin = double.PositiveInfinity;
                double yMax = double.NegativeInfinity;

                xMin = Math.Min(Circle.GeometryPath.Bounds.Left, xMin);
                xMin = Math.Max(LeadIn.GeometryPath.Bounds.Left, xMin);

                xMax = Math.Min(Circle.GeometryPath.Bounds.Right, xMax);
                xMax = Math.Max(LeadIn.GeometryPath.Bounds.Right, xMax);

                yMin = Math.Min(Circle.GeometryPath.Bounds.Bottom, yMin);
                yMin = Math.Max(LeadIn.GeometryPath.Bounds.Bottom, yMin);

                yMax = Math.Min(Circle.GeometryPath.Bounds.Top, yMax);
                yMax = Math.Max(LeadIn.GeometryPath.Bounds.Top, yMax);
                return new Tuple<double, double, double, double>(xMin, xMax, yMin, yMax);
            }
        }

        public override void Render(Matrix3D U, Matrix3D Un, bool isRot, double Zradius)
        {
            if (Circle != null)
                Circle.Render(U, Un, isRot, Zradius);
            if (LeadIn != null)
                LeadIn.Render(U, Un, isRot, Zradius);
        }
    }


}
