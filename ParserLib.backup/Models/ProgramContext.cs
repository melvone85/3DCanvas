using ParserLib.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using static ParserLib.Helpers.TechnoHelper;

namespace ParserLib.Models
{
    public class ProgramContext : IProgramContext
    {
        private double xMin { get; set; }
        private double xMax { get; set; }
        private double yMin { get; set; }
        private double yMax { get; set; }
        private double zMin { get; set; }
        private double zMax { get; set; }
        public IEntity ReferenceMove { get; set; }
        public IEntity LastEntity { get; set; }
        public ELineType ContourLineType { get; set; }

        public Point3D CenterRotationPoint { get; set; }

        public int SourceLine { get; set; }
        public bool IsIncremental { get; set; }
        public bool InMainProgram { get; set; }
        public bool IsBeamOn { get; set; }
        public bool IsMarkingProgram { get; set; }
        public bool IsInchProgram { get; set; }
        public bool Is3DProgram { get; set; }
        public bool Is2DProgram { get; set; }
        public bool IsTubeProgram { get; set; }
        public bool IsWeldProgram { get; set; }
        public IList<IBaseEntity> Moves { get; set; }

        public void UpdateProgramCenterPoint() {
            if (LastEntity != null && IsBeamOn) {
                if (LastEntity.EntityType == EEntityType.Poly)
                {
                    var poly = LastEntity as IPoly;

                    foreach (var item in poly.Lines)
                    {
                        CalculateMinMaxFromBaseEntity(item);
                    }
                }
                else if (LastEntity.EntityType == EEntityType.Slot)
                {
                    var slot = LastEntity as ISlot;

                    CalculateMinMaxFromBaseEntity(slot.Arc1);
                    CalculateMinMaxFromBaseEntity(slot.Arc2);
                    CalculateMinMaxFromBaseEntity(slot.Line1);
                    CalculateMinMaxFromBaseEntity(slot.Line2);
                }
                else
                {
                    CalculateMinMaxFromBaseEntity(LastEntity);
                }

                CenterRotationPoint = new Point3D((yMin + yMax) / 2, (xMin + xMax) / 2, (zMin + zMax) / 2);
            }
        }

        private void CalculateMinMaxFromBaseEntity(IEntity BaseEntity)
        {
            xMin = Math.Min(BaseEntity.StartPoint.X, xMin);
            xMin = Math.Min(BaseEntity.EndPoint.X, xMin);
            xMax = Math.Max(BaseEntity.StartPoint.X, xMax);
            xMax = Math.Max(BaseEntity.EndPoint.X, xMax);
            yMin = Math.Min(BaseEntity.StartPoint.Y, yMin);
            yMin = Math.Min(BaseEntity.EndPoint.Y, yMin);
            yMax = Math.Max(BaseEntity.StartPoint.Y, yMax);
            yMax = Math.Max(BaseEntity.EndPoint.Y, yMax);
            zMin = Math.Min(BaseEntity.StartPoint.Z, zMin);
            zMin = Math.Min(BaseEntity.EndPoint.Z, zMin);
            zMax = Math.Max(BaseEntity.StartPoint.Z, zMax);
            zMax = Math.Max(BaseEntity.EndPoint.Z, zMax);

            if (BaseEntity is IArc)
            {

                xMin = Math.Min((BaseEntity as IArc).ViaPoint.X, xMin);
                xMax = Math.Max((BaseEntity as IArc).ViaPoint.X, xMax);
                yMin = Math.Min((BaseEntity as IArc).ViaPoint.Y, yMin);
                yMax = Math.Max((BaseEntity as IArc).ViaPoint.Y, yMax);
                zMin = Math.Min((BaseEntity as IArc).ViaPoint.Z, zMin);
                zMax = Math.Max((BaseEntity as IArc).ViaPoint.Z, zMax);
            }
        }
    }
}
