using ParserLib.Interfaces;
using ParserLib.Models;
using ParserLib.Services.Parsers.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;

namespace ParserLib.Services.Parsers
{
    public class ParseIso : IParser
    {
        public string Filename { get; set; }
        private Dictionary<string, float> _variables = null;
        private Regex Gcode;

        public ParseIso(string fileName)
        {
            Filename = fileName;
            string WordRegexPattern = @"[A-Z]?[+-]?[0-9]*\.*\d+|\b([A-Z][^A-Z]*)\b|\b([A-Z][A-Z]?[\=]?[A-Z][0-9]*)\b";
            //string WordRegexPattern = @"[A-Za-z]+[\=]?[A-Za-z]*[+-]?\d*\.?\d*([+-]?[*:]?\d+\.?\d*)+";
            Gcode = new Regex(WordRegexPattern, RegexOptions.IgnoreCase);
        }

        public IEntity ParseLine(string line)
        {
            if (line.StartsWith("G"))
            {
                if (line.Contains("G91"))
                { }

                IEntity move;

                MatchCollection m = Gcode.Matches(line);
                var code = m[0].ToString();

                if (m.Count > 1)
                {
                    var gCodeNumber = int.Parse(code.Substring(1));

                    switch (gCodeNumber)
                    {
                        case 0:
                        case 1:
                            move = new LinearMove
                            {
                                EntityType = Globals.Globals.EEntityType.Line
                            };
                            BuildMove(ref move, m);
                            break;

                        case 2:
                        case 3:
                            move = new ArcMove
                            {
                                EntityType = Globals.Globals.EEntityType.Arc
                            };
                            BuildMove(ref move, m);

                            break;

                        case 102:
                        case 103:
                            move = new ArcMove
                            {
                                EntityType = Globals.Globals.EEntityType.Arc
                            };
                            BuildMove(ref move, m);

                            break;

                        case 104:
                            move = new ArcMove
                            {
                                EntityType = Globals.Globals.EEntityType.Arc
                            };
                            BuildMove(ref move, m);

                            break;

                        default:
                            move = null;
                            break;
                    }

                    if (move != null)
                        move.OriginalLine = line;
                    return move;
                }
            }

            return null;
        }

        private HashSet<string> LineToSkip
        {
            get
            {
                return new HashSet<string>()
                {
                    "G08",
                    "G40",
                    "G71"
                };
            }
        }


