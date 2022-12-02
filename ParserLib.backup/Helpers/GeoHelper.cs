﻿using ParserLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace ParserLib.Helpers
{
    public static class GeoHelper
    {
        public static void AddCircularMoveProperties(ref ArcMove move)
        {
            var A = move.StartPoint;
            var B = move.ViaPoint;
            var C = move.EndPoint;

            //segments
            double CB = Point3D.Subtract(C, B).Length;
            double CA = Point3D.Subtract(C, A).Length;
            double AB = Point3D.Subtract(A, B).Length;

            //circumradius
            double s = (CB + CA + AB) / 2;
            double r = CB * CA * AB / 4 / Math.Sqrt(s * (s - CB) * (s - CA) * (s - AB));

            move.Radius = r;
            if (double.IsInfinity(r))
                move.Radius = 1000;

            //Circumcenter
            double b1 = GetCircumPoint(CB, CA, AB);
            double b2 = GetCircumPoint(CA, CB, AB);
            double b3 = GetCircumPoint(AB, CB, CA);

            Vector3D v = new Vector3D(b1, b2, b3);
            Vector3D p1 = new Vector3D(A.X, B.X, C.X);
            Vector3D p2 = new Vector3D(A.Y, B.Y, C.Y);
            Vector3D p3 = new Vector3D(A.Z, B.Z, C.Z);
            Point3D centerPoint = new Point3D(Vector3D.DotProduct(v, p1) / (b1 + b2 + b3), Vector3D.DotProduct(v, p2) / (b1 + b2 + b3), Vector3D.DotProduct(v, p3) / (b1 + b2 + b3));

            //Verifico se è un large arc:
            //se l'angolo tra OB e OA oppure l'angolo tra OB e OC è maggiore di angolo tra OA e OC allora è un large arc
            Vector3D vIni = Point3D.Subtract(centerPoint, A); //vettore OA
            Vector3D vMed = Point3D.Subtract(centerPoint, B); //vettore OB
            Vector3D vEnd = Point3D.Subtract(centerPoint, C); //vettore OC
            move.CenterPoint = centerPoint;
            move.IsStroked = true;
            move.IsLargeArc = false;

            if (Vector3D.AngleBetween(vMed, vIni) > Vector3D.AngleBetween(vIni, vEnd) || Vector3D.AngleBetween(vMed, vEnd) > Vector3D.AngleBetween(vIni, vEnd))
            {
                move.IsLargeArc = true;
            }

            move.Normal = Vector3D.CrossProduct(Point3D.Subtract(A, B), Point3D.Subtract(A, C));
        }

        private static double GetCircumPoint(double q1, double q2, double q3)
        {
            return q1 * q1 * (q2 * q2 + q3 * q3 - q1 * q1);
        }

        public static void GetMoveFromMacroHole(ref ArcMove hole)
        {
            //const double alpha = -0.0001745329;
            //Se da fastidio questa tolleranza bisogna introdurre L'elemento ellipse geometry che fà un cerchio

            double maxClosingGap = 0.01; //mm
            double alpha = -maxClosingGap / hole.Radius;

            //Se si vedessero comportamenti strani, verificare che la direzione della normale segua la regola della mano sinistra rispetto al verso di percorrenza dell'arco.

            var normalVector = Point3D.Subtract(hole.CenterPoint, hole.NormalPoint);
            normalVector.Normalize();

            var vectorUp = (hole.CenterPoint.X == hole.NormalPoint.X && hole.CenterPoint.Y == hole.NormalPoint.Y) ? new Vector3D(1, 0, 0) : new Vector3D(0, 0, 1);

            var versor = Vector3D.CrossProduct(normalVector, vectorUp);
            versor.Normalize();

            var vr = Vector3D.Multiply(versor, hole.Radius);
            var rotatedVector = vr * Math.Cos(alpha) + Vector3D.CrossProduct(normalVector, vr) * Math.Sin(alpha) + normalVector * (Vector3D.DotProduct(normalVector, vr) * (1 - Math.Cos(alpha)));

            hole.StartPoint = Vector3D.Add(vr, hole.CenterPoint);
            hole.ViaPoint = Vector3D.Add(-vr, hole.CenterPoint);
            hole.EndPoint = Vector3D.Add(rotatedVector, hole.CenterPoint);

            hole.Normal = normalVector;
        }

        //def buildSlot(C1, C2, N, r:int, resolution:int= 5) :
        //#converet to numpy if necessary 
        //C1,C2,N = Curves.convertToNpArray(C1, C2, N)
        //if Curves.areCoincident([C1, C2, N]):
        //    raise ValueError('All the 3 points must be different to each other!')
        //#n normal 
        //n = N-C2
        //nVersor = n / np.linalg.norm(n)
        //C1C2 = C1-C2
        //C1C2Versor = C1C2 / np.linalg.norm(C1C2)
        //p1 = C1C2Versor* r
        //p2 = C1C2Versor* -r
        //deg = int(90 / resolution)
        //rad = deg* np.pi/180
        //points =[]
        //try :
        //    for step in range(-resolution, resolution+1):
        //        theta = rad* step
        //        rotated = p1* np.cos(theta) + np.cross(nVersor, p1)* np.sin(theta)+  nVersor* (np.dot(nVersor, p1))*(1-np.cos(theta))
        //        rotated += C1
        //        points.append(rotated)
        //    for step in range(-resolution, resolution+1) :
        //        theta = rad* step
        //        rotated = p2* np.cos(theta) + np.cross(nVersor, p2)* np.sin(theta)+  nVersor* (np.dot(nVersor, p2))*(1-np.cos(theta))
        //        rotated += C2
        //        points.append(rotated)
        //    points.append(points[0])
        //except TypeError as e:
        //    print(e)
        //return points

        public static void GetMovesFromMacroSlot(ref SlotMove slot)
        {
            var c1C2Vector = Point3D.Subtract(slot.Arc1.CenterPoint, slot.Arc2.CenterPoint);
            
            var c2C1Vector = Point3D.Subtract(slot.Arc2.CenterPoint, slot.Arc1.CenterPoint);
            c2C1Vector.Normalize();

            slot.Arc1.NormalPoint = Point3D.Add(slot.Arc2.NormalPoint, c1C2Vector); ;
            c1C2Vector.Normalize();
   

            var normalVectorC1 = Point3D.Subtract(slot.Arc1.CenterPoint, slot.Arc1.NormalPoint);
            normalVectorC1.Normalize();

            var normalVectorC2 = Point3D.Subtract(slot.Arc2.CenterPoint, slot.Arc2.NormalPoint);
            normalVectorC2.Normalize();



            var cAVersor = Vector3D.CrossProduct(normalVectorC1, c2C1Vector);
            cAVersor.Normalize();

            var cAVector = cAVersor * slot.Arc1.Radius;
            var arc1StartPoint = slot.Arc1.CenterPoint + cAVector;

            var cBVector = cAVersor * -slot.Arc1.Radius;
            var arc1EndPoint = slot.Arc1.CenterPoint + cBVector;

            var arc1ViaPoint =Point3D.Add(slot.Arc1.CenterPoint, c2C1Vector * -slot.Arc1.Radius);

            var cCVersor = Vector3D.CrossProduct(normalVectorC2, c1C2Vector);
            cCVersor.Normalize();

            var cCVector = cCVersor * slot.Arc2.Radius;
            var arc2StartPoint = slot.Arc2.CenterPoint + cCVector;

            var cDVector = cCVersor * -slot.Arc2.Radius;
            var arc2EndPoint = slot.Arc2.CenterPoint + cDVector;

            var arc2ViaPoint = Point3D.Add(slot.Arc2.CenterPoint, c1C2Vector * -slot.Arc2.Radius);

            slot.Arc1.StartPoint = arc1StartPoint;
            slot.Arc1.EndPoint = arc1EndPoint;
            slot.Arc1.ViaPoint = arc1ViaPoint;
            slot.Arc1.Normal = normalVectorC1;

            slot.Arc2.StartPoint = arc2StartPoint;
            slot.Arc2.EndPoint = arc2EndPoint;
            slot.Arc2.ViaPoint = arc2ViaPoint;
            slot.Arc2.Normal = normalVectorC2;

            slot.Line1.StartPoint = slot.Arc1.EndPoint;
            slot.Line1.EndPoint = slot.Arc2.StartPoint;

            slot.Line2.StartPoint = slot.Arc2.EndPoint;
            slot.Line2.EndPoint = slot.Arc1.StartPoint;
        }

        public static void GetMovesFromMacroPoly(ref PolyMoves poly)
        {
            //calcolo di quanti gradi devo ruotare il primo segmento
            double alpha = 2*Math.PI / poly.Sides;

            //la normale della poly è presa rispetto al vertice, 

            //vettore dal centro al vertice 
            var vertexCenterVector = Point3D.Subtract(poly.CenterPoint, poly.VertexPoint);
            //centerVertexVector.Normalize();
            //vettore dal vertice al centro 
            var centerVertexVector = Point3D.Subtract(poly.VertexPoint, poly.CenterPoint);
            //vertexCenterVector.Normalize();
            //punto sulla nomrale partendo dal centro, sposto il punto normale parsificato sul centro della poly
            var normalCenterPoint = Point3D.Add(poly.NormalPoint, vertexCenterVector);
            //vettore dal centro alla nnuova normale
            var normalVector = Point3D.Subtract(normalCenterPoint, poly.CenterPoint);
            normalVector.Normalize();

            // calcolo i nuovi vettori

            var vertices = new Point3D[poly.Sides+1];

            var angle = 0.0;
            for (int i = 0; i < poly.Sides; i++)
            {
                angle=alpha * i;
                var rotatedVector = centerVertexVector * Math.Cos(angle) + Vector3D.CrossProduct(normalVector, centerVertexVector) * Math.Sin(angle) + normalVector * (Vector3D.DotProduct(normalVector, centerVertexVector) * (1 - Math.Cos(angle)));
                var newVertex = Point3D.Add(poly.CenterPoint, rotatedVector);
                vertices[i] = newVertex;
            }
            // aggiungo in coda ai vertici il primo vertice
            vertices[poly.Sides] = poly.VertexPoint; // puoi controllare che stia funzionando bene verificando che  poly.Vertices[0] == poly.VertexPoint;

            poly.Lines = new List<Interfaces.ILine>();
            

            for (int i = 0; i < poly.Sides; i++)
            {
                poly.Lines.Add(
                    new LinearMove() {
                        
                        StartPoint = vertices[i], 
                        EndPoint = vertices[i + 1],
                        SourceLine = poly.SourceLine,
                        IsBeamOn = poly.IsBeamOn,
                        LineColor = poly.LineColor,
                        OriginalLine = poly.OriginalLine,
                    }); 
            }
        }

        public static class StringToFormula
        {
            private static string[] _operators = { "-", "+", ":", "*", "^" };

            private static Func<double, double, double>[] _operations = {
        (a1, a2) => a1 - a2,
        (a1, a2) => a1 + a2,
        (a1, a2) => a1 / a2,
        (a1, a2) => a1 * a2,
        (a1, a2) => Math.Pow(a1, a2)
    };

            public static double Eval(string expression)
            {
                List<string> tokens = getTokens(expression);
                Stack<double> operandStack = new Stack<double>();
                Stack<string> operatorStack = new Stack<string>();
                int tokenIndex = 0;

                if (expression.Contains("MVX")) expression = expression.Replace("MVX", "0.0");
                if (expression.Contains("MVY")) expression = expression.Replace("MVY", "0.0");
                if (expression.Contains("MVZ")) expression = expression.Replace("MVZ", "0.0");

                while (tokenIndex < tokens.Count)
                {
                    string token = tokens[tokenIndex];

                    if (token.Equals("MVX")) token = token.Replace("MVX", "0.0");
                    else if (token.Equals("MVY")) token = token.Replace("MVY", "0.0");
                    else if (token.Equals("MVZ")) token = token.Replace("MVZ", "0.0");

                    if (token == "(")
                    {
                        string subExpr = getSubExpression(tokens, ref tokenIndex);
                        operandStack.Push(Eval(subExpr));
                        continue;
                    }
                    if (token == ")")
                    {
                        throw new ArgumentException("Mis-matched parentheses in expression");
                    }
                    //If this is an operator
                    if (Array.IndexOf(_operators, token) >= 0)
                    {
                        while (operatorStack.Count > 0 && Array.IndexOf(_operators, token) < Array.IndexOf(_operators, operatorStack.Peek()))
                        {
                            string op = operatorStack.Pop();
                            double arg2 = operandStack.Pop();
                            double arg1 = operandStack.Pop();
                            operandStack.Push(_operations[Array.IndexOf(_operators, op)](arg1, arg2));
                        }
                        operatorStack.Push(token);
                    }
                    else
                    {
                        operandStack.Push(double.Parse(token));
                    }
                    tokenIndex += 1;
                }

                while (operatorStack.Count > 0)
                {
                    string op = operatorStack.Pop();
                    double arg2 = operandStack.Pop();
                    double arg1 = operandStack.Pop();
                    operandStack.Push(_operations[Array.IndexOf(_operators, op)](arg1, arg2));
                }
                return operandStack.Pop();
            }

            private static string getSubExpression(List<string> tokens, ref int index)
            {
                StringBuilder subExpr = new StringBuilder();
                int parenlevels = 1;
                index += 1;
                while (index < tokens.Count && parenlevels > 0)
                {
                    string token = tokens[index];
                    if (tokens[index] == "(")
                    {
                        parenlevels += 1;
                    }

                    if (tokens[index] == ")")
                    {
                        parenlevels -= 1;
                    }

                    if (parenlevels > 0)
                    {
                        subExpr.Append(token);
                    }

                    index += 1;
                }

                if ((parenlevels > 0))
                {
                    throw new ArgumentException("Mis-matched parentheses in expression");
                }
                return subExpr.ToString();
            }

            private static List<string> getTokens(string expression)
            {
                string operators = "()^*:+-";
                List<string> tokens = new List<string>();
                StringBuilder sb = new StringBuilder();
                int counter = -1;
                foreach (char c in expression.Replace(" ", string.Empty))
                {
                    counter++;
                    if (operators.IndexOf(c) >= 0 && counter > 0)
                    {
                        if ((sb.Length > 0))
                        {
                            tokens.Add(sb.ToString());
                            sb.Length = 0;
                        }
                        tokens.Add(c.ToString());
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                if ((sb.Length > 0))
                {
                    tokens.Add(sb.ToString());
                }
                return tokens;
            }
        }


    }
}