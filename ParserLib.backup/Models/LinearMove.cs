using ParserLib.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using static ParserLib.Helpers.TechnoHelper;

namespace ParserLib.Models
{
    public class LinearMove : ViewModelBase,ILine
    {

        public bool Is2DProgram { get; set; }
        private Point3D _endPoint;
        private Point3D _startPoint;

        public ELineType LineColor { get; set; }
        public EEntityType EntityType { get => EEntityType.Line; }
        public int SourceLine { get; set; }
        public bool IsBeamOn { get; set; }
        public string OriginalLine { get; set; }

        public Point3D StartPoint { get { return _startPoint; } set { _startPoint = value; } }
        public Point3D EndPoint { get { return _endPoint; } set { _endPoint = value; } }

        public PathGeometry GeometryPath { get; set; }


        public void Render(Matrix3D U, Matrix3D Un, bool isRot, double Zradius)
        {
            StartPoint = U.Transform(StartPoint);
            EndPoint = U.Transform(EndPoint);
            OnPropertyChanged("StartPoint");
            OnPropertyChanged("EndPoint");
        }

    }
}
