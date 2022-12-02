using ParserLib.Helpers;
using ParserLib.Interfaces;
using ParserLib.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using static ParserLib.Helpers.GeoHelper;
using static ParserLib.Helpers.TechnoHelper;

namespace ParserLib.Services.Parsers
{

    public class ParseIso : IParser
    {
        public string Filename { get; set; }
        private Regex GcodeAxesQuotaRegex;
        private Regex VariableDeclarationRegex;
        private Regex MacroParsRegex;
        private double _conversionValue = 1;
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
        HashSet<string> macroNotConverted = new HashSet<string>();
        Dictionary<string, List<Tuple<int, string>>> _dicSubprograms;

        //private SemaphoreSlim sm = new SemaphoreSlim(1);

        public static Dictionary<int, string> ReadTextLinesAsync(string path)
        {
            //Stopwatch dt = new Stopwatch();
            //dt.Start();

            Dictionary<int, string> dic = new Dictionary<int, string>();

            //var fileTextContent = new string[40000];
            using (StreamReader sr = File.OpenText(path))
            {
                int x = 0;
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();
                    line = line.Trim().ToUpper();
                    if (line.StartsWith("//") || line.StartsWith("(*") || line.StartsWith(";") || line.Equals(string.Empty) ||
                        line.StartsWith("G09") || line.StartsWith("G08") || line.StartsWith("F=") ||
                        line.StartsWith("G40") || line.StartsWith("G41") || line.StartsWith("G42") ||
                        line.StartsWith("EI") || line.StartsWith("GO*") || line.StartsWith("<MATERIAL"))
                    {
                        //fileTextContent[x] = string.Empty;
                    }
                    else
                    {
                        dic[x] = line;
                        //fileTextContent[x] = line;
                    }
                    x += 1;
                }
                //Array.Resize(ref fileTextContent, x);
            } //Finished. Close the file
            //dt.Stop();

            //Console.WriteLine($"{dt.ElapsedMilliseconds}");
            return dic;
        }


        public IProgramContext GetProgramContext()
        {
            var programContext = new ProgramContext();

            programContext.ReferenceMove = new LinearMove();
            programContext.ReferenceMove.EndPoint = new Point3D(0, 0, 0);// programContext.ReferenceMove != null ? new Point3D(programContext.ReferenceMove.EndPoint.X, programContext.ReferenceMove.EndPoint.Y, programContext.ReferenceMove.EndPoint.Z) : new Point3D(0, 0, 0);

            programContext.LastEntity = new LinearMove();
            programContext.LastEntity.EndPoint = new Point3D(0, 0, 0);

            programContext.Moves = GetMoves(programContext);

            return programContext;
        }

