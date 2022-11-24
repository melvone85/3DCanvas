using System.Windows.Media;
using System.Windows.Media.Media3D;
using static ParserLib.Helpers.TechnoHelper;

namespace ParserLib.Interfaces
{
    public interface IEntity:IBaseEntity
    {
 


        Point3D StartPoint { get; set; }
        Point3D EndPoint { get; set; }



        PathGeometry GeometryPath { get; set; }
    }
}
