using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using static ParserLib.Entities.Helpers.TechnoHelper;

namespace ParserLib.Interfaces
{
    public interface IProgramContext
    {
        IEntity ReferenceMove { get; set; }
        IEntity LastEntity { get; set; }
        bool Is3DProgram { get; set; }
        bool Is2DProgram { get; set; }
        bool IsTubeProgram { get; set; }
        bool IsWeldProgram { get; set; }
        bool IsIncremental { get; set; }
        bool IsInchProgram { get; set; }    
        bool InMainProgram { get; set; } 
        bool IsBeamOn { get; set; }
        bool IsMarkingProgram { get; set; }
        int SourceLine { get; set; }
        ELineType ContourLineType { get; set; }
    }
}
