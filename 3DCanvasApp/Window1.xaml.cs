using Canvas3DViewer.Converters;
using Microsoft.Win32;
using ParserLib;
using ParserLib.Interfaces;
using ParserLib.Models;
using ParserLib.Services.Parsers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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
    public partial class Window1 : Window, INotifyPropertyChanged
    {
        public int dbgCnt = 0;
        List<IBaseEntity> moves;
        public string Filename { get; set; }

        public Point previousCoordinate;
        public Point3D centerRotation = new Point3D(150, 150, 0);
        From3DTo2DPointConversion from3Dto2DPointConversion = null;
        private Brush _originalColor = null;
        private ObservableCollection<System.IO.FileInfo> _cncFiles;

        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<System.IO.FileInfo> CncFiles { get => _cncFiles; set { _cncFiles = value; OnPropertyChanged(); } }
        private ObservableCollection<string> _extFiles;
        public ObservableCollection<string> ExtFiles { get => _extFiles; set { _extFiles = value; OnPropertyChanged(); } }


        public Window1()
        {
            InitializeComponent();
            System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-EN");
            this.DataContext = this;
            from3Dto2DPointConversion = new From3DTo2DPointConversion();
        }

        private async void DrawProgram(string fullName)
        {
            try
            {

                Parser parser = null;

                if (SelectedExtensionFile == "*.iso")
                    parser = new Parser(new ParseIso(fullName));
                else if (SelectedExtensionFile == "*.mpf")
                    parser = new Parser(new ParseMpf(fullName));


                var programContext = (IProgramContext)await parser.GetProgramContext();
                moves = (List<IBaseEntity>)programContext.Moves;

                if (moves == null) return;

                centerRotation = programContext.CenterRotationPoint;

                //int i = 0;
                foreach (var item in moves)
                {
                    //i++;
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

                    }
                }


                InitialTransform();

            }
            catch (Exception ex)
            {

                Console.WriteLine(ex.Message);
            }
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

            Path p = new Path();
            p.StrokeThickness = 1;
            p.Tag = arcMove;
            p.Stroke = GetLineColor(arcMove.LineColor);
            arcMove.GeometryPath = geometry;

            p.Data = geometry;
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ExtFiles = new ObservableCollection<string>();
            ExtFiles.Add("*.mpf"); ExtFiles.Add("*.iso");
            SelectedExtensionFile = Properties.Settings.Default.ExtensionFile;
            LoadProgramsList();

        }

        private string _extFile;
        public string SelectedExtensionFile
        {
            get
            {
                return _extFile;
            }
            set
            {
                Properties.Settings.Default.ExtensionFile = value;
                Properties.Settings.Default.Save();

                _extFile = value;

                LoadProgramsList();
                OnPropertyChanged("SelectedExtensionFile");
            }
        }

        private void LoadProgramsList()
        {
            if (System.IO.Directory.Exists(Properties.Settings.Default.CncProgramsPath))
            {
                System.IO.DirectoryInfo dt = new System.IO.DirectoryInfo(Properties.Settings.Default.CncProgramsPath);

                List<System.IO.FileInfo> lst = new List<System.IO.FileInfo>(dt.GetFiles(SelectedExtensionFile, System.IO.SearchOption.AllDirectories));
                CncFiles = new ObservableCollection<System.IO.FileInfo>(lst.OrderBy(n => n.Name));
                txtIsoPrograms.Text = Properties.Settings.Default.CncProgramsPath;
            }
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
                    return Brushes.Azure;
                case ELineType.CutLine5:
                    return Brushes.Violet;
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

        private string CreateStringPoint(Point3D p, string s)
        {
            return $"{s} X:{Math.Round(p.X, 3)} Y:{Math.Round(p.Y, 3)} Z:{Math.Round(p.Z, 3)}";
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

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (PropertyChanged != null) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

        private void InitialTransform()
        {


            ////////////////////////////////////////////////////////////////////////////////////
            //// ORIENTAMENTO VISUALIZZAZIONE (AL MOMENTO DIREZIONE Z- DALL'ALTO)


            Matrix3D U = Matrix3D.Identity;
            Matrix3D Un = Matrix3D.Identity;
            Point3D cor = new Point3D(centerRotation.Y, centerRotation.X, centerRotation.Z);

            U.RotateAt(new Quaternion(new Vector3D(1, 0, 0), 180), cor);
            Un.RotateAt(new Quaternion(new Vector3D(1, 0, 0), 180), new Point3D(0, 0, 0));


            foreach (var item in moves)
            {
                item.Render(U, Un, false, 1);
            }

            //// shift al centro del canvas

            double xMin = double.PositiveInfinity;
            double xMax = double.NegativeInfinity;
            double yMin = double.PositiveInfinity;
            double yMax = double.NegativeInfinity;



            foreach (var item in moves)
            {
                if (!item.IsBeamOn) { continue; }
                if ((item is ArcMove) == false && item.Is2DProgram == false)
                {
                    continue;
                }

                xMin = Math.Min((item as IEntity).GeometryPath.Bounds.Left, xMin);
                xMax = Math.Max((item as IEntity).GeometryPath.Bounds.Right, xMax);
                yMin = Math.Min((item as IEntity).GeometryPath.Bounds.Bottom, yMin);
                yMax = Math.Max((item as IEntity).GeometryPath.Bounds.Top, yMax);
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


            ////////////////////////////////////////////////////////////////////////////////////
            /// ZOOM FIT CANVAS

            U.SetIdentity();
            Un.SetIdentity();

            double dX = xMax - xMin;
            double dY = yMax - yMin;
            double margin = 50;


            if (dX > dY)
            {
                double Z = (canvas1.ActualWidth - margin) / dX;

                U.ScaleAt(new Vector3D(Z, Z, Z), cor);

                foreach (var item in moves)
                {
                    item.Render(U, Un, false, Z);
                }
            }
            else
            {
                double Z = (canvas1.ActualHeight - margin) / dY;

                U.ScaleAt(new Vector3D(Z, Z, Z), cor);

                foreach (var item in moves)
                {
                    item.Render(U, Un, false, Z);
                }

            }


        }

        private void canvas1_MouseWheel(object sender, MouseWheelEventArgs e)
        {

            double Z = 1;

            if (e.Delta > 0)
            {
                Z = 1.1;
            }

            if (e.Delta < 0)
            {
                Z = 1 / 1.1;
            }


            Point3D cor = new Point3D(centerRotation.Y, centerRotation.X, centerRotation.Z);

            Matrix3D U = Matrix3D.Identity;
            Matrix3D Un = Matrix3D.Identity;
            Point3D PZ = new Point3D(Mouse.GetPosition(canvas1).X, Mouse.GetPosition(canvas1).Y, cor.Z);



            //U.ScaleAt(new Vector3D(Z, Z, Z), cor);

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
            var fi = e.AddedItems[0] as System.IO.FileInfo;
            Filename = fi.FullName;
            DrawProgram(fi.FullName);
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

        private void txtIsoPrograms_TextInput(object sender, TextCompositionEventArgs e)
        {

        }

        private void txtIsoPrograms_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (System.IO.Directory.Exists(txtIsoPrograms.Text))
            {
                Properties.Settings.Default.CncProgramsPath = txtIsoPrograms.Text;
                Properties.Settings.Default.Save();
                LoadProgramsList();
            }
        }
    }
}
