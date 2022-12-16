using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace ParserLib.Helpers
{
    internal class MathHelpers
    {
        ///<summary> Rotates the first vector around the pivot vector by alpha RADIANTS, Returns the rotated vector </summary> 
        public static Vector3D Rotate_Rodriguez(Vector3D vectorToRotate, Vector3D pivotVector, double alpha)
        {
            Vector3D rotatedVector = vectorToRotate * Math.Cos(alpha) + Vector3D.CrossProduct(pivotVector, vectorToRotate) * Math.Sin(alpha) + pivotVector * (Vector3D.DotProduct(pivotVector, vectorToRotate) * (1 - Math.Cos(alpha)));
            return rotatedVector;
        }

        ///<summary> Returns TRUE if the angle between the 2 input NORMALIZED vectors is greater than 180deg </summary> 
        public static Boolean IsLargeAngle(Vector3D v1,Vector3D v2, Vector3D moveNormal)
        {
            var cross = Vector3D.CrossProduct(v1, v2);
            var dot = Vector3D.DotProduct(v1, v2);
            var angle = Math.Atan2(cross.Length, dot);
            var test = Vector3D.DotProduct(moveNormal, cross);
            if  (test < 0.0) return true; else return false;

        }
      


    }
}
