using ParserLib.Helpers;
using ParserLib.Interfaces;
using ParserLib.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using static ParserLib.Helpers.GeoHelper;
using static ParserLib.Helpers.TechnoHelper;

namespace ParserLib.Services.Parsers
{
    public class ParseMpf : IParser
    {
        public string Filename { get; set; }
        private Regex GcodeAxesQuotaRegex;
        private Regex VariableDeclarationRegex;
        private Regex MacroParsRegex;
        private double _conversionValue = 1;
        private Dictionary<string, List<Tuple<int, string>>> _dicSubprograms;
        private static int _indexOfM30;
        private static int _programEndAtLine = 0;


        public ParseMpf(string fileName)
        {

            Filename = fileName;
            string GcodeAxesQuotaPattern = @"\b[A-Za-z]+[\=]?[A-Za-z]*[+-]?\d*\.?\d*([+-]?[*:]?[A-Za-z]*[+-]?\d*\.?\d*)+\b";
            GcodeAxesQuotaRegex = new Regex(GcodeAxesQuotaPattern, RegexOptions.IgnoreCase);
            string VariableDeclarationPattern = @"\b[A-Za-z]+[\d]+([\=]?[A-Za-z]*[+-]?[*:]?\d*\.?\d*)+\b";
            VariableDeclarationRegex = new Regex(VariableDeclarationPattern, RegexOptions.IgnoreCase);
            string macroParsPattern = @"([+-]?[*:]?\d+\.?\d*)+";
            MacroParsRegex = new Regex(macroParsPattern, RegexOptions.IgnoreCase);

        }

        public IProgramContext GetProgramContext()
        {
            var programContext = new ProgramContext
            {
                ReferenceMove = new LinearMove
                {
                    EndPoint = new Point3D(0, 0, 0)// programContext.ReferenceMove != null ? new Point3D(programContext.ReferenceMove.EndPoint.X, programContext.ReferenceMove.EndPoint.Y, programContext.ReferenceMove.EndPoint.Z) : new Point3D(0, 0, 0);
                },

                LastEntity = new LinearMove
                {
                    EndPoint = new Point3D(0, 0, 0),
                },

                LastHeadPosition = new Point3D(0, 0, 0)
            };


            programContext.Moves = GetMoves(programContext);

            return programContext;
        }

        private IList<IBaseEntity> GetMoves(ProgramContext programContext)
        {
            var lstMoves = ReadAndFilterLinesFromFile();

            IList<IBaseEntity> moves = GetMoves(programContext, lstMoves);

            return moves;
        }

