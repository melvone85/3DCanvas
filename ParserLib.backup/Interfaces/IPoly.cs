using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace ParserLib.Interfaces
{
    internal interface IPoly : IEntity
    {
        int Sides { get; set; }
        Point3D NormalPoint { get; set; }
        Point3D CenterPoint { get; set; }
        Point3D VertexPoint { get; set; }
        List<ILine> Lines { get; set; }

    }
}
