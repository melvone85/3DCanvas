using ParserLib.Entities.Helpers;
using ParserLib.Interfaces;
using ParserLib.Models;
using ParserLib.Services.Parsers.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using static ParserLib.Entities.Helpers.GeoHelper;
using static ParserLib.Entities.Helpers.TechnoHelper;

namespace ParserLib.Services.Parsers
{
    public class ParseIso : IParser
    {
        public string Filename { get; set; }
        private Regex GcodeAxesQuotaRegex;
        private Regex VariableDeclarationRegex;
        private Regex MacroParsRegex;
        private double _conversionValue = 1;
        Stopwatch wat = new Stopwatch();
        public ParseIso(string fileName)
        {

            Filename = fileName;
            //string WordRegexPattern = @"[A-Z]?[+-]?[0-9]*\.*\d+|\b([A-Z][^A-Z]*)\b|\b([A-Z][A-Z]?[\=]?[A-Z][0-9]*)\b";
            string GcodeAxesQuotaPattern = @"\b[A-Za-z]+[\=]?[A-Za-z]*[+-]?\d*\.?\d*([+-]?[*:]?[A-Za-z]*[+-]?\d*\.?\d*)+\b";
            GcodeAxesQuotaRegex = new Regex(GcodeAxesQuotaPattern, RegexOptions.IgnoreCase);
            string VariableDeclarationPattern = @"\b[A-Za-z]+[\d]+([\=]?[A-Za-z]*[+-]?[*:]?\d*\.?\d*)+\b";
            VariableDeclarationRegex = new Regex(VariableDeclarationPattern, RegexOptions.IgnoreCase);
            string macroParsPattern = @"([+-]?[*:]?\d+\.?\d*)+";
            MacroParsRegex = new Regex(macroParsPattern, RegexOptions.IgnoreCase);

        }

        public List<string> WriteSafeReadAllLines(string path)
        {
            using (FileStream csv = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader sr = new StreamReader(csv))
                {
                    List<string> file = new List<string>();
                    while (!sr.EndOfStream)
                        file.Add(sr.ReadLine().ToUpper().Trim());

                    return file;
                }
            }
        }