        private SortedDictionary<int, string> ReadAndFilterLinesFromFile()
        {
            try
            {
                var dic_LineNumber_Line = ReadProgramFile(Filename);
                _dicSubprograms = new Dictionary<string, List<Tuple<int, string>>>();

                Dictionary<int, string> dic = new Dictionary<int, string>();
                Dictionary<string, string> dicVariables = new Dictionary<string, string>();
                //Dictionary<int, List<string>> dicOperationsInLabel = new Dictionary<int, List<string>>();
                //List<string> lstLabelOperations = new List<string>();
                var variableRegex = @"(PVAR\[\d+\]|LVVAR\[\d+\])";

                //If M30 index is different from the end of the list Size, then probrably there will be subprograms
                if (_indexOfM30 != dic_LineNumber_Line.Count)
                {
                    var lst = dic_LineNumber_Line.Values.ToList();

                    ReadSubprograms(lst, _indexOfM30);
                }

                Parallel.ForEach(dic_LineNumber_Line.Keys, (key, state) =>
                {
                    if (key > _programEndAtLine) return;
                    var lineToClean = dic_LineNumber_Line[key];
                    var lineNumber = key;

                    if (lineToClean.StartsWith("P_") == false
                            || lineToClean.StartsWith("P_WORK_ON") || lineToClean.StartsWith("P_WORK_OFF") || lineToClean.StartsWith("P_WORK_TYPE") || lineToClean.StartsWith("P_BEAM_ON")
                            || lineToClean.StartsWith("P_BEAM_OFF") || lineToClean.StartsWith("P_HOLE") || lineToClean.StartsWith("P_SLOT") || lineToClean.StartsWith("P_RECT")
                            || lineToClean.StartsWith("P_CIRCLE") || lineToClean.StartsWith("P_KEYHOLE") || lineToClean.StartsWith("P_MARKING") || lineToClean.StartsWith("P_END_MARKING")
                            || lineToClean.StartsWith("P_POLY") || lineToClean.StartsWith("P_SQUARE") || lineToClean.StartsWith("P_APPROACH_MC") || lineToClean.StartsWith("P_RETRACT_MC")
                            || lineToClean.StartsWith("P_G01") || lineToClean.StartsWith("P_G00") || lineToClean.StartsWith("P_G104") || lineToClean.StartsWith("P_G90")
                            || lineToClean.StartsWith("P_G70") || lineToClean.StartsWith("P_G71") || lineToClean.StartsWith("P_G102") || lineToClean.StartsWith("P_G103")
                            )
                    { 
                        var lineCleaned = CleanLine(lineToClean);

                        if (lineCleaned.StartsWith("M30"))
                        {
                            dic.Add(lineNumber, lineCleaned);
                            state.Break();
                            return;
                        }

                        #region SubProgram
                        if (lineCleaned.StartsWith("REPEAT lbl_N"))
                        {
                            //Be Sure to take just the Q command and not comments or other stuff
                            var q = GcodeAxesQuotaRegex.Match(lineCleaned).Value;
                            dic.Add(lineNumber, q);
                            return;
                        }
                        #endregion

                        #region Handle GO
                        if (lineCleaned.StartsWith("GO"))
                        {
                            
                        }

                        #endregion

                        #region Handle Variables
                        if ((lineCleaned.StartsWith("PVAR[") || lineCleaned.StartsWith("LVVAR[")) && lineCleaned.Contains("="))
                        {
                            //lineCleaned = lineCleaned.Replace(" =", "=").Replace("= ", "=").Replace(" = ", "=");
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
                        #endregion

                        #region Handle line that use variables
                        if (lineCleaned.Contains("PVAR") || lineCleaned.Contains("LVVAR"))
                        {
                            var varFounded1 = (Regex.Matches(lineCleaned, variableRegex));
foreach (var item in varFounded1)
                        {
                                if (dicVariables.ContainsKey(item.ToString()))
                                {
                                    lineCleaned = lineCleaned.Replace(item.ToString(), dicVariables[item.ToString()]);
                                }
                                else {
                                    lineCleaned = lineCleaned.Replace(item.ToString(), "0");
                                }
                        }
                        }

                        //var varFounded1 = (Regex.Matches(lineCleaned, variableRegex));

                        
                        #endregion

                        lock (dic)
                        {
                            dic.Add(lineNumber, lineCleaned);
                        }
                    }
                });

                return new SortedDictionary<int, string>(dic);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error " + ex.Message);
            }

            return null;
        }

        public static Dictionary<int, string> ReadProgramFile(string path)
        {
            Dictionary<int, string> dic = new Dictionary<int, string>();

            //var fileTextContent = new string[40000];
            Stopwatch dt = new Stopwatch();
            int counterSkippedAtTheBeginning = 0;
            dt.Start();
            const Int32 BufferSize = 1024;
            using (var fileStream = File.OpenRead(path))
            using (var streamReader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true, BufferSize))
            {

                string line;
                int lineNumberFromReadedFile = 0;
                int indexOfCleanedLines = 0;
                while ((line = streamReader.ReadLine()) != null)
                {
                    if (line == string.Empty || line.StartsWith(";", true, null) || line == "P_G08" || line == "P_G09" || line == "B_TIMER_START" || line == "B_TIMER_STOP")
                    {
                        counterSkippedAtTheBeginning++;
                        lineNumberFromReadedFile++;
                        continue;
                    }

                    line = line.TrimStart().ToUpper();

                    if (
                        line.StartsWith("P_SPEED", true, System.Globalization.CultureInfo.CurrentCulture) ||
                        line.StartsWith("P_HOLE_ACCURACY", true, System.Globalization.CultureInfo.CurrentCulture) ||
                        line.StartsWith("P_G40", true, System.Globalization.CultureInfo.CurrentCulture) ||
                        line.StartsWith("P_G41", true, System.Globalization.CultureInfo.CurrentCulture) ||
                        line.StartsWith("P_G42", true, System.Globalization.CultureInfo.CurrentCulture) ||
                        line.StartsWith("MSG", true, System.Globalization.CultureInfo.CurrentCulture) ||
                        line.StartsWith("P_MATERIAL", true, System.Globalization.CultureInfo.CurrentCulture))
                    {
                        lineNumberFromReadedFile++;
                        continue;
                    }

                    if (line == "M30")
                    {
                        indexOfCleanedLines++;
                        _indexOfM30 = indexOfCleanedLines;
                        _programEndAtLine = lineNumberFromReadedFile;
                        dic[lineNumberFromReadedFile] = line;
                    }
                    else
                    {
                        indexOfCleanedLines++;
                        dic[lineNumberFromReadedFile] = line;
                    }
                    lineNumberFromReadedFile++;
                    // Process line
                }
            }
            dt.Stop();

            Console.WriteLine($"First reading of the file: {dt.ElapsedMilliseconds} ms");
            return dic;
        }

