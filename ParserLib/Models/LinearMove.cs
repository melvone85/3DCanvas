using System.Windows.Media.Media3D;
using static ParserLib.Helpers.TechnoHelper;

namespace ParserLib.Models
{
    public class LinearMove : Entity
    {
        public override EEntityType EntityType { get => EEntityType.Line; }

        public override void Render(Matrix3D U, Matrix3D Un, bool isRot, double Zradius)
        {
            StartPoint = U.Transform(StartPoint);
            EndPoint = U.Transform(EndPoint);
            OnPropertyChanged("StartPoint");
            OnPropertyChanged("EndPoint");
        }

        public override string ToString()
        {
            return $"Line sp: {StartPoint} ep: {EndPoint}";
        }
    }
}