        private SortedDictionary<int, string> ReadAndFilterLinesFromFile()
        {
            try
            {
                var dic_LineNumber_Line = ReadTextLinesAsync(Filename);
                _dicSubprograms = new Dictionary<string, List<Tuple<int, string>>>();

                Dictionary<int, string> dic = new Dictionary<int, string>();
                Dictionary<string, string> dicVariables = new Dictionary<string, string>();
                Dictionary<int, List<string>> dicOperationsInLabel = new Dictionary<int, List<string>>();

                List<string> lstLabelOperations = new List<string>();

                bool jumpingIntoLabel = false;
                bool readingLabel = false;
                var searchingForLabel = -1;

                Parallel.ForEach(dic_LineNumber_Line.Keys, (key) =>
                {

                    var lineToClean = dic_LineNumber_Line[key];
                    var lineNumber = key;

                    if (lineToClean.StartsWith("$") == false
                                || lineToClean.StartsWith("$(WORK_ON") || lineToClean.StartsWith("$(WORK_OFF") || lineToClean.StartsWith("$(WORK_TYPE)") || lineToClean.StartsWith("$(BEAM_ON")
                                || lineToClean.StartsWith("$(BEAM_OFF") || lineToClean.StartsWith("$(HOLE)") || lineToClean.StartsWith("$(SLOT") || lineToClean.StartsWith("$(RECT")
                                || lineToClean.StartsWith("$(CIRCLE") || lineToClean.StartsWith("$(KEYHOLE") || lineToClean.StartsWith("$(MARKING") || lineToClean.StartsWith("$(END_MARKING")
                                || lineToClean.StartsWith("$(POLY") || lineToClean.StartsWith("$(SQUARE"))
                    {
                        var lineCleaned = CleanLine(lineToClean);

                        if (lineCleaned.StartsWith("M30")) return;

                        //SubPrograms
                        if (lineCleaned.StartsWith("Q"))
                        {
                            //Be Sure to take just the Q command and not comments or other stuff
                            var q = GcodeAxesQuotaRegex.Match(lineCleaned).Value;

                            var labelToFind = q.Replace("Q", "N");

                            if (_dicSubprograms.ContainsKey(labelToFind))
                            {
                                foreach (var item in _dicSubprograms[labelToFind])
                                {
                                    if (dic.ContainsKey(item.Item1) == false)
                                    {
                                        lock (dic)
                                        {
                                            dic.Add(item.Item1, item.Item2);
                                        }
                                    }
                                }
                            }
                            return;
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
                                    lock (dic)
                                    {
                                        dic.Add(lineNumber, lineCleaned);
                                    }

                                }
                            }

                            //Get the next line
                            return;
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

                            if (dicOperationsInLabel.ContainsKey(labelFounded) == false)
                            {
                                lock (dicOperationsInLabel)
                                {
                                    dicOperationsInLabel.Add(labelFounded, lstLabelOperations);
                                }
                            }


                            return;
                        }

                        if (jumpingIntoLabel)
                        {
                            return;
                        }

                        if (readingLabel)
                        {
                            lock (lstLabelOperations)
                            {
                                lstLabelOperations.Add(lineCleaned);
                            }
                        }
                        #endregion

                        #region Handle Variables
                        if ((lineCleaned.StartsWith("P") || lineCleaned.StartsWith("LV")) && lineCleaned.Contains("=") && lineCleaned.Contains("$S25") == false)
                        {
                            lineCleaned=lineCleaned.Replace(" =", "=").Replace("= ", "=").Replace(" = ", "=");
                            MatchCollection m = VariableDeclarationRegex.Matches(lineCleaned);

                            foreach (var item in m)
                            {
                                var varDeclaration = item.ToString().Split(new string[] { "=" }, StringSplitOptions.RemoveEmptyEntries);
                                double.TryParse(varDeclaration[1].Trim(), out double val);
                                if (dicVariables.ContainsKey(varDeclaration[0].Trim()) == false && varDeclaration != null)
                                {
                                    lock (dicVariables)
                                    {
                                        dicVariables.Add(varDeclaration[0].Trim(), val.ToString());
                                    }
                                }
                            }
                            return;
                        }
                        else if ((lineCleaned.StartsWith("P") || lineCleaned.StartsWith("LV")) && lineCleaned.Contains("$S25"))
                        {
                            return;
                        }
                        #endregion

                        #region Handle line that use variables
                        //var rr = Regex.Match(lineCleaned, VariableDeclarationRegex).Value;
                        var varFounded1 = (Regex.Matches(lineCleaned, @"[P]\d+|LV\d+"));

                        foreach (var item in varFounded1)
                        {
                            if (dicVariables.ContainsKey(item.ToString()))
                            {
                                lineCleaned = lineCleaned.Replace(item.ToString(), dicVariables[item.ToString()]);
                            }
                        }
                        #endregion

                        lock (dic)
                        {
                            dic.Add(lineNumber, lineCleaned);
                        }
                    }
                });

                var dic1 = new SortedDictionary<int, string>(dic);

                return dic1;
                #region Commented
                //foreach (var lineToClean in lines)
                //{
                //    try
                //    {

                //        lineNumber += 1;

                //        if (lineToClean.StartsWith(";") || lineToClean.StartsWith("(*") || lineToClean.StartsWith("//") || string.IsNullOrEmpty(lineToClean) || string.IsNullOrWhiteSpace(lineToClean))
                //        {
                //            continue;
                //        }
                //        if (lineToClean.StartsWith("<MATERIAL")) { continue; }

                //        if (lineToClean.StartsWith("G08") || lineToClean.StartsWith("G09") || lineToClean.StartsWith("F") || lineToClean.StartsWith("EI") ||
                //            lineToClean.StartsWith("G40") || lineToClean.StartsWith("GO*") || lineToClean.StartsWith("G41") || lineToClean.StartsWith("G42") || lineToClean.StartsWith("<MATERIAL"))
                //        {
                //            continue;
                //        }

