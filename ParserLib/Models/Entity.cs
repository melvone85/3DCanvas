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
    public abstract class Entity : ViewModelBase, IEntity
    {
        public virtual Point3D EndPoint { get; set; }
        public virtual Point3D StartPoint { get; set; }
        public virtual PathGeometry GeometryPath { get; set; } = new PathGeometry();


        public abstract Tuple<double, double, double, double> BoundingBox { get; }

        public virtual TechnoHelper.ELineType LineColor { get; set; }

        public virtual TechnoHelper.EEntityType EntityType {get;set;}

        public virtual int SourceLine               { get; set; }
        public virtual bool IsBeamOn                { get; set; }
        public virtual bool Is2DProgram             { get; set; }
        public virtual string OriginalLine { get; set; }

        public abstract void Render(Matrix3D U, Matrix3D Un, bool isRot, double Zradius);

        public override string ToString()
        {
            return "Ciao";
        }

    }
}