        Dictionary<string, List<Tuple<int, string>>> _dicSubprograms;
        public List<IEntity> GetMoves()
        {
            var d = new System.Diagnostics.Stopwatch();
            d.Start();


            var lines = WriteSafeReadAllLines(Filename);


            _dicSubprograms = new Dictionary<string, List<Tuple<int, string>>>();
            Dictionary<string, string> dicVariables = new Dictionary<string, string>();
            Dictionary<int, List<string>> dicOperationsInLabel = new Dictionary<int, List<string>>();
            List<IEntity> moves = new List<IEntity>();
            List<Tuple<int, string>> lstMoves = new List<Tuple<int, string>>();
            List<string> lstLabelOperations = new List<string>();

            var lineNumber = 0;
            bool jumpingIntoLabel = false;
            bool readingLabel = false;
            var searchingForLabel = -1;


            foreach (var lineToClean in lines)
            {
                try
                {

                    lineNumber += 1;

                    if (lineToClean.StartsWith(";") || lineToClean.StartsWith("(*") || lineToClean.StartsWith("//") || string.IsNullOrEmpty(lineToClean) || string.IsNullOrWhiteSpace(lineToClean))
                    {
                        continue;
                    }
                    if (lineToClean.StartsWith("<MATERIAL")) { continue; }

                    if (lineToClean.StartsWith("G08") || lineToClean.StartsWith("G09") || lineToClean.StartsWith("F") || lineToClean.StartsWith("EI") ||
                        lineToClean.StartsWith("G40") || lineToClean.StartsWith("GO*") || lineToClean.StartsWith("G41") || lineToClean.StartsWith("G42") || lineToClean.StartsWith("<MATERIAL"))
                    {
                        continue;
                    }

                    if (lineToClean.StartsWith("$") == false
                        || lineToClean.StartsWith("$(WORK_ON") || lineToClean.StartsWith("$(WORK_OFF") || lineToClean.StartsWith("$(WORK_TYPE)") || lineToClean.StartsWith("$(BEAM_ON")
                        || lineToClean.StartsWith("$(BEAM_OFF") || lineToClean.StartsWith("$(HOLE") || lineToClean.StartsWith("$(SLOT") || lineToClean.StartsWith("$(RECT")
                        || lineToClean.StartsWith("$(CIRCLE") || lineToClean.StartsWith("$(KEYHOLE") || lineToClean.StartsWith("$(MARKING") || lineToClean.StartsWith("$(END_MARKING")
                        || lineToClean.StartsWith("$(POLY") || lineToClean.StartsWith("$(SQUARE"))
                    {

                        var lineCleaned = CleanLine(lineToClean);

                        if (lineCleaned.StartsWith("M30")) break;

                        //SubPrograms
                        if (lineCleaned.StartsWith("Q"))
                        {
                            //Be Sure to take just the Q command and not comments or other stuff
                            var q = GcodeAxesQuotaRegex.Match(lineCleaned).Value;

                            var labelToFind = q.Replace("Q", "N");

                            if (_dicSubprograms.ContainsKey(labelToFind) == false)
                                ReadSubprograms(lines, lineNumber);

                            if (_dicSubprograms.ContainsKey(labelToFind))
                            {
                                foreach (var item in _dicSubprograms[labelToFind])
                                {
                                    lstMoves.Add(new Tuple<int, string>(item.Item1, item.Item2));
                                }

                            }

                            continue;

                        }

                        #region Handle GO
                        if (lineCleaned.StartsWith("GO"))
                        {
                            //Is not reading label but if it was reset to it
                            readingLabel = false;
                            //Need to search the label xx in the GOxx
                            jumpingIntoLabel = true;


                            lineCleaned = lineCleaned.Replace(" ", "");

                            //Take just the integer part
                            searchingForLabel = int.Parse(Regex.Match(lineCleaned, @"\d+").Value);

                            //If the label operations are already present in the dictionary then print it. Better to not enter here
                            //Because I have to set that the file reading index is at the end of this foreach
                            if (dicOperationsInLabel.ContainsKey(searchingForLabel))
                            {
                                var lst = dicOperationsInLabel[searchingForLabel];
                                foreach (var item in lst)
                                {
                                    lstMoves.Add(new Tuple<int, string>(lineNumber, lineCleaned));
                                }
                            }

                            //Get the next line
                            continue;
                        }

                        if (lineCleaned.StartsWith("N") && lineCleaned.Contains("P200=") == false)
                        {
                            readingLabel = true;
                            lineCleaned = lineCleaned.Replace(" ", "");

                            var labelFounded = int.Parse(Regex.Match(lineCleaned, @"\d+").Value);

                            //If is searching for a label from GOXX and this is the label searched then
                            //Stop searching.
                            if (searchingForLabel == int.Parse(Regex.Match(lineCleaned, @"\d+").Value))
                            {
                                jumpingIntoLabel = false;
                            }

                            if (lstLabelOperations.Count > 0)
                                lstLabelOperations = new List<string>();

                            dicOperationsInLabel.Add(labelFounded, lstLabelOperations);


                            continue;
                        }

                        if (jumpingIntoLabel)
                        {
                            continue;
                        }

                        if (readingLabel)
                        {
                            lstLabelOperations.Add(lineCleaned);
                        }
                        #endregion

                        #region Handle Variables
                        if ((lineCleaned.StartsWith("P") || lineCleaned.StartsWith("LV")) && lineCleaned.Contains("=") && lineCleaned.Contains("$S25") == false)
                        {
                            MatchCollection m = VariableDeclarationRegex.Matches(lineCleaned);

                            foreach (var item in m)
                            {
                                var varDeclaration = item.ToString().Split(new string[] { "=" }, StringSplitOptions.RemoveEmptyEntries);
                                double.TryParse(varDeclaration[1].Trim(), out double val);
                                dicVariables.Add(varDeclaration[0].Trim(), val.ToString());
                            }
                            continue;
                        }
                        else if ((lineCleaned.StartsWith("P") || lineCleaned.StartsWith("LV")) && lineCleaned.Contains("$S25"))
                        {
                            //Dichiarazione timer da skippare
                            continue;
                        }
                        #endregion

                        #region Handle line that use variables
                        //var rr = Regex.Match(lineCleaned, VariableDeclarationRegex).Value;
                        var varFounded = (Regex.Match(lineCleaned, @"[P]\d+|LV\d+")).Value;
                        //var rr = VariableDeclarationRegex.Match(lineCleaned).Value;
                        if (varFounded != "")
                        {
                            foreach (var variable in dicVariables)
                            {
                                if (lineCleaned.Contains(variable.Key))
                                    lineCleaned = lineCleaned.Replace(variable.Key, variable.Value);

                            }
                        }
                        #endregion

                        lstMoves.Add(new Tuple<int, string>(lineNumber, lineCleaned));
                    }


                }
                catch (Exception ex)
                {
                    //logger.Error("Error parsing part-program header when reading line: " + line + " - line-number: " + lineNumber + " - in file " + fileName + " " + ex.Message + ex.StackTrace);
                }
            }

            ProgramContext programContext = new ProgramContext();

            programContext.ReferenceMove = new LinearMove();
            programContext.ReferenceMove.EndPoint = new Point3D(0, 0, 0);// programContext.ReferenceMove != null ? new Point3D(programContext.ReferenceMove.EndPoint.X, programContext.ReferenceMove.EndPoint.Y, programContext.ReferenceMove.EndPoint.Z) : new Point3D(0, 0, 0);

            programContext.LastEntity = new LinearMove();
            programContext.LastEntity.EndPoint = new Point3D(0, 0, 0);



            try
            {
                foreach (var t in lstMoves)
                {

                    var line = t.Item2.Trim();
                    if (line.StartsWith("G08") || line.StartsWith("G09") || line.StartsWith("G40") || line.StartsWith("G41")) continue;
                    programContext.SourceLine = t.Item1;

                    IEntity entity = null;
                    if (line.StartsWith("$"))
                    {
                        ParseMacro(line, ref programContext, ref entity);
                    }
                    else if (line.StartsWith("G"))
                    {
                        ParseGLine(line, ref programContext, ref entity);
                    }
                    else if (line.StartsWith("N"))
                    {
                        ParseNLine(line, ref programContext, ref entity);
                    }
                    else if (line.StartsWith("Q"))
                    {
                        ParseQLine(line, ref programContext, ref entity);
                    }
                    else if (line.StartsWith("M"))
                    {
                        ParseMLine(line, ref programContext, ref entity);
                    }

                    if (entity != null)
                    {
                        moves.Add(entity);
                    }
                }

#if DEBUG
                wat.Stop();
                Console.WriteLine($"Time to parse Iso: ms {wat.ElapsedTicks}");
#endif
                return moves;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error " + ex.Message);

            }


            return moves;
        }

