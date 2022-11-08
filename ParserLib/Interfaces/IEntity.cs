using System.Windows.Media.Media3D;
using static ParserLib.Globals.Globals;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ParserLib.Interfaces
{
    public interface IEntity
    {
        ELineType LineColor { get; set; }//Line color is an int that rappresent the integer value of the line 1:Green 2:Blue 3:Red
        EEntityType EntityType { get; set; }
        
        int SourceLine { get; set; }//Line source from original file
        bool IsBeamOn { get; set; }

        Point3D StartPoint { get; set; }
        Point3D EndPoint { get; set; }

        string OriginalLine { get; set; }

        void Render(Matrix3D U, Matrix3D Un, bool isRot, double Zradius);




        PathGeometry Bounds { get; set; }


    }
}
