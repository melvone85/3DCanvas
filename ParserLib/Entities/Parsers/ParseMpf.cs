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
        public ParseMpf(string fileName)
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


        private static async Task<List<string>> ReadTextLinesAsync(string path, Encoding encoding)
        {
            var fileTextContent = new List<string>();
            using (var fileStream = new FileStream(path,
            FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 4096, useAsync: true))
            using (var streamReader = new StreamReader(fileStream,
            encoding))
            {
                string text;
                while ((text = await streamReader.ReadLineAsync())
                  != null)
                {
                    fileTextContent.Add(text);
                }
            }
            return fileTextContent;
        }

        Dictionary<string, List<Tuple<int, string>>> _dicSubprograms;


        public async Task<IProgramContext> GetProgramContext()
        {
            var programContext = new ProgramContext();

            programContext.ReferenceMove = new LinearMove();
            programContext.ReferenceMove.EndPoint = new Point3D(0, 0, 0);// programContext.ReferenceMove != null ? new Point3D(programContext.ReferenceMove.EndPoint.X, programContext.ReferenceMove.EndPoint.Y, programContext.ReferenceMove.EndPoint.Z) : new Point3D(0, 0, 0);

            programContext.LastEntity = new LinearMove();
            programContext.LastEntity.EndPoint = new Point3D(0, 0, 0);

            programContext.Moves = await GetMoves(programContext);

            return programContext;
        }


        private async Task<List<Tuple<int, string>>> ReadAndFilterLinesFromFile()
        {
            List<Tuple<int, string>> lstMoves = new List<Tuple<int, string>>();

            await Task.Run(async () =>
            {
                try
                {
                    var lines = await ReadTextLinesAsync(Filename, System.Text.Encoding.Default);

                    //var lines = await WriteSafeReadAllLinesAsync(Filename);

                    _dicSubprograms = new Dictionary<string, List<Tuple<int, string>>>();
                    Dictionary<string, string> dicVariables = new Dictionary<string, string>();
                    Dictionary<int, List<string>> dicOperationsInLabel = new Dictionary<int, List<string>>();
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

                            if (lineToClean.StartsWith(";") || string.IsNullOrEmpty(lineToClean) || string.IsNullOrWhiteSpace(lineToClean))
                            {
                                continue;
                            }
                            if (lineToClean.StartsWith("P_MATERIAL")) { continue; }

                            if (lineToClean.StartsWith("P_G08") || lineToClean.StartsWith("P_G09") || lineToClean.StartsWith("F") || lineToClean.StartsWith("EI") ||
                                lineToClean.StartsWith("P_G40") || lineToClean.StartsWith("GO*") || lineToClean.StartsWith("P_G41") || lineToClean.StartsWith("P_G42"))
                            {
                                continue;
                            }


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
                }
                catch (Exception ex)
                {

                    Console.WriteLine("Error " + ex.Message);
                }


            });

            return lstMoves;
        }


        private async Task<IList<IBaseEntity>> GetMoves(ProgramContext programContext=null)
        {
            List<Tuple<int, string>> lstMoves = await ReadAndFilterLinesFromFile();

            if (programContext == null)
            {
                programContext = new ProgramContext();

                programContext.ReferenceMove = new LinearMove();
                programContext.ReferenceMove.EndPoint = new Point3D(0, 0, 0);// programContext.ReferenceMove != null ? new Point3D(programContext.ReferenceMove.EndPoint.X, programContext.ReferenceMove.EndPoint.Y, programContext.ReferenceMove.EndPoint.Z) : new Point3D(0, 0, 0);

                programContext.LastEntity = new LinearMove();
                programContext.LastEntity.EndPoint = new Point3D(0, 0, 0);
            }

            IList<IBaseEntity> moves = await GetMoves(programContext, lstMoves);

            foreach (var item in macroNotConverted)
            {
                Console.WriteLine(item);
            }

            return moves;
        }

        private async Task<IList<IBaseEntity>> GetMoves(ProgramContext programContext, List<Tuple<int, string>> lstMoves)
        {
            var moves = new List<IBaseEntity>();

            await Task.Run(async () =>
            {
                try
                {
                    foreach (var t in lstMoves)
                    {
                        var line = t.Item2.Trim();
                        if (line.StartsWith("P_G08") || line.StartsWith("P_G09") || line.StartsWith("P_G40") || line.StartsWith("P_G41")) continue;
                        programContext.SourceLine = t.Item1;

                        IEntity entity = null;
                        if (line.StartsWith("P_"))
                        {
                            ParseMacro(line, ref programContext, ref entity);
                        }

                        if (entity != null)
                        {

                            programContext.UpdateProgramCenterPoint();

                            moves.Add(entity);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error " + ex.Message);
                }
            });
            
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

        private IEntity CalculateStartAndEndPoint(ProgramContext programContext, IEntity entity, string[] quotas)
        {
            entity.SourceLine = programContext.SourceLine;
            entity.IsBeamOn = programContext.IsBeamOn;
            entity.LineColor = programContext.ContourLineType;

            entity.StartPoint = Create3DPoint(programContext, programContext.LastEntity.EndPoint);

            BuildMove(programContext, ref entity, quotas);


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

            if (entity != null)
            {
                entity.Is2DProgram = programContext.Is2DProgram;
                programContext.LastEntity = entity;
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


        private void ParseMacro(string lineP, ref ProgramContext programContext, ref IEntity entity)
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
                        StartPoint = programContext.LastEntity.EndPoint,
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

            if (entity != null)
            {
                entity.Is2DProgram = programContext.Is2DProgram;
                programContext.LastEntity = entity;
            }
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
                if (currentItem.StartsWith("Par") && nextItem != "")
                {
                    d.Add(nextItem);
                    i++;
                }
                else if (currentItem.StartsWith("Par") && nextItem == "")
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


        HashSet<string> macroNotConverted = new HashSet<string>();

        public string CleanLine(string lineToClean)
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


        public void BuildMove(ProgramContext programContext, ref IEntity move, string[] quotas)
        {
            var endPoint = new Point3D(0, 0, 0);
            var viaPoint = new Point3D(0, 0, 0);

            endPoint.X = GetQuotaValue(quotas[0], programContext);
            endPoint.Y = GetQuotaValue(quotas[1], programContext);
            endPoint.Z = programContext.Is2DProgram ? 0.0 : GetQuotaValue(quotas[2], programContext);

            if (quotas.Length > 7)
            {
                viaPoint.X = GetQuotaValue(quotas[5], programContext);
                viaPoint.Y = GetQuotaValue(quotas[6], programContext);
                viaPoint.Z = programContext.Is2DProgram ? 0.0 : GetQuotaValue(quotas[7], programContext);
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