        private void ReadSubprograms(List<string> lst, int lineNumber)
        {
            try
            {
                bool startReading = false;
                var lstSubInstructions = new List<Tuple<int, string>>();
                var readingSub = false;
                for (int i = lineNumber; i < lst.Count; i++)
                {
                    var line = lst[i];
                    if (line == "" || line.StartsWith("//") || line.StartsWith("(*")) continue;
                    if (line.StartsWith("M30"))
                    {
                        startReading = true;
                        readingSub = false;
                    }

                    if (startReading == false) continue;


                    if (line.StartsWith("N"))
                    {
                        var n = GcodeAxesQuotaRegex.Match(line).Value;
                        lstSubInstructions = new List<Tuple<int, string>>();
                        _dicSubprograms.Add(n, lstSubInstructions);
                        readingSub = true;
                        continue;
                    }

                    if (line.StartsWith("M01") || line.StartsWith("M02"))
                    {
                        readingSub = false;
                    }
                    if (readingSub)
                    {
                        lstSubInstructions.Add(new Tuple<int, string>(i, line));
                    }

                }
            }
            catch (Exception)
            {

            }
        }


        private void ParseMLine(string line, ref ProgramContext programContext, ref IEntity entity)
        {
            //throw new NotImplementedException();
        }

        private void ParseQLine(string line, ref ProgramContext programContext, ref IEntity entity)
        {
            //throw new NotImplementedException();
        }

