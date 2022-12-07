using ParserLib.Models;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace ParserLib.Interfaces
{
    public interface IRect : IEntity
    {
        Point3D SidePoint { get; set; }
        Point3D CenterPoint { get; set; }
        Point3D VertexPoint { get; set; }
        List<LinearMove> Lines { get; set; }
    }
}
