using Canvas3DViewer.Converters;
using Canvas3DViewer.Models;
using Canvas3DViewer.ViewModels;
using ParserLib;
using ParserLib.Interfaces;
using ParserLib.Models;
using ParserLib.Services.Parsers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

using static ParserLib.Helpers.TechnoHelper;

namespace Canvas3DViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        public int dbgCnt = 0;
        List<IBaseEntity> moves;
        public string Filename { get; set; }

        private Point previousCoordinate;
        private Point3D centerRotation = new Point3D(150, 150, 0);
        private From3DTo2DPointConversion from3Dto2DPointConversion = null;
        private Brush _originalColor = null;

        public Window1()
        {
            InitializeComponent();
            System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-EN");
            this.DataContext = new CncFilesViewModel();
            from3Dto2DPointConversion = new From3DTo2DPointConversion();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //this.DataContext = new CncFilesViewModel();
        }

        private ObjectCache cache = MemoryCache.Default;
        public List<T> GetCachedDataList<T>(string key)
        {
            List<T> result = null;
            if (cache.Contains(key))
                result = cache[key] as List<T>;

            foreach (var item in cache)
            {
                Console.WriteLine("cache object key-value: " + item.Key + "-" + item.Value);
            }



            return result;
        }


        private void DrawProgram(string fullName)
        {
            List<ParseIso> data = GetCachedDataList<ParseIso>("ParseIso") as List<ParseIso>;



            Stopwatch st = new Stopwatch();
            st.Start();

            try
            {
                Parser parser = null;

                if ((this.DataContext as CncFilesViewModel).SelectedExtensionFile == "*.iso")
                    parser = new Parser(new ParseIso(fullName));
                else if ((this.DataContext as CncFilesViewModel).SelectedExtensionFile == "*.mpf")
                    parser = new Parser(new ParseMpf(fullName));

                var programContext = parser.GetProgramContext();
                moves = (List<IBaseEntity>)programContext.Moves;

                if (moves == null) return;

                centerRotation = programContext.CenterRotationPoint;

                foreach (var item in moves)
                {
                    if (item.IsBeamOn == false)
                    {
                        if (item.EntityType == EEntityType.Line && moves.Count < 2000)
                        {
                            DrawLine(item as LinearMove, true);
                        }
                    }
                    else
                    {
                        if (item.EntityType == EEntityType.Line)
                        {
                            DrawLine(item as LinearMove);
                        }
                        else if (item.EntityType == EEntityType.Arc)
                        {
                            DrawArc(item as ArcMove);
                        }
                        else if (item.EntityType == EEntityType.Slot)
                        {
                            var slot = item as SlotMove;

                            DrawArc(slot.Arc1 as ArcMove);
                            DrawLine(slot.Line1 as LinearMove);
                            DrawArc(slot.Arc2 as ArcMove);
                            DrawLine(slot.Line2 as LinearMove);
                        }
                        else if (item.EntityType == EEntityType.Poly)
                        {
                            var poly = item as PolyMoves;

                            foreach (var l in poly.Lines)
                            {
                                DrawLine(l as LinearMove);
                            }
                        }
                        else if (item.EntityType == EEntityType.Rect)
                        {
                            var rect = item as RectMoves;

                            foreach (var l in rect.Lines)
                            {
                                DrawLine(l as LinearMove);
                            }
                        }

                    }
                }

                InitialTransform();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            var ms = st.ElapsedMilliseconds;
            Console.WriteLine($"Program: {System.IO.Path.GetFileName(fullName)} is completed in {ms}ms");
        }

        private void DrawArc(ArcMove arcMove)
        {
            PathFigure pf = new PathFigure();
            ArcSegment ls = new ArcSegment();

            BindingBase sourceBinding = new Binding { Source = arcMove, Path = new PropertyPath("StartPoint"), Converter = from3Dto2DPointConversion };
            BindingOperations.SetBinding(pf, PathFigure.StartPointProperty, sourceBinding);

            pf.Segments.Add(ls);

            BindingBase destinationBindingPoint = new Binding { Source = arcMove, Path = new PropertyPath("EndPoint"), Converter = from3Dto2DPointConversion };
            BindingBase destinationBindingSize = new Binding { Source = arcMove, Path = new PropertyPath("ArcSize") };
            BindingBase destinationBindingRotationAngle = new Binding { Source = arcMove, Path = new PropertyPath("RotationAngle") };
            BindingBase destinationBindingIsLargeArc = new Binding { Source = arcMove, Path = new PropertyPath("IsLargeArc") };
            BindingBase destinationBindingIsStroked = new Binding { Source = arcMove, Path = new PropertyPath("IsStroked") };
            BindingBase destinationBindingSweepDirection = new Binding { Source = arcMove, Path = new PropertyPath("ArcSweepDirection") };

            BindingOperations.SetBinding(ls, ArcSegment.PointProperty, destinationBindingPoint);
            BindingOperations.SetBinding(ls, ArcSegment.SizeProperty, destinationBindingSize);
            BindingOperations.SetBinding(ls, ArcSegment.RotationAngleProperty, destinationBindingRotationAngle);
            BindingOperations.SetBinding(ls, ArcSegment.IsLargeArcProperty, destinationBindingIsLargeArc);
            BindingOperations.SetBinding(ls, ArcSegment.IsStrokedProperty, destinationBindingIsStroked);
            BindingOperations.SetBinding(ls, ArcSegment.SweepDirectionProperty, destinationBindingSweepDirection);

            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(pf);
            arcMove.GeometryPath = geometry;

            Path p = new Path
            {
                StrokeThickness = 1,
                Tag = arcMove,
                Stroke = GetLineColor(arcMove.LineColor),
                Data = geometry
            };

            p.MouseDown += MouseClickEntity;
            p.MouseEnter += MouseEnterEntity;
            p.MouseLeave += MouseLeaveEntity;

            canvas1.Children.Add(p);
        }

        private void DrawLine(LinearMove linearMove, bool isRapid = false)
        {
            PathFigure pf = new PathFigure();
            LineSegment ls = new LineSegment();

            BindingBase sourceBinding = new Binding { Source = linearMove, Path = new PropertyPath("StartPoint"), Converter = from3Dto2DPointConversion };
            BindingOperations.SetBinding(pf, PathFigure.StartPointProperty, sourceBinding);

            pf.Segments.Add(ls);

            BindingBase destinationBinding = new Binding { Source = linearMove, Path = new PropertyPath("EndPoint"), Converter = from3Dto2DPointConversion };
            BindingOperations.SetBinding(ls, LineSegment.PointProperty, destinationBinding);

            PathGeometry geometry = new PathGeometry();

            geometry.Figures.Add(pf);


            Path p = new Path();
            p.StrokeThickness = 1;

            if (isRapid) p.StrokeDashArray = new DoubleCollection() { 4, 2 };
            p.Tag = linearMove;
            linearMove.GeometryPath = geometry;
            p.Data = geometry;

            if (isRapid == false)
            {
                p.StrokeThickness = 1;

                p.Stroke = GetLineColor(linearMove.LineColor);


                p.MouseDown += MouseClickEntity;
                p.MouseEnter += MouseEnterEntity;
                p.MouseLeave += MouseLeaveEntity;
            }
            else
            {
                p.Stroke = Brushes.DarkGray;
                p.Opacity = 0.2;
            }
            canvas1.Children.Add(p);
        }

        private string CreateStringPoint(Point3D p, string s)
        {
            return $"{s} X:{Math.Round(p.X, 3)} Y:{Math.Round(p.Y, 3)} Z:{Math.Round(p.Z, 3)}";
        }

        //private void InitialTransform()
        //{


        //    ////////////////////////////////////////////////////////////////////////////////////
        //    //// ORIENTAMENTO VISUALIZZAZIONE (AL MOMENTO DIREZIONE Z- DALL'ALTO)


        //    Matrix3D U = Matrix3D.Identity;
        //    Matrix3D Un = Matrix3D.Identity;
        //    Point3D cor = new Point3D(centerRotation.Y, centerRotation.X, centerRotation.Z);

        //    U.RotateAt(new Quaternion(new Vector3D(1, 0, 0), 180), cor);
        //    Un.RotateAt(new Quaternion(new Vector3D(1, 0, 0), 180), new Point3D(0, 0, 0));


        //    foreach (var item in moves)
        //    {
        //        item.Render(U, Un, false, 1);
        //    }

        //    //// shift al centro del canvas

        //    double xMin = double.PositiveInfinity;
        //    double xMax = double.NegativeInfinity;
        //    double yMin = double.PositiveInfinity;
        //    double yMax = double.NegativeInfinity;



        //    foreach (var item in moves)
        //    {
        //        var entity = item as IEntity;
        //        if (!item.IsBeamOn) { continue; }
        //        if ((item is ArcMove) == false && item.Is2DProgram == false)
        //        {
        //            continue;
        //        }

        //        xMin = Math.Min(entity.BoundingBox.Item1, xMin);
        //        xMax = Math.Max(entity.BoundingBox.Item2, xMax);
        //        yMin = Math.Min(entity.BoundingBox.Item3, yMin);
        //        yMax = Math.Max(entity.BoundingBox.Item4, yMax);
        //    }

        //    double xMed = (xMax + xMin) / 2;
        //    double yMed = (yMax + yMin) / 2;

        //    double yMedCanvas = canvas1.ActualHeight / 2;
        //    double xMedCanvas = canvas1.ActualWidth / 2;

        //    U.SetIdentity();
        //    Un.SetIdentity();

        //    U.OffsetX = xMedCanvas - xMed;
        //    U.OffsetY = yMedCanvas - yMed;

        //    cor = U.Transform(cor);

        //    centerRotation.X = yMedCanvas;
        //    centerRotation.Y = xMedCanvas;
        //    centerRotation.Z = cor.Z;

        //    cor = new Point3D(centerRotation.Y, centerRotation.X, centerRotation.Z);


        //    foreach (var item in moves)
        //    {
        //        item.Render(U, Un, false, 1);
        //    }


        //    ////////////////////////////////////////////////////////////////////////////////////
        //    /// ZOOM FIT CANVAS

        //    U.SetIdentity();
        //    Un.SetIdentity();

        //    double dX = xMax - xMin;
        //    double dY = yMax - yMin;
        //    double margin = 50;


        //    //if (dX > dY)
        //    //{
        //    //    double Z = (canvas1.ActualWidth - margin) / dX;

        //    //    U.ScaleAt(new Vector3D(Z, Z, Z), cor);

        //    //    foreach (var item in moves)
        //    //    {
        //    //        item.Render(U, Un, false, Z);
        //    //    }
        //    //}
        //    //else
        //    //{
        //    //    double Z = (canvas1.ActualHeight - margin) / dY;

        //    //    U.ScaleAt(new Vector3D(Z, Z, Z), cor);

        //    //    foreach (var item in moves)
        //    //    {
        //    //        item.Render(U, Un, false, Z);
        //    //    }

        //    //}

        //    double newZ = (dX > dY) ? (canvas1.ActualWidth - margin) / dX : (canvas1.ActualHeight - margin) / dY;

        //    //double Z = (canvas1.ActualWidth - margin) / divisionFactorZ;

        //    U.ScaleAt(new Vector3D(newZ, newZ, newZ), cor);

        //    foreach (var item in moves)
        //    {
        //        item.Render(U, Un, false, newZ);
        //    }


        //}

        private void InitialTransform()
        {
            #region Top View of the drawing
            Matrix3D U = Matrix3D.Identity;
            Matrix3D Un = Matrix3D.Identity;
            Point3D cor = new Point3D(centerRotation.Y, centerRotation.X, centerRotation.Z);

            U.RotateAt(new Quaternion(new Vector3D(1, 0, 0), 180), cor);
            Un.RotateAt(new Quaternion(new Vector3D(1, 0, 0), 180), new Point3D(0, 0, 0));

            foreach (var item in moves)
            {
                item.Render(U, Un, false, 1);
            }
            #endregion 

            #region Shift Drawing in the center of the canvas

            double xMin = double.PositiveInfinity;
            double xMax = double.NegativeInfinity;
            double yMin = double.PositiveInfinity;
            double yMax = double.NegativeInfinity;

            foreach (var item in moves)
            {
                if (!item.IsBeamOn) { continue; }

                var entity = (item as IEntity);
                if (double.IsInfinity(entity.BoundingBox.Item1) || double.IsInfinity(entity.BoundingBox.Item2) || double.IsInfinity(entity.BoundingBox.Item3) || double.IsInfinity(entity.BoundingBox.Item4))
                    continue;
                //if ((item is ArcMove) == false && item.Is2DProgram == false)
                //{
                //    continue;
                //}

                //if (entity.EntityType == EEntityType.Arc)
                //{
                //    continue;
                //}

                xMin = Math.Min(entity.BoundingBox.Item1, xMin);
                xMax = Math.Max(entity.BoundingBox.Item2, xMax);
                yMin = Math.Min(entity.BoundingBox.Item3, yMin);
                yMax = Math.Max(entity.BoundingBox.Item4, yMax);
            }


            double xMed = (xMax + xMin) / 2;
            double yMed = (yMax + yMin) / 2;

            double yMedCanvas = canvas1.ActualHeight / 2;
            double xMedCanvas = canvas1.ActualWidth / 2;

            U.SetIdentity();
            Un.SetIdentity();

            U.OffsetX = xMedCanvas - xMed;
            U.OffsetY = yMedCanvas - yMed;

            cor = U.Transform(cor);

            centerRotation.X = yMedCanvas;
            centerRotation.Y = xMedCanvas;
            centerRotation.Z = cor.Z;

            cor = new Point3D(centerRotation.Y, centerRotation.X, centerRotation.Z);

            foreach (var item in moves)
            {
                item.Render(U, Un, false, 1);
            }
            #endregion

            #region Zoom drawing into Canvas
            U.SetIdentity();
            Un.SetIdentity();

            double dX = xMax - xMin;
            double dY = yMax - yMin;
            double margin = 50;

            double newZ = (dX > dY) ? (canvas1.ActualWidth - margin) / dX : (canvas1.ActualHeight - margin) / dY;

            U.ScaleAt(new Vector3D(newZ, newZ, newZ), cor);

            foreach (var item in moves)
            {
                item.Render(U, Un, false, newZ);
            }
            #endregion

        }

        private SolidColorBrush GetLineColor(ELineType lineColor)
        {
            switch (lineColor)
            {
                case ELineType.CutLine1:
                    return Brushes.Green;
                case ELineType.CutLine2:
                    return Brushes.RoyalBlue;
                case ELineType.CutLine3:
                    return Brushes.Red;
                case ELineType.CutLine4:
                    return Brushes.Violet;
                case ELineType.CutLine5:
                    return Brushes.Aqua;
                case ELineType.Marking:
                    return Brushes.Yellow;
                case ELineType.Microwelding:
                case ELineType.Rapid:
                    return Brushes.Gray;
                default:
                    return Brushes.White;
            }

        }

        private void MouseClickEntity(object sender, MouseButtonEventArgs e)
        {
            var p = ((Path)sender);

            if (p.Tag != null && p.Tag is IEntity)
            {
                var entity = (p.Tag as IEntity);


                txtLine.Text = entity.OriginalLine.ToString();
                txtLineNumber.Text = $"Source line: {entity.SourceLine.ToString()}";

                txtSP.Text = CreateStringPoint(entity.StartPoint, "Start p:");

                if (entity is ArcMove)
                {
                    txtVP.Visibility = Visibility.Visible;
                    txtVP.Text = CreateStringPoint((entity as ArcMove).ViaPoint, "Via p:");
                }
                else
                {
                    txtVP.Visibility = Visibility.Collapsed;
                }
                txtEP.Text = CreateStringPoint(entity.EndPoint, "End p:");



            }
        }

        private void MouseEnterEntity(object sender, MouseEventArgs e)
        {
            _originalColor = ((Path)sender).Stroke;
            ((Path)sender).Stroke = Brushes.Orange;
            ((Path)sender).StrokeThickness = 5;
        }

        private void MouseLeaveEntity(object sender, MouseEventArgs e)
        {
            ((Path)sender).Stroke = _originalColor;
            ((Path)sender).StrokeThickness = 1;
        }

        private void canvas1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            previousCoordinate = Mouse.GetPosition(canvas1);
        }

        private void canvas1_MouseMove(object sender, MouseEventArgs e)
        {
            lbl.Text = $"X:{Mouse.GetPosition(canvas1).X.ToString("N0")} Y:{Mouse.GetPosition(canvas1).Y.ToString("N0")}";

            if (moves == null) return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {

                double rotSpeed = 200;

                if (Keyboard.IsKeyDown(Key.LeftShift)) { rotSpeed = rotSpeed / 100; }


                double vX = (Mouse.GetPosition(canvas1).Y - previousCoordinate.Y) / canvas1.ActualHeight;
                double vY = (Mouse.GetPosition(canvas1).X - previousCoordinate.X) / canvas1.ActualWidth;

                Matrix3D U = Matrix3D.Identity;
                Matrix3D Un = Matrix3D.Identity;

                double qRotAngle = Math.Pow(Math.Pow(vX, 2) + Math.Pow(vY, 2), 0.5) * rotSpeed;

                Vector3D vQ = new Vector3D(vX, -vY, 0);

                if (vQ.Length != 0)
                {

                    Quaternion Q = new Quaternion(vQ, qRotAngle);

                    U.RotateAt(Q, centerRotation);
                    Un.RotateAt(Q, new Point3D(0, 0, 0));

                    foreach (var item in moves)
                    {
                        item.Render(U, Un, true, 1);
                    }
                }
                previousCoordinate = Mouse.GetPosition(canvas1);
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                Point3D cor = new Point3D(centerRotation.Y, centerRotation.X, centerRotation.Z);

                double vY = (Mouse.GetPosition(canvas1).Y - previousCoordinate.Y);
                double vX = (Mouse.GetPosition(canvas1).X - previousCoordinate.X);

                Matrix3D U = Matrix3D.Identity;
                Matrix3D Un = Matrix3D.Identity;

                U.OffsetX = vX;
                U.OffsetY = vY;

                cor = U.Transform(cor);

                centerRotation.X = cor.Y;
                centerRotation.Y = cor.X;
                centerRotation.Z = cor.Z;

                foreach (var item in moves)
                {
                    item.Render(U, Un, false, 1);
                }

                previousCoordinate = Mouse.GetPosition(canvas1);
            }
        }

        private void canvas1_MouseWheel(object sender, MouseWheelEventArgs e)
        {

            double Z = 1;

            if (e.Delta > 0)
            {
                Z = 1.1;
            }
            else if (e.Delta < 0)
            {
                Z = 1 / 1.1;
            }

            Point3D cor = new Point3D(centerRotation.Y, centerRotation.X, centerRotation.Z);

            Matrix3D U = Matrix3D.Identity;
            Matrix3D Un = Matrix3D.Identity;
            Point3D PZ = new Point3D(Mouse.GetPosition(canvas1).X, Mouse.GetPosition(canvas1).Y, cor.Z);

            U.ScaleAt(new Vector3D(Z, Z, Z), PZ);

            cor = U.Transform(cor);
            centerRotation.X = cor.Y;
            centerRotation.Y = cor.X;
            centerRotation.Z = cor.Z;

            foreach (var item in moves)
            {
                item.Render(U, Un, false, Z);
            }
        }

        private void ListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            canvas1.Children.Clear();
            if (e.AddedItems.Count == 0) return;
            var fi = e.AddedItems[0] as CncFile;
            Filename = fi.FullPath;
            DrawProgram(Filename);
        }

        private void txtLine_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (Filename != string.Empty && txtLine.Text != "")
                {
                    var nppDir = @"C:\Program Files\Notepad++";
                    var nppExePath = System.IO.Path.Combine(nppDir, "Notepad++.exe");

                    var nppReadmePath = System.IO.Path.Combine(nppDir, Filename);
                    var line = int.Parse(txtLineNumber.Text.Replace("Source line: ", ""));
                    var sb = new StringBuilder();
                    sb.AppendFormat("\"{0}\" -n{1}", nppReadmePath, line);
                    Process.Start(nppExePath, sb.ToString());
                }

            }
        }

    }
}