        private void ParseNLine(string line, ref ProgramContext programContext, ref IEntity entity)
        {
            //throw new NotImplementedException();
        }

        private void ParseGLine(string line, ref ProgramContext programContext, ref IEntity entity)
        {
            if (line.StartsWith("G00 Z=P1") && programContext.Is2DProgram)
            {
                return;
            }

            var m = GcodeAxesQuotaRegex.Matches(line);

            if (m[0].Value.Length > 1)
            {
                var gCodeNumber = int.Parse(m[0].Value.Substring(1));

                switch (gCodeNumber)
                {
                    case 0:
                    case 1:
                        if (m.Count == 1) return;
                        entity = new LinearMove { OriginalLine = line };
                        entity = CalculateStartAndEndPoint(programContext, entity, m);

                        break;
                    case 70:
                    case 71:
                        _conversionValue = gCodeNumber == 70 ? 25.4 : 1;
                        programContext.IsInchProgram = gCodeNumber == 70;
                        break;
                    case 2:
                    case 3:

                        break;

                    case 92:
                    case 93:
                    case 113:

                        var refMove = programContext.ReferenceMove != null ? programContext.ReferenceMove : new LinearMove();

                        //It's like to not have axes in the instruction such as G93
                        if (m.Count == 1)
                        {
                            refMove.EndPoint = new Point3D(0, 0, 0);
                        }
                        else
                        {
                            BuildMove(ref refMove, new Point3D(0, 0, 0), m, programContext);
                        }
                        programContext.ReferenceMove = refMove;

                        break;

                    case 90:
                        programContext.IsIncremental = false;
                        break;
                    case 91:
                        programContext.IsIncremental = true;
                        break;
                    case 100:
                        break;

                    case 102:
                    case 103:
                        break;
                    case 104:
                        entity = new ArcMove { OriginalLine = line };
                        entity = CalculateStartAndEndPoint(programContext, entity, m);



                        break;


                    default:
                        break;
                }

                if (entity != null)
                {
                    entity.Is2DProgram = programContext.Is2DProgram;
                    programContext.LastEntity = entity;
                }
            }
        }

        private IEntity CalculateStartAndEndPoint(ProgramContext programContext, IEntity entity, MatchCollection m)
        {
            entity.SourceLine = programContext.SourceLine;
            entity.IsBeamOn = programContext.IsBeamOn;
            entity.LineColor = programContext.ContourLineType;
            if (entity.SourceLine > 13145)
            {

            }
            entity.StartPoint = Create3DPoint(programContext, programContext.LastEntity.EndPoint);

            BuildMove(ref entity, entity.StartPoint, m, programContext);


            if (programContext.IsIncremental == false)
            {
                entity.EndPoint = Create3DPoint(programContext, programContext.ReferenceMove.EndPoint, entity.EndPoint); //new Point3D(programContext.ReferenceMove.EndPoint.X + entity.EndPoint.X, programContext.ReferenceMove.EndPoint.Y + entity.EndPoint.Y, programContext.ReferenceMove.EndPoint.Z + entity.EndPoint.Z);
            }
            else
            {
                entity.EndPoint = Create3DPoint(programContext, programContext.ReferenceMove.EndPoint, programContext.LastEntity.EndPoint, entity.EndPoint); //new Point3D(programContext.ReferenceMove.EndPoint.X + entity.EndPoint.X, programContext.ReferenceMove.EndPoint.Y + entity.EndPoint.Y, programContext.ReferenceMove.EndPoint.Z + entity.EndPoint.Z);
            }


            if (entity is ArcMove)
            {
                var arcMove = (entity as ArcMove);
                arcMove.ViaPoint = Create3DPoint(programContext, programContext.ReferenceMove.EndPoint, arcMove.ViaPoint);
                GeoHelper.AddCircularMoveProperties(ref arcMove);
            }


            return entity;
        }