        public List<IEntity> GetMoves()
        {
            Dictionary<int, List<string>> dicOperationsInLabel = new Dictionary<int, List<string>>();
            var d = new System.Diagnostics.Stopwatch();
            d.Start();
            List<IEntity> moves = new List<IEntity>();
            List<string> lstMoves = new List<string>();
            var lineNumber = -1;
            bool jumpingIntoLabel = false;
            bool foundedLabelTojump = false;
            bool readingLabel = false;
            List<string> lstLabelOperations = new List<string>();
            var searchingForLabel = -1;
            try
            {
                using (var fs = new FileStream(Filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var sr = new StreamReader(fs))
                    {
                        var lineToClean = sr.ReadLine().ToUpper().Trim();
                        try
                        {
                            while (lineToClean != null)
                            {
                                lineNumber += 1;

                                if (lineToClean.StartsWith(";") || lineToClean.StartsWith("(*") || lineToClean.StartsWith("//") || string.IsNullOrEmpty(lineToClean) || string.IsNullOrWhiteSpace(lineToClean))
                                {
                                    lineToClean = sr.ReadLine();
                                    continue;
                                }

                                if (lineToClean.StartsWith("G08") || lineToClean.StartsWith("G09") || lineToClean.StartsWith("G71") || lineToClean.StartsWith("F") ||
                                    lineToClean.StartsWith("G40") || lineToClean.StartsWith("G41") || lineToClean.StartsWith("G42") || lineToClean.StartsWith("<MATERIAL"))
                                {
                                    lineToClean = sr.ReadLine();
                                    continue;
                                }

                                if (lineToClean.StartsWith("$") == false
                                    || lineToClean.StartsWith("$(WORK_ON") || lineToClean.StartsWith("$(WORK_OFF") || lineToClean.StartsWith("$(BEAM_ON")
                                    || lineToClean.StartsWith("$(BEAM_OFF") || lineToClean.StartsWith("$(HOLE") || lineToClean.StartsWith("$(SLOT") || lineToClean.StartsWith("$(RECT")
                                    || lineToClean.StartsWith("$(CIRCLE") || lineToClean.StartsWith("$(KEYHOLE") || lineToClean.StartsWith("$(MARKING") || lineToClean.StartsWith("$(END_MARKING")
                                    || lineToClean.StartsWith("$(POLY") || lineToClean.StartsWith("$(SQUARE"))
                                {

                                    if (lineToClean.StartsWith("GO"))
                                    {
                                        //Reset label founded
                                        foundedLabelTojump = false;
                                        //Is not reading label but if it was reset to it
                                        readingLabel = false;
                                        //Need to search the label xx in the GOxx
                                        jumpingIntoLabel = true;


                                        lineToClean = lineToClean.Replace(" ", "");

                                        //Take just the integer part
                                        searchingForLabel = int.Parse(Regex.Match(lineToClean, @"\d+").Value);

                                        //If the label operations are already present in the dictionary then print it. Better to not enter here
                                        //Because I have to set that the file reading index is at the end of this foreach
                                        if (dicOperationsInLabel.ContainsKey(searchingForLabel))
                                        {
                                            var lst = dicOperationsInLabel[searchingForLabel];
                                            foreach (var item in lst)
                                            {
                                                lstMoves.Add(lineToClean);
                                            }
                                        }

                                        //Get the next line
                                        lineToClean = sr.ReadLine();
                                        continue;
                                    }

                                    if (lineToClean.StartsWith("N") && lineToClean.Contains("P200=") == false)
                                    {
                                        readingLabel = true;
                                        foundedLabelTojump = false;
                                        lineToClean = lineToClean.Replace(" ", "");

                                        var labelFounded = int.Parse(Regex.Match(lineToClean, @"\d+").Value);

                                        //If is searching for a label from GOXX and this is the label searched then
                                        //Stop searching.
                                        if (searchingForLabel == int.Parse(Regex.Match(lineToClean, @"\d+").Value))
                                        {
                                            foundedLabelTojump = true;
                                            jumpingIntoLabel = false;
                                        }

                                        if (lstLabelOperations.Count > 0)
                                            lstLabelOperations = new List<string>();

                                        dicOperationsInLabel.Add(labelFounded, lstLabelOperations);

                                        lineToClean = sr.ReadLine();

                                        continue;
                                    }

                                    if (jumpingIntoLabel)
                                    {
                                        lineToClean = sr.ReadLine();
                                        continue;
                                    }

                                    if (readingLabel)
                                    {
                                        lstLabelOperations.Add(lineToClean);
                                    }

                                    if (lineToClean.StartsWith("G") && (lineToClean.Contains("LV") || lineToClean.Contains("P")))
                                    {
                                    }



                                    lstMoves.Add(lineToClean);
                                }
                                lineToClean = sr.ReadLine();

                            }
                        }
                        catch (Exception ex)
                        {
                            //logger.Error("Error parsing part-program header when reading line: " + line + " - line-number: " + lineNumber + " - in file " + fileName + " " + ex.Message + ex.StackTrace);
                        }
                        sr.Close();
                    }
                }

            }
            catch (Exception ex)
            {
                //logger.Error("Error parsing part-program header when reading line: " + "in file " + fileName + ex.Message + ex.StackTrace);
            }


            //    using (StreamReader fs = new StreamReader(Filename))
            //{
            int lineCounter = 0;
            var isBeamOn = false;
            Globals.Globals.ELineType color = Globals.Globals.ELineType.Rapid;
            double xi = 0;
            double yi = 0;
            double zi = 0;
            bool isIncrementalMove = false;
            _variables = new Dictionary<string, float>();
            IEntity referenceMove = null;
            //var isSkipping = false;
            var nToFind = "";
            bool isMarkingPiece=false;
            foreach (var line in lstMoves)
            {
                lineCounter++;

                if (line.StartsWith("P") && line.Contains("="))
                {
                }
                if (line.StartsWith("LV") && line.Contains("="))
                {
                }
                if (line.StartsWith("GO"))
                {
                    nToFind = line.Replace("GO", "N");
                    continue;
                }

                if (line.StartsWith("$(WORK_ON") || line.StartsWith("$(BEAM_ON"))
                {
                    isBeamOn = true;

                    var lineType = int.Parse(line.Replace("$(WORK_ON)", string.Empty).Replace("(", "").Trim().Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries)[0]);
                    color = (Globals.Globals.ELineType)lineType;
                }
                else if (line.StartsWith("$(WORK_OFF") || line.StartsWith("$(BEAM_OFF"))
                {
                    color = Globals.Globals.ELineType.Rapid;
                    isBeamOn = false;
                }
                else if (line.StartsWith("$(MARKING_PIECE)"))
                {
                    isMarkingPiece = true;
                }
                else if (line.StartsWith("$(MARKING)"))
                {
                    color = Globals.Globals.ELineType.Marking;
                    isBeamOn = true;
                }
                else if (line.StartsWith("$(END_MARKING"))
                {
                    color = Globals.Globals.ELineType.Rapid;
                    isBeamOn = false;
                }
                else if (line.StartsWith("G91"))
                {
                    isIncrementalMove = true;
                }
                else if (line.StartsWith("G91"))
                {
                    isIncrementalMove = false;
                }
                else if (line.StartsWith("G92") || line.StartsWith("G93") || line.StartsWith("G113") || line.StartsWith("G114"))
                {
                    referenceMove = new LinearMove();
                    MatchCollection m = Gcode.Matches(line);
                    BuildMove(ref referenceMove, m);
                    //referenceMove.EndPoint = new Point3D(0, 0, 0);

                    continue;
                }
                else if (line.StartsWith("G92"))
                {
                    referenceMove = new LinearMove();
                    referenceMove.EndPoint = new Point3D(0, 0, 0);
                }

                if (moves.Count > 0)
                {
                    var prevMove = moves[moves.Count - 1];
                    xi = prevMove.EndPoint.X;
                    yi = prevMove.EndPoint.Y;
                    zi = prevMove.EndPoint.Z;
                }

                if (referenceMove == null)
                {
                    referenceMove = new LinearMove();
                    referenceMove.EndPoint = new Point3D(0, 0, 0);
                }

                if (line.StartsWith("$(HOLE)"))
                {
                    continue;
                    var normLine = line.Replace(" ", "");
                    var pars = normLine.Replace("$(HOLE)(", "").Replace(")", "").Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                    Point3D C = new Point3D(referenceMove.EndPoint.X + double.Parse(pars[0]), referenceMove.EndPoint.Y + double.Parse(pars[1]), referenceMove.EndPoint.Z + double.Parse(pars[2]));
                    Point3D N = new Point3D(referenceMove.EndPoint.X + double.Parse(pars[3]), referenceMove.EndPoint.Y + double.Parse(pars[4]), referenceMove.EndPoint.Z + double.Parse(pars[5]));
                    var r = double.Parse(pars[6]);
                   
                    var am = GetMoveFromHole(C, N, r);
                    
                    am.SourceLine = lineCounter;
                    am.IsBeamOn = isBeamOn;
                    am.LineColor = isMarkingPiece? Globals.Globals.ELineType.Marking:color;
                    am.OriginalLine = line;
                    moves.Add(am);

                   

                }
                else
                {
                    var move = ParseLine(line);
                    if (move == null) continue;

                    move.StartPoint = new Point3D(xi, yi, zi);
                    move.EndPoint = new Point3D(referenceMove.EndPoint.X + move.EndPoint.X, referenceMove.EndPoint.Y + move.EndPoint.Y, referenceMove.EndPoint.Z + move.EndPoint.Z); ;

                    if (move is ArcMove)
                    {
                        var circleMove = (ArcMove)move;
                        var vp = new Point3D(referenceMove.EndPoint.X + circleMove.ViaPoint.X, referenceMove.EndPoint.Y + circleMove.ViaPoint.Y, referenceMove.EndPoint.Z + circleMove.ViaPoint.Z);
                        circleMove.ViaPoint = vp;

                        AddCircularMoveProperties(ref circleMove);
                    }

                    move.SourceLine = lineCounter;
                    move.IsBeamOn = isBeamOn;
                    move.LineColor = isMarkingPiece ? isBeamOn==false?Globals.Globals.ELineType.Rapid: Globals.Globals.ELineType.Marking : color;

                    moves.Add(move);
                }
            }
 

            d.Stop();

            Console.WriteLine($"Time to parse Iso: ms {d.ElapsedMilliseconds}");
            return moves;
        }

        public List<IEntity> ParseMacro(string macro)
        {
            return new List<IEntity>();
        }

        private ArcMove GetMoveFromHole(Point3D centerPoint, Point3D normalPoint, double radius)
        {
            //const double alpha = -0.0001745329;
            //Se da fastidio questa tolleranza bisogna introdurre L'elemento ellipse geometry che fà un cerchio

            double maxClosingGap = 0.01; //mm
            double alpha = -maxClosingGap/radius;


            var normalVector = Point3D.Subtract(centerPoint, normalPoint);
            normalVector.Normalize();

            var vectorUp = (centerPoint.X == normalPoint.X && centerPoint.Y == normalPoint.Y) ? new Vector3D(1, 0, 0) : new Vector3D(0, 0, 1);

            var versor = Vector3D.CrossProduct(normalVector, vectorUp);
            versor.Normalize();





            var vr = Vector3D.Multiply(versor, radius);
            var rotatedVector = vr * Math.Cos(alpha) + Vector3D.CrossProduct(normalVector, vr) * Math.Sin(alpha) + normalVector * (Vector3D.DotProduct(normalVector, vr) * (1 - Math.Cos(alpha)));

            var startPoint = Vector3D.Add(vr, centerPoint);
            var viaPoint = Vector3D.Add(-vr, centerPoint);
            var endPoint = Vector3D.Add(rotatedVector, centerPoint);

            var am = new ArcMove() {  StartPoint = startPoint, ViaPoint = viaPoint, EndPoint = endPoint };
            am.IsStroked = true;
            am.IsLargeArc = true;
          
            am.Radius = radius;
            am.Normal = normalVector;
            am.CenterPoint = centerPoint;
            am.EntityType = Globals.Globals.EEntityType.Arc;




            return am;
        }


        private IEntity AddCircularMoveProperties(ref ArcMove move)
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

            /// TACCONE DA MIGLIORARE
            if (double.IsInfinity(r))
            {
                move.Radius = 1000;
            }

            //Circumcenter
            double b1 = CB * CB * (CA * CA + AB * AB - CB * CB);
            double b2 = CA * CA * (CB * CB + AB * AB - CA * CA);
            double b3 = AB * AB * (CB * CB + CA * CA - AB * AB);

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

            move.IsStroked = true;
            move.IsLargeArc = false;


            if (Vector3D.AngleBetween(vMed, vIni) > Vector3D.AngleBetween(vIni, vEnd) || Vector3D.AngleBetween(vMed, vEnd) > Vector3D.AngleBetween(vIni, vEnd))
            {
                move.IsLargeArc = true;

            }

            move.Normal = Vector3D.CrossProduct(Point3D.Subtract(A, B), Point3D.Subtract(A, C));

            move.CenterPoint = centerPoint;



            return move;
        }

        private void BuildMove(ref IEntity move, MatchCollection matches)
        {
            var endPoint = new Point3D(0, 0, 0);
            var viaPoint = new Point3D(0, 0, 0);

            for (int i = 1; i < matches.Count; i++)
            {
                var ax = matches[i].ToString();

                var axName = ax[0];
                var axValue = ax.Substring(1).Replace("=", "");

                switch (axName)
                {
                    case 'X': endPoint.X = GetQuotaValue(axValue.Trim()); break;
                    case 'Y': endPoint.Y = GetQuotaValue(axValue.Trim()); break;
                    case 'Z': endPoint.Z = GetQuotaValue(axValue.Trim()); break;

                    //case 'A': move.Xu = float.Parse(axValue); break;
                    //case 'B': move.Xu = float.Parse(axValue); break;
                    //case 'C': move.Xu = float.Parse(axValue); break;

                    case 'I': viaPoint.X = GetQuotaValue(axValue.Trim()); break;
                    case 'J': viaPoint.Y = GetQuotaValue(axValue.Trim()); break;
                    case 'K': viaPoint.Z = GetQuotaValue(axValue.Trim()); break;

                    default:
                        break;
                }
            }

            move.EndPoint = endPoint;

            if (move.EntityType == Globals.Globals.EEntityType.Arc || move.EntityType == Globals.Globals.EEntityType.Circle)
            {
                (move as ArcMove).ViaPoint = viaPoint;
            }
        }

        private static double GetQuotaValue(string axValue)
        {
            if (double.TryParse(axValue, out var quota))
            {
                return quota;
            }
            else 
            {
                if (axValue.Contains("#")) 
                { 
                               var  searchingForLabel = int.Parse(Regex.Match(axValue, @"\d+\.+\d+").Value);

                }


            }

            bool result = axValue.Any(x => !char.IsLetter(x));

            if (result)
            {
                string formula = axValue; //or get it from DB
                StringToFormula stf = new StringToFormula();
                return stf.Eval(formula);
            }
            else
            {
                //P or LV
            }

            return double.Parse(axValue);
        }
    }

    public class StringToFormula
    {
        private string[] _operators = { "-", "+", "/", "*", "^" };

        private Func<double, double, double>[] _operations = {
        (a1, a2) => a1 - a2,
        (a1, a2) => a1 + a2,
        (a1, a2) => a1 / a2,
        (a1, a2) => a1 * a2,
        (a1, a2) => Math.Pow(a1, a2)
    };

        public double Eval(string expression)
        {
            List<string> tokens = getTokens(expression);
            Stack<double> operandStack = new Stack<double>();
            Stack<string> operatorStack = new Stack<string>();
            int tokenIndex = 0;

            while (tokenIndex < tokens.Count)
            {
                string token = tokens[tokenIndex];
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

        private string getSubExpression(List<string> tokens, ref int index)
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

        private List<string> getTokens(string expression)
        {
            string operators = "()^*/+-";
            List<string> tokens = new List<string>();
            StringBuilder sb = new StringBuilder();

            foreach (char c in expression.Replace(" ", string.Empty))
            {
                if (operators.IndexOf(c) >= 0)
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