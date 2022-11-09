using ParserLib.Interfaces;
using System.Windows.Media.Media3D;
using static ParserLib.Entities.Helpers.TechnoHelper;

namespace ParserLib.Models
{
    public class ProgramContext : IProgramContext
    {
        public IEntity ReferenceMove { get; set; }
        public IEntity LastEntity { get; set; }
        public ELineType ContourLineType { get; set; }

        public bool IsIncremental { get; set; }
        public bool InMainProgram { get; set; }
        public bool IsBeamOn { get; set; }
        public bool IsMarkingProgram { get; set; }
        public int SourceLine { get; set; }
        public bool IsInchProgram { get; set; }
        public bool Is3DProgram { get; set; }
        public bool Is2DProgram { get; set; }
        public bool IsTubeProgram { get; set; }
        public bool IsWeldProgram { get; set; }
    }
}