        private Point3D Create3DPoint(ProgramContext programContext, Point3D p1, Point3D p2 = default, Point3D p3 = default)
        {
            if (p3 != null)
            {
                return new Point3D((p1.X + p2.X + p3.X), (p1.Y + p2.Y + p3.Y), programContext.Is2DProgram ? 0 : (p1.Z + p2.Z + p3.Z));
            }
            else if (p2 != null)
            {
                return new Point3D((p1.X + p2.X), (p1.Y + p2.Y), programContext.Is2DProgram ? 0 : (p1.Z + p2.Z));
            }
            else if (p1 != null)
            {
                return new Point3D((p1.X), (p1.Y), programContext.Is2DProgram ? 0 : p1.Z);
            }

            return new Point3D(0, 0, 0);

        }

        private double Converter(double value)
        {
            return value * _conversionValue;
        }

        public double Converter(string value)
        {
            return Converter(double.Parse(value));
        }


        private void ParseMacro(string line, ref ProgramContext programContext, ref IEntity entity)
        {
            if (line.StartsWith("$(WORK_ON)") || line.StartsWith("$(MARKING)") || line.StartsWith("$(BEAM_ON)"))
            {
                programContext.IsBeamOn = true;

                if (programContext.IsMarkingProgram == false)
                {
                    if (line.StartsWith("$(WORK_ON)"))
                    {
                        var lineType = int.Parse(line.Replace("$(WORK_ON)", string.Empty).Replace("(", "").Trim().Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries)[0]);
                        programContext.ContourLineType = (ELineType)lineType;
                    }
                    else if (line.StartsWith("$(MARKING)"))
                    {
                        programContext.ContourLineType = ELineType.Marking;
                    }
                }
                else
                {
                    programContext.ContourLineType = ELineType.Marking;
                }
            }
            else if (line.StartsWith("$(WORK_OFF)") || line.StartsWith("$(END_MARKING)") || line.StartsWith("$(BEAM_OFF)"))
            {
                programContext.IsBeamOn = false;
                programContext.ContourLineType = ELineType.Rapid;
            }
            else if (line.StartsWith("$(MARKING_PIECE)"))
            {
                programContext.IsMarkingProgram = true;
            }
            else if (line.StartsWith("$(HOLE)"))
            {

                var macroParFounded = MacroParsRegex.Matches(line);
#if DEBUG
                wat.Restart();
#endif
                var cX = Converter(macroParFounded[0].Value);
                var cY = Converter(macroParFounded[1].Value);
                var cZ = Converter(macroParFounded[2].Value);
                Point3D pC = new Point3D(cX, cY, cZ);
                var nX = Converter(macroParFounded[3].Value);
                var nY = Converter(macroParFounded[4].Value);
                var nZ = Converter(macroParFounded[5].Value);
                Point3D pN = new Point3D(nX, nY, nZ);

                var radius = Converter(macroParFounded[6].Value);

                entity = new ArcMove
                {
                    IsStroked = true,
                    IsLargeArc = true,
                    SourceLine = programContext.SourceLine,
                    IsBeamOn = programContext.IsBeamOn,
                    LineColor = programContext.ContourLineType,
                    OriginalLine = line,
                    Radius = Math.Abs(radius),//Aggiunto math.abs perchè... Se si vedessero comportamenti strani, verificare che la direzione della normale segua la regola della mano sinistra rispetto al verso di percorrenza dell'arco.
                    CenterPoint = Create3DPoint(programContext, programContext.ReferenceMove.EndPoint, pC),
                    NormalPoint = Create3DPoint(programContext, programContext.ReferenceMove.EndPoint, pN),
                };

                var holeMacro = entity as ArcMove;
                GeoHelper.GetMoveFromMacroHole(ref holeMacro);
#if DEBUG
                wat.Stop();
                Console.WriteLine($"ArcMove ticks: {wat.ElapsedTicks}");
#endif
            }
            else if (line.StartsWith("$(WORK_TYPE)"))
            {
                var macroParFounded = MacroParsRegex.Match(line).Value;
                programContext.Is2DProgram = macroParFounded == "1";

                programContext.Is3DProgram = macroParFounded == "2";
                programContext.IsTubeProgram = macroParFounded == "3";
                programContext.IsWeldProgram = macroParFounded == "4";
            }
        }

        private string CleanLine(string lineToClean)
        {

            if (lineToClean.Contains("//") || lineToClean.Contains("(*"))
            {
                int splitcomment = lineToClean.IndexOf("//") != -1 ? lineToClean.IndexOf("//") : lineToClean.IndexOf("(*") != -1 ? lineToClean.IndexOf("(*") : 0;
                lineToClean = lineToClean.Substring(0, splitcomment);
            }
            if (lineToClean.Contains("#")) lineToClean = lineToClean.Substring(0, lineToClean.IndexOf("#"));
            if (lineToClean.Contains("  ")) lineToClean = lineToClean.Replace("  ", " ");
            if (lineToClean.Contains(") (")) lineToClean = lineToClean.Replace(") (", ")(");
            if (lineToClean.Contains(" =")) lineToClean = lineToClean.Replace(" =", "=").Replace("= ", "=");
            if (lineToClean.Contains("LF=")) lineToClean = lineToClean.Substring(0, lineToClean.IndexOf("LF"));
            if (lineToClean.Contains("F=")) lineToClean = lineToClean.Substring(0, lineToClean.IndexOf("F="));

            return lineToClean.Trim();
        }


        private void BuildMove(ref IEntity move, Point3D startPoint, MatchCollection matches, ProgramContext programContext)
        {
            var endPoint = new Point3D(0, 0, 0);
            var viaPoint = new Point3D(0, 0, 0);

            for (int i = 1; i < matches.Count; i++)
            {
                var ax = matches[i].ToString();

                var axName = ax[0];

                if (axName == 'A' || axName == 'B' || axName == 'F' || axName == 'L' || axName == 'C' || axName == 'U' || axName == 'V' || axName == 'W' || axName == 'G') continue;

                var axValue = ax.Substring(1).Replace("=", "");
                if (char.IsLetter(axValue[0]) == true) axValue = "0";

                var axValueD = GetQuotaValue(axValue.Trim(), programContext);

                switch (axName)
                {
                    case 'X': endPoint.X = axValueD; break;
                    case 'Y': endPoint.Y = axValueD; break;
                    case 'Z': endPoint.Z = programContext.Is2DProgram ? 0.0 : axValueD; break;

                    case 'I': viaPoint.X = axValueD; break;
                    case 'J': viaPoint.Y = axValueD; break;
                    case 'K': viaPoint.Z = programContext.Is2DProgram ? 0.0 : axValueD; break;

                    default:
                        break;
                }



            }

            move.EndPoint = endPoint;

            if (move.EntityType == EEntityType.Arc || move.EntityType == EEntityType.Circle)
            {
                (move as ArcMove).ViaPoint = viaPoint;
            }
        }

        private double GetQuotaValue(string axValue, ProgramContext programContext)
        {
            if (double.TryParse(axValue, out var quota))
            {
                return Converter(quota);
            }
            else
            {
                if (axValue.Contains("#"))
                {
                    var searchingForLabel = int.Parse(Regex.Match(axValue, @"\d+\.+\d+").Value);
                }

                bool result = axValue.Any(x => !char.IsLetter(x));

                if (result)
                {
                    string formula = axValue;
                    //StringToFormula stf = new StringToFormula();
                    var eval = StringToFormula.Eval(formula);
                    return Converter(eval);
                }
            }

            if (axValue == "MVX" || axValue == "MVY" || axValue == "MVZ")
            {
                return 0.0;
            }

            if (axValue.Contains("P") || axValue.Contains("LV"))
            {


            }



            return double.Parse(axValue);
        }
    }


}