                //        if (lineToClean.StartsWith("$") == false
                //            || lineToClean.StartsWith("$(WORK_ON") || lineToClean.StartsWith("$(WORK_OFF") || lineToClean.StartsWith("$(WORK_TYPE)") || lineToClean.StartsWith("$(BEAM_ON")
                //            || lineToClean.StartsWith("$(BEAM_OFF") || lineToClean.StartsWith("$(HOLE)") || lineToClean.StartsWith("$(SLOT") || lineToClean.StartsWith("$(RECT")
                //            || lineToClean.StartsWith("$(CIRCLE") || lineToClean.StartsWith("$(KEYHOLE") || lineToClean.StartsWith("$(MARKING") || lineToClean.StartsWith("$(END_MARKING")
                //            || lineToClean.StartsWith("$(POLY") || lineToClean.StartsWith("$(SQUARE"))
                //        {

                //            var lineCleaned = CleanLine(lineToClean);

                //            if (lineCleaned.StartsWith("M30")) break;

                //            //SubPrograms
                //            if (lineCleaned.StartsWith("Q"))
                //            {
                //                //Be Sure to take just the Q command and not comments or other stuff
                //                var q = GcodeAxesQuotaRegex.Match(lineCleaned).Value;

                //                var labelToFind = q.Replace("Q", "N");

                //                if (_dicSubprograms.ContainsKey(labelToFind) == false)
                //                    ReadSubprograms(lines, lineNumber);

                //                if (_dicSubprograms.ContainsKey(labelToFind))
                //                {
                //                    foreach (var item in _dicSubprograms[labelToFind])
                //                    {
                //                        lstMoves.Add(new Tuple<int, string>(item.Item1, item.Item2));
                //                    }

                //                }

                //                continue;

                //            }

                //            #region Handle GO
                //            if (lineCleaned.StartsWith("GO"))
                //            {
                //                //Is not reading label but if it was reset to it
                //                readingLabel = false;
                //                //Need to search the label xx in the GOxx
                //                jumpingIntoLabel = true;


                //                lineCleaned = lineCleaned.Replace(" ", "");

                //                //Take just the integer part
                //                searchingForLabel = int.Parse(Regex.Match(lineCleaned, @"\d+").Value);

                //                //If the label operations are already present in the dictionary then print it. Better to not enter here
                //                //Because I have to set that the file reading index is at the end of this foreach
                //                if (dicOperationsInLabel.ContainsKey(searchingForLabel))
                //                {
                //                    var lst = dicOperationsInLabel[searchingForLabel];
                //                    foreach (var item in lst)
                //                    {
                //                        lstMoves.Add(new Tuple<int, string>(lineNumber, lineCleaned));
                //                    }
                //                }

                //                //Get the next line
                //                continue;
                //            }

                //            if (lineCleaned.StartsWith("N") && lineCleaned.Contains("P200=") == false)
                //            {
                //                readingLabel = true;
                //                lineCleaned = lineCleaned.Replace(" ", "");

                //                var labelFounded = int.Parse(Regex.Match(lineCleaned, @"\d+").Value);

                //                //If is searching for a label from GOXX and this is the label searched then
                //                //Stop searching.
                //                if (searchingForLabel == int.Parse(Regex.Match(lineCleaned, @"\d+").Value))
                //                {
                //                    jumpingIntoLabel = false;
                //                }

                //                if (lstLabelOperations.Count > 0)
                //                    lstLabelOperations = new List<string>();

                //                dicOperationsInLabel.Add(labelFounded, lstLabelOperations);


                //                continue;
                //            }

                //            if (jumpingIntoLabel)
                //            {
                //                continue;
                //            }

                //            if (readingLabel)
                //            {
                //                lstLabelOperations.Add(lineCleaned);
                //            }
                //            #endregion

                //            #region Handle Variables
                //            if ((lineCleaned.StartsWith("P") || lineCleaned.StartsWith("LV")) && lineCleaned.Contains("=") && lineCleaned.Contains("$S25") == false)
                //            {
                //                MatchCollection m = VariableDeclarationRegex.Matches(lineCleaned);

                //                foreach (var item in m)
                //                {
                //                    var varDeclaration = item.ToString().Split(new string[] { "=" }, StringSplitOptions.RemoveEmptyEntries);
                //                    double.TryParse(varDeclaration[1].Trim(), out double val);
                //                    dicVariables.Add(varDeclaration[0].Trim(), val.ToString());
                //                }
                //                continue;
                //            }
                //            else if ((lineCleaned.StartsWith("P") || lineCleaned.StartsWith("LV")) && lineCleaned.Contains("$S25"))
                //            {
                //                //Dichiarazione timer da skippare
                //                continue;
                //            }
                //            #endregion

