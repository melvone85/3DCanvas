using ParserLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace ParserLib.Entities.Helpers
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
            //se l'angolo tra OB e OA oppure l'angolo tra OB e OC è maggiore di angolo tra OA e OC allora Ã¨ un large arc
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

            //move.RedrawArc();
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
