using ParserLib.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ParserLib.Models
{
    public class LinearMove : ILine, INotifyPropertyChanged
    {
        private Point3D _endPoint;
        private Point3D _startPoint;

        public Globals.Globals.ELineType LineColor { get; set; }
        public Globals.Globals.EEntityType EntityType { get; set; }
        public int SourceLine { get; set; }
        public bool IsBeamOn { get; set; }
        public string OriginalLine { get; set; }

        public Point3D StartPoint { get { return _startPoint; } set { _startPoint = value; } }
        public Point3D EndPoint { get { return _endPoint; } set { _endPoint = value; } }

        public event PropertyChangedEventHandler PropertyChanged;

        public PathGeometry Bounds { get; set; }


        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }


        public void Render(Matrix3D U, Matrix3D Un, bool isRot, double Zradius)
        {
            StartPoint = U.Transform(StartPoint);
            EndPoint = U.Transform(EndPoint);
            OnPropertyChanged("StartPoint");
            OnPropertyChanged("EndPoint");
        }





        }
}