                //            #region Handle line that use variables
                //            //var rr = Regex.Match(lineCleaned, VariableDeclarationRegex).Value;
                //            var varFounded = (Regex.Match(lineCleaned, @"[P]\d+|LV\d+")).Value;
                //            //var rr = VariableDeclarationRegex.Match(lineCleaned).Value;
                //            if (varFounded != "")
                //            {
                //                foreach (var variable in dicVariables)
                //                {
                //                    if (lineCleaned.Contains(variable.Key))
                //                        lineCleaned = lineCleaned.Replace(variable.Key, variable.Value);

                //                }
                //            }
                //            #endregion

                //            lstMoves.Add(new Tuple<int, string>(lineNumber, lineCleaned));
                //        }


                //    }
                //    catch (Exception ex)
                //    {
                //        //logger.Error("Error parsing part-program header when reading line: " + line + " - line-number: " + lineNumber + " - in file " + fileName + " " + ex.Message + ex.StackTrace);
                //    }
                //}

                //});
                #endregion

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error " + ex.Message);
            }

            return null;
        }

        private IList<IBaseEntity> GetMoves(ProgramContext programContext = null)
        {

            var lstMoves = ReadAndFilterLinesFromFile();

            if (programContext == null)
            {
                programContext = new ProgramContext();

                programContext.ReferenceMove = new LinearMove();
                programContext.ReferenceMove.EndPoint = new Point3D(0, 0, 0);// programContext.ReferenceMove != null ? new Point3D(programContext.ReferenceMove.EndPoint.X, programContext.ReferenceMove.EndPoint.Y, programContext.ReferenceMove.EndPoint.Z) : new Point3D(0, 0, 0);

                programContext.LastEntity = new LinearMove();
                programContext.LastEntity.EndPoint = new Point3D(0, 0, 0);
            }

            IList<IBaseEntity> moves = GetMoves(programContext, lstMoves);

            //foreach (var item in macroNotConverted)
            //{
            //    Console.WriteLine(item);
            //}

            return moves;
        }

        private IList<IBaseEntity> GetMoves(ProgramContext programContext, SortedDictionary<int, string> lstMoves)
        {

            List<IBaseEntity> moves = new List<IBaseEntity>();

            try
            {

                foreach (var t in lstMoves)
                {
                    var line = t.Value;
                    if (line == null) continue;
                    if (line.StartsWith("G08") || line.StartsWith("G09") || line.StartsWith("G40") || line.StartsWith("G41")) continue;

                    programContext.SourceLine = t.Key;

                    IBaseEntity entity = null;
                    if (line.StartsWith("$"))
                    {
                        ParseMacro(line, ref programContext, ref entity);
                    }
                    else if (line.StartsWith("G"))
                    {
                        ParseGLine(line, ref programContext, ref entity);
                    }

                    if (entity != null)
                    {
                        moves.Add(entity);
                        programContext.UpdateProgramCenterPoint();
                    }
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine("Error " + ex.Message);
            }


            return moves;
        }

        private void ReadSubprograms(IList<string> lst, int lineNumber)
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

        private void ParseGLine(string line, ref ProgramContext programContext, ref IBaseEntity entity)
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
                        //if (m.Count == 1)
                        //{
                        //    refMove.EndPoint = new Point3D(0, 0, 0);
                        //}
                        //else
                        //{
                        //    BuildMove(ref refMove, new Point3D(0, 0, 0), m, programContext);
                        //}
                        //programContext.ReferenceMove = refMove;

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
                    programContext.LastEntity = (entity as IEntity);
                }
            }
        }

        private IBaseEntity CalculateStartAndEndPoint(ProgramContext programContext, IBaseEntity entity, MatchCollection regexMatches)
        {
            entity.SourceLine = programContext.SourceLine;
            entity.IsBeamOn = programContext.IsBeamOn;
            entity.LineColor = programContext.ContourLineType;

            IEntity e = (entity as IEntity);
            e.StartPoint = Create3DPoint(programContext, programContext.LastEntity.EndPoint);

            BuildMove(ref entity, (entity as IEntity).StartPoint, regexMatches, programContext);

            //if (programContext.IsIncremental == false)
            //{
            //    (entity as IEntity).EndPoint = Create3DPoint(programContext, programContext.ReferenceMove.EndPoint, (entity as IEntity).EndPoint); //new Point3D(programContext.ReferenceMove.EndPoint.X + entity.EndPoint.X, programContext.ReferenceMove.EndPoint.Y + entity.EndPoint.Y, programContext.ReferenceMove.EndPoint.Z + entity.EndPoint.Z);
            //}
            //else
            //{
            e.EndPoint = Create3DPoint(programContext, programContext.ReferenceMove.EndPoint, (entity as IEntity).EndPoint, programContext.IsIncremental ? programContext.LastEntity.EndPoint : new Point3D(0, 0, 0)); //new Point3D(programContext.ReferenceMove.EndPoint.X + entity.EndPoint.X, programContext.ReferenceMove.EndPoint.Y + entity.EndPoint.Y, programContext.ReferenceMove.EndPoint.Z + entity.EndPoint.Z);
            //}

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

        private void ParseMacro(string line, ref ProgramContext programContext, ref IBaseEntity entity)
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

            }
            else if (line.StartsWith("$(SLOT)"))
            {

                var macroParFounded = MacroParsRegex.Matches(line);

                var c1X = Converter(macroParFounded[0].Value);
                var c1Y = Converter(macroParFounded[1].Value);
                var c1Z = Converter(macroParFounded[2].Value);
                Point3D pC1 = new Point3D(c1X, c1Y, c1Z);

                var c2X = Converter(macroParFounded[3].Value);
                var c2Y = Converter(macroParFounded[4].Value);
                var c2Z = Converter(macroParFounded[5].Value);
                Point3D pC2 = new Point3D(c2X, c2Y, c2Z);

                var nX = Converter(macroParFounded[6].Value);
                var nY = Converter(macroParFounded[7].Value);
                var nZ = Converter(macroParFounded[8].Value);
                Point3D pN = new Point3D(nX, nY, nZ);

                var radius = Converter(macroParFounded[9].Value);


                entity = new SlotMove()
                {

                    SourceLine = programContext.SourceLine,
                    IsBeamOn = programContext.IsBeamOn,
                    LineColor = programContext.ContourLineType,
                    OriginalLine = line,
                    Arc1 = new ArcMove
                    {
                        SourceLine = programContext.SourceLine,
                        IsBeamOn = programContext.IsBeamOn,
                        LineColor = programContext.ContourLineType,
                        OriginalLine = line,
                        IsStroked = true,
                        IsLargeArc = true,
                        Radius = Math.Abs(radius),
                        CenterPoint = Create3DPoint(programContext, programContext.ReferenceMove.EndPoint, pC1),
                        NormalPoint = Create3DPoint(programContext, programContext.ReferenceMove.EndPoint, pN),
                    },
                    Arc2 = new ArcMove
                    {
                        SourceLine = programContext.SourceLine,
                        IsBeamOn = programContext.IsBeamOn,
                        LineColor = programContext.ContourLineType,
                        OriginalLine = line,
                        IsStroked = true,
                        IsLargeArc = true,
                        Radius = Math.Abs(radius),
                        CenterPoint = Create3DPoint(programContext, programContext.ReferenceMove.EndPoint, pC2),
                        NormalPoint = Create3DPoint(programContext, programContext.ReferenceMove.EndPoint, pN),
                    },
                    Line1 = new LinearMove()
                    {
                        SourceLine = programContext.SourceLine,
                        IsBeamOn = programContext.IsBeamOn,
                        LineColor = programContext.ContourLineType,
                        OriginalLine = line,
                    },
                    Line2 = new LinearMove()
                    {
                        SourceLine = programContext.SourceLine,
                        IsBeamOn = programContext.IsBeamOn,
                        LineColor = programContext.ContourLineType,
                        OriginalLine = line,
                    }

                };


                var slotMove = entity as SlotMove;
                GeoHelper.GetMovesFromMacroSlot(ref slotMove);

            }
            else if (line.StartsWith("$(POLY)"))
            {

                var macroParFounded = MacroParsRegex.Matches(line);

                var c1X = Converter(macroParFounded[0].Value);
                var c1Y = Converter(macroParFounded[1].Value);
                var c1Z = Converter(macroParFounded[2].Value);
                Point3D centerPoint = new Point3D(c1X, c1Y, c1Z);

                var v1X = Converter(macroParFounded[3].Value);
                var v1Y = Converter(macroParFounded[4].Value);
                var v1Z = Converter(macroParFounded[5].Value);
                Point3D vertexPoint = new Point3D(v1X, v1Y, v1Z);

                var nX = Converter(macroParFounded[6].Value);
                var nY = Converter(macroParFounded[7].Value);
                var nZ = Converter(macroParFounded[8].Value);
                Point3D normalPoint = new Point3D(nX, nY, nZ);

                int.TryParse(macroParFounded[9].Value, out int sides);

                var radius = (macroParFounded.Count < 11) ? 0.0 : Converter(macroParFounded[10].Value);

                entity = new PolyMoves()
                {
                    SourceLine = programContext.SourceLine,
                    IsBeamOn = programContext.IsBeamOn,
                    LineColor = programContext.ContourLineType,
                    OriginalLine = line,
                    Sides = sides,
                    Radius = radius,
                    VertexPoint = vertexPoint,
                    NormalPoint = normalPoint,
                    CenterPoint = centerPoint

                };

                var polyMove = entity as PolyMoves;
                GeoHelper.GetMovesFromMacroPoly(ref polyMove);

            }
            else if (line.StartsWith("$(RECT)"))
            {

                var macroParFounded = MacroParsRegex.Matches(line);

                var c1X = Converter(macroParFounded[0].Value);
                var c1Y = Converter(macroParFounded[1].Value);
                var c1Z = Converter(macroParFounded[2].Value);
                Point3D centerPoint = new Point3D(c1X, c1Y, c1Z);

                var v1X = Converter(macroParFounded[3].Value);
                var v1Y = Converter(macroParFounded[4].Value);
                var v1Z = Converter(macroParFounded[5].Value);
                Point3D vertexPoint = new Point3D(v1X, v1Y, v1Z);

                var sX = Converter(macroParFounded[6].Value);
                var sY = Converter(macroParFounded[7].Value);
                var sZ = Converter(macroParFounded[8].Value);
                Point3D sidePoint = new Point3D(sX, sY, sZ);

                entity = new RectMoves()
                {
                    SourceLine = programContext.SourceLine,
                    IsBeamOn = programContext.IsBeamOn,
                    LineColor = programContext.ContourLineType,
                    OriginalLine = line,
                    SidePoint = sidePoint,
                    VertexPoint = vertexPoint,
                    CenterPoint = centerPoint
                };

                var rectMove = entity as RectMoves;
                GeoHelper.GetMovesFromMacroRect(ref rectMove);

            }
            else if (line.StartsWith("$(WORK_TYPE)"))
            {
                var macroParFounded = MacroParsRegex.Match(line).Value;
                programContext.Is2DProgram = macroParFounded == "1";
                programContext.Is3DProgram = macroParFounded == "2";
                programContext.IsTubeProgram = macroParFounded == "3";
                programContext.IsWeldProgram = macroParFounded == "4";
            }
            else if (line.StartsWith("$(APPROACH_MC)") || line.StartsWith("$(RETRACT_MC)"))
            {
                var macroParFounded = MacroParsRegex.Matches(line);

                var X = Converter(macroParFounded[0].Value);
                var Y = Converter(macroParFounded[1].Value);
                var Z = Converter(macroParFounded[2].Value);

                entity = new LinearMove()
                {
                    StartPoint = programContext.LastEntity.StartPoint,
                    EndPoint = new Point3D(X, Y, Z),
                    SourceLine = programContext.SourceLine,
                    IsBeamOn = false,
                    LineColor = ELineType.Rapid,
                    OriginalLine = line
                };
            }
            else
            {

                var macroName = line.Substring(2, line.IndexOf(")") - 2).Trim();
                if (macroNotConverted.Add(macroName))
                {


                }
                //Console.WriteLine(macroName);
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
            //if (lineToClean.Contains(") (")) lineToClean = lineToClean.Replace(") (", ")(");
            if (lineToClean.Contains(" =")) lineToClean = lineToClean.Replace(" =", "=").Replace("= ", "=");
            if (lineToClean.Contains("LF=")) lineToClean = lineToClean.Substring(0, lineToClean.IndexOf("LF"));
            if (lineToClean.Contains("F=")) lineToClean = lineToClean.Substring(0, lineToClean.IndexOf("F="));

            return lineToClean.Trim();
        }

        private void BuildMove(ref IBaseEntity entity, Point3D startPoint, MatchCollection matches, ProgramContext programContext)
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

            (entity as IEntity).EndPoint = endPoint;

            if (entity.EntityType == EEntityType.Arc || entity.EntityType == EEntityType.Circle)
            {
                (entity as ArcMove).ViaPoint = viaPoint;
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