        private IList<IBaseEntity> GetMoves(ProgramContext programContext, SortedDictionary<int, string> lstMoves)
        {
            List<IBaseEntity> moves = new List<IBaseEntity>();

            try
            {
                foreach (var t in lstMoves)
                {
                    if (t.Value == null) continue;
                    var line = t.Value;
                    if (line == null) continue;

                    programContext.SourceLine = t.Key;

                    IBaseEntity entity = null;
                    ParseLine(ref programContext, moves, line, ref entity);

                    
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error " + ex.Message);
            }


            return moves;
        }

        private void ParseLine(ref ProgramContext programContext, List<IBaseEntity> moves, string line, ref IBaseEntity baseEntity)
        {
            if (line.StartsWith("P_"))
            {
                ParseMacro(line, ref programContext, ref baseEntity);
            }

            if (baseEntity != null)
            {
                baseEntity.Is2DProgram = programContext.Is2DProgram;

                if (baseEntity.EntityType == EEntityType.Rect)
                    programContext.LastHeadPosition = (baseEntity as RectMoves).Lines.Last().EndPoint;
                else if (baseEntity.EntityType == EEntityType.Slot)
                    programContext.LastHeadPosition = (baseEntity as SlotMove).Line2.EndPoint;
                else if (baseEntity.EntityType == EEntityType.Poly)
                    programContext.LastHeadPosition = (baseEntity as PolyMoves).Lines.Last().EndPoint;
                else
                    programContext.LastHeadPosition = (baseEntity as Entity).EndPoint;


                programContext.LastEntity = baseEntity as IEntity;

                programContext.UpdateProgramCenterPoint();
                moves.Add(baseEntity);
            }
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

        private IBaseEntity CalculateStartAndEndPoint(ProgramContext programContext, IBaseEntity entity, string[] quotas)
        {
            entity.SourceLine = programContext.SourceLine;

            entity.IsBeamOn = programContext.IsBeamOn;

            entity.LineColor = programContext.ContourLineType;

            IEntity e = (entity as IEntity);

            e.StartPoint = Create3DPoint(programContext, programContext.LastHeadPosition);

            BuildMove(ref entity, quotas, programContext);

            e.EndPoint = Create3DPoint(programContext, programContext.ReferenceMove.EndPoint, e.EndPoint, programContext.IsIncremental ? programContext.LastHeadPosition : new Point3D(0, 0, 0)); //new Point3D(programContext.ReferenceMove.EndPoint.X + entity.EndPoint.X, programContext.ReferenceMove.EndPoint.Y + entity.EndPoint.Y, programContext.ReferenceMove.EndPoint.Z + entity.EndPoint.Z);


            if (entity is ArcMove)
            {
                var arcMove = (entity as ArcMove);
                arcMove.ViaPoint = Create3DPoint(programContext, programContext.ReferenceMove.EndPoint, arcMove.ViaPoint);
                GeoHelper.AddCircularMoveProperties(ref arcMove);
                //If radius is greater then 99000 for sure it is a linear movement.
                if (arcMove.Radius > 99000)
                {
                    entity = new LinearMove { StartPoint = arcMove.StartPoint, EndPoint = arcMove.EndPoint, OriginalLine = arcMove.OriginalLine, SourceLine = arcMove.SourceLine, IsBeamOn = arcMove.IsBeamOn, LineColor = arcMove.LineColor };
                }
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


        private void ParseMacro(string lineP, ref ProgramContext programContext, ref IBaseEntity entity)
        {
            MatchCollection macroParFounded;

            var macroName = lineP.Split('(')[0].Trim().ToUpper();

            var line = lineP.Replace(macroName, "");

            switch (macroName)
            {
                case "P_WORK_TYPE":
                    var macroParFoundedWt = MacroParsRegex.Match(line).Value;
                    programContext.Is2DProgram = macroParFoundedWt == "1";
                    programContext.Is3DProgram = macroParFoundedWt == "2";
                    programContext.IsTubeProgram = macroParFoundedWt == "3";
                    programContext.IsWeldProgram = macroParFoundedWt == "4";
                    break;
                case "P_WORK_ON":
                case "P_MARKING":
                case "P_BEAM_ON":
                    programContext.IsBeamOn = true;

                    if (programContext.IsMarkingProgram == false)
                    {
                        if (lineP.StartsWith("P_WORK_ON"))
                        {
                            var lineType = int.Parse(line.Replace("P_WORK_ON", string.Empty).Replace("(", "").Trim().Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries)[0]);
                            programContext.ContourLineType = (ELineType)lineType;
                        }
                        else if (lineP.StartsWith("P_MARKING"))
                        {
                            programContext.ContourLineType = ELineType.Marking;
                        }
                    }
                    else
                    {
                        programContext.ContourLineType = ELineType.Marking;
                    }
                    break;
                case "P_WORK_OFF":
                case "P_END_MARKING":
                case "BEAM_OFF":
                    programContext.IsBeamOn = false;
                    programContext.ContourLineType = ELineType.Rapid;
                    break;
                case "P_MARKING_PIECE":
                    programContext.IsMarkingProgram = true;
                    break;
                case "P_HOLE":
                    macroParFounded = MacroParsRegex.Matches(line);

                    Point3D pSlotCenter = new Point3D(Converter(macroParFounded[0].Value), Converter(macroParFounded[1].Value), Converter(macroParFounded[2].Value));
                    Point3D pSlotNormal = new Point3D(Converter(macroParFounded[3].Value), Converter(macroParFounded[4].Value), Converter(macroParFounded[5].Value));

                    entity = new ArcMove
                    {
                        IsStroked = true,
                        IsLargeArc = true,
                        SourceLine = programContext.SourceLine,
                        IsBeamOn = programContext.IsBeamOn,
                        LineColor = programContext.ContourLineType,
                        OriginalLine = lineP,
                        Radius = Math.Abs(Converter(macroParFounded[6].Value)),//Aggiunto math.abs perchè... Se si vedessero comportamenti strani, verificare che la direzione della normale segua la regola della mano sinistra rispetto al verso di percorrenza dell'arco.
                        CenterPoint = Create3DPoint(programContext, programContext.ReferenceMove.EndPoint, pSlotCenter),
                        NormalPoint = Create3DPoint(programContext, programContext.ReferenceMove.EndPoint, pSlotNormal),
                    };

                    var holeMacro = entity as ArcMove;
                    GeoHelper.GetMoveFromMacroHole(ref holeMacro);
                    break;
                case "P_SLOT":
                    macroParFounded = MacroParsRegex.Matches(line);

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
                        OriginalLine = lineP,
                        Arc1 = new ArcMove
                        {
                            SourceLine = programContext.SourceLine,
                            IsBeamOn = programContext.IsBeamOn,
                            LineColor = programContext.ContourLineType,
                            OriginalLine = lineP,
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
                            OriginalLine = lineP,
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
                            OriginalLine = lineP,
                        },
                        Line2 = new LinearMove()
                        {
                            SourceLine = programContext.SourceLine,
                            IsBeamOn = programContext.IsBeamOn,
                            LineColor = programContext.ContourLineType,
                            OriginalLine = lineP,
                        }

                    };


                    var slotMove = entity as SlotMove;
                    GeoHelper.GetMovesFromMacroSlot(ref slotMove);
                    slotMove.EndPoint = slotMove.Line2.EndPoint;
                    break;
                case "P_APPROACH_MC":
                case "P_RETRACT_MC":
                    macroParFounded = MacroParsRegex.Matches(line);

                    var X = GetQuotaValue(macroParFounded[0].Value, programContext);
                    var Y = GetQuotaValue(macroParFounded[1].Value, programContext);
                    var Z = GetQuotaValue(macroParFounded[2].Value, programContext);

                    entity = new LinearMove()
                    {
                        StartPoint = programContext.LastHeadPosition,
                        EndPoint = new Point3D(X, Y, Z),
                        SourceLine = programContext.SourceLine,
                        IsBeamOn = false,
                        LineColor = ELineType.Rapid,
                        OriginalLine = lineP
                    };
                    break;

                case "P_POLY":
                    var polyParFounded = MacroParsRegex.Matches(line);

                    var centerPointPolyX = Converter(polyParFounded[0].Value);
                    var centerPointPolyY = Converter(polyParFounded[1].Value);
                    var centerPointPolyZ = Converter(polyParFounded[2].Value);
                    Point3D centerPointPoly = new Point3D(centerPointPolyX, centerPointPolyY, centerPointPolyZ);

                    var verticePointPolyX = Converter(polyParFounded[3].Value);
                    var verticePointPolyY = Converter(polyParFounded[4].Value);
                    var verticePointPolyZ = Converter(polyParFounded[5].Value);
                    Point3D vertexPointPoly = new Point3D(verticePointPolyX, verticePointPolyY, verticePointPolyZ);

                    var normalPointPolyX = Converter(polyParFounded[6].Value);
                    var normalPointPolyY = Converter(polyParFounded[7].Value);
                    var normalPointPolyZ = Converter(polyParFounded[8].Value);
                    Point3D normalPointPoly = new Point3D(normalPointPolyX, normalPointPolyY, normalPointPolyZ);

                    int.TryParse(polyParFounded[9].Value, out int sides);

                    var radiusPoly = (polyParFounded.Count < 11) ? 0.0 : Converter(polyParFounded[10].Value);

                    entity = new PolyMoves()
                    {
                        SourceLine = programContext.SourceLine,
                        IsBeamOn = programContext.IsBeamOn,
                        LineColor = programContext.ContourLineType,
                        OriginalLine = line,
                        Sides = sides,
                        Radius = radiusPoly,
                        VertexPoint = vertexPointPoly,
                        NormalPoint = normalPointPoly,
                        CenterPoint = centerPointPoly
                    };

                    var polyMove = entity as PolyMoves;
                    GeoHelper.GetMovesFromMacroPoly(ref polyMove);
                    break;

                case "P_RECT":

                    var rectParFounded = MacroParsRegex.Matches(line);


                        var centerPointRectX = Converter(rectParFounded[0].Value);
                        var centerPointRectY = Converter(rectParFounded[1].Value);
                        var centerPointRectZ = Converter(rectParFounded[2].Value);
                        Point3D centerPoint = new Point3D(centerPointRectX, centerPointRectY, centerPointRectZ);

                        var vertexPointRectX = Converter(rectParFounded[3].Value);
                        var vertexPointRectY = Converter(rectParFounded[4].Value);
                        var vertexPointRectZ = Converter(rectParFounded[5].Value);
                        Point3D vertexPoint = new Point3D(vertexPointRectX, vertexPointRectY, vertexPointRectZ);

                        var sidePointRextX = Converter(rectParFounded[6].Value);
                        var sidePointRextY = Converter(rectParFounded[7].Value);
                        var sidePointRextZ = Converter(rectParFounded[8].Value);
                        Point3D sidePoint = new Point3D(sidePointRextX, sidePointRextY, sidePointRextZ);

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

                    break;

                case "P_G00":
                case "P_G01":
                    var g01Pars = GetParsFromLine(line);

                    entity = new LinearMove { OriginalLine = lineP };
                    entity = CalculateStartAndEndPoint(programContext, entity, g01Pars);
                    break;
                case "P_G70":
                case "P_G71":
                    _conversionValue = macroName == "P_70" ? 25.4 : 1;
                    programContext.IsInchProgram = macroName == "P_70";
                    break;
                case "P_G92":
                    programContext.IsIncremental = false;
                    break;
                case "P_G90":
                    programContext.IsIncremental = false;
                    break;
                case "P_G91":
                    programContext.IsIncremental = true;
                    break;
                case "P_G93":
                    // macroParFounded = MacroParsRegex.Matches(line);

                    break;
                case "P_G100":
                    break;

                case "P_G102":
                case "P_G103":
                    break;
                case "P_G104":

                    var g104Pars = GetParsFromLine(line);
                    entity = new ArcMove { OriginalLine = lineP };
                    entity = CalculateStartAndEndPoint(programContext, entity, g104Pars);
                    break;


                default:

                    break;
            }

            //if (entity != null)
            //{
            //    entity.Is2DProgram = programContext.Is2DProgram;
            //    programContext.LastEntity = (entity as IEntity);
            //}
        }


        public string[] GetParsFromLine(string line)
        {
            var l = line.Replace("(", "").Replace(")", "").Replace(" ", "");

            var splitted = l.Split(',');
            var d = new List<string>();

            var currentItem = string.Empty;
            var nextItem = string.Empty;

            for (int i = 0; i < splitted.Length; i++)
            {
                currentItem = splitted[i];
                nextItem = i + 1 < splitted.Length ? splitted[i + 1] : "";
                if (currentItem.StartsWith("PAR") && nextItem != "")
                {
                    d.Add(nextItem);
                    i++;
                }
                else if (currentItem.StartsWith("PAR") && nextItem == "")
                {
                    d.Add("0");
                    i++;
                }
                else
                {
                    d.Add(currentItem);
                }

                if (i == splitted.Length - 1) break;

            }

            return d.ToArray();



        }

        public string CleanLine(string lineToClean)
        {
            if (lineToClean.Contains(";"))
            {
                int splitcomment = lineToClean.IndexOf(";");
                lineToClean = lineToClean.Substring(0, splitcomment);
            }
            if (lineToClean.Contains("  ")) lineToClean = lineToClean.Replace("  ", " ");
            if (lineToClean.Contains(") (")) lineToClean = lineToClean.Replace(") (", ")(");
            if (lineToClean.Contains("=")) lineToClean = lineToClean.Replace(" =", "=").Replace("= ", "=");

            return lineToClean.Trim();
        }


        public void BuildMove(ref IBaseEntity entity, string[] quotas,ProgramContext programContext)
        {
            var endPoint = new Point3D(0, 0, 0);
            var viaPoint = new Point3D(0, 0, 0);

            endPoint.X = GetQuotaValue(quotas[0], programContext);
            endPoint.Y = GetQuotaValue(quotas[1], programContext);
            endPoint.Z = programContext.Is2DProgram ? 0.0 : GetQuotaValue(quotas[2], programContext);

            if (entity.EntityType!=EEntityType.Line)
            {
                viaPoint.X = GetQuotaValue(quotas[5], programContext);
                viaPoint.Y = GetQuotaValue(quotas[6], programContext);
                viaPoint.Z = programContext.Is2DProgram ? 0.0 : GetQuotaValue(quotas[7], programContext);
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