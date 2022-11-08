using ParserLib;
using ParserLib.Interfaces;
using ParserLib.Models;
using ParserLib.Services.Parsers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace CanvasTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window,INotifyPropertyChanged
    {
        public int dbgCnt = 0;
        List<IEntity> moves;

        public Point previousCoordinate;

        public Point3D centerRotation = new Point3D(150, 150, 0);
        From3Dto2DPointConversion from3Dto2DPointConversion = new From3Dto2DPointConversion();
        private Brush _originalColor = null;
        private ObservableCollection<System.IO.FileInfo> isoFiles;

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        private void MouseClickEntity(object sender, MouseButtonEventArgs e)
        {
            var p = ((Path)sender);

            txtN1.Text = "";
            txtN2.Text = "";
            txtN3.Text = "";
            txtN4.Text = "";
            txtN5.Text = "";
            txtN6.Text = "";
            txtN7.Text = "";
            txtN8.Text = "";
            if (p.Tag != null && p.Tag is IEntity)
            {
                var entity = (IEntity)p.Tag;
                txtN1.Text = $"Entity ({entity.EntityType})";
                txtN2.Text = $"Start Point({entity.StartPoint})";
                txtN3.Text = $"End Point({entity.EndPoint})";
                txtLine.Text = (p.Tag as IEntity).OriginalLine.ToString();
                txtLineNumber.Text = (p.Tag as IEntity).SourceLine.ToString();
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

        protected void OnNotifyPropertyChanged([CallerMemberName] string propertyName = "") 
        {
            if (PropertyChanged != null) PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(propertyName)); 
        }

        public ObservableCollection<System.IO.FileInfo> IsoFiles { get => isoFiles; set { isoFiles = value; OnNotifyPropertyChanged(); } }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            System.IO.DirectoryInfo dt = new System.IO.DirectoryInfo(@"C:\ncprog\PROGRAMMI iso-siemens\");
            IsoFiles = new ObservableCollection<System.IO.FileInfo>(dt.GetFiles(@"*.iso",System.IO.SearchOption.TopDirectoryOnly));

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
            arcMove.Bounds = geometry;

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

            p.Data = geometry;

            linearMove.Bounds = geometry;

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
                p.Opacity = 0.1;
            }
            canvas1.Children.Add(p);
        }



        private SolidColorBrush GetLineColor(ParserLib.Globals.Globals.ELineType lineColor)
        {
            switch (lineColor)
            {
                case ParserLib.Globals.Globals.ELineType.CutLine1:
                    return Brushes.Green;
                    break;
                case ParserLib.Globals.Globals.ELineType.CutLine2:
                    return Brushes.RoyalBlue;
                    break;
                case ParserLib.Globals.Globals.ELineType.CutLine3:
                    return Brushes.Red;
                    break;
                case ParserLib.Globals.Globals.ELineType.CutLine4:
                    return Brushes.Azure;
                    break;
                case ParserLib.Globals.Globals.ELineType.CutLine5:
                    return Brushes.Violet;
                    break;
                case ParserLib.Globals.Globals.ELineType.Marking:
                    return Brushes.Yellow;
                    break;
                case ParserLib.Globals.Globals.ELineType.Microwelding:
                    break;
                case ParserLib.Globals.Globals.ELineType.Rapid:
                    return Brushes.White;
                    break;
                default:
                    return Brushes.White;
                    break;
            }
            return Brushes.White;
        }

        private void canvas1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            previousCoordinate = Mouse.GetPosition(canvas1);
        }

        private void canvas1_MouseMove(object sender, MouseEventArgs e)
        {
            lbl.Text = " X : " + Mouse.GetPosition(canvas1).X.ToString() + " Y : " + Mouse.GetPosition(canvas1).Y.ToString();

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

                    U.RotateAt(Q, new Point3D(centerRotation.Y, centerRotation.X, centerRotation.Z));
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

            //U.SetIdentity();
            //Un.SetIdentity();

            //U.RotateAt(new Quaternion(new Vector3D(1, 0, 1), -45), cor);
            //Un.RotateAt(new Quaternion(new Vector3D(1, 0, 1), -45), new Point3D(0, 0, 0));

            //foreach (var item in moves)
            //{
            //    item.Render(U, Un, false, 1);
            //}

            ////////////////////////////////////////////////////////////////////////////////////
            //// shift al centro del canvas

            double xMin = double.PositiveInfinity;
            double xMax = double.NegativeInfinity;
            double yMin = double.PositiveInfinity;
            double yMax = double.NegativeInfinity;



            foreach (var item in moves)
            {
                if (!item.IsBeamOn) { continue; }
                if (!(item is ArcMove)) { continue; }


                xMin = Math.Min(item.Bounds.Bounds.Left, xMin);
                xMax = Math.Max(item.Bounds.Bounds.Right, xMax);
                yMin = Math.Min(item.Bounds.Bounds.Bottom, yMin);
                yMax = Math.Max(item.Bounds.Bounds.Top, yMax);

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
                double Z = (canvas1.ActualWidth- margin) / dX;

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
            var fi = e.AddedItems[0] as System.IO.FileInfo;
            DrawProgram(fi.FullName);
        }

        private void DrawProgram(string fullName)
        {
            Parser parser = new Parser(new ParseIso(fullName));
            moves = (List<IEntity>)parser.GetMoves();


            var xMin = double.PositiveInfinity;
            var xMax = double.NegativeInfinity;
            var yMin = double.PositiveInfinity;
            var yMax = double.NegativeInfinity;
            var zMin = double.PositiveInfinity;
            var zMax = double.NegativeInfinity;

            foreach (IEntity item in moves)
            {
                if (item.IsBeamOn == false) continue;

                if (item.StartPoint.X < xMin) xMin = item.StartPoint.X;
                if (item.EndPoint.X < xMin) xMin = item.EndPoint.X;
                if (item is IArc)
                    if ((item as IArc).ViaPoint.X < xMin) xMin = (item as IArc).ViaPoint.X;

                if (item.StartPoint.X > xMax) xMax = item.StartPoint.X;
                if (item.EndPoint.X > xMax) xMax = item.EndPoint.X;
                if (item is IArc)
                    if ((item as IArc).ViaPoint.X > xMax) xMax = (item as IArc).ViaPoint.X;

                if (item.StartPoint.Y < yMin) yMin = item.StartPoint.Y;
                if (item.EndPoint.Y < yMin) yMin = item.EndPoint.Y;
                if (item is IArc)
                    if ((item as IArc).ViaPoint.Y < yMin) yMin = (item as IArc).ViaPoint.Y;

                if (item.StartPoint.Y > yMax) yMax = item.StartPoint.Y;
                if (item.EndPoint.Y > yMax) yMax = item.EndPoint.Y;
                if (item is IArc)
                    if ((item as IArc).ViaPoint.Y > yMax) yMax = (item as IArc).ViaPoint.Y;


                if (item.StartPoint.Z < zMin) zMin = item.StartPoint.Z;
                if (item.EndPoint.Z < zMin) zMin = item.EndPoint.Z;
                if (item is IArc)
                    if ((item as IArc).ViaPoint.Z < zMin) xMin = (item as IArc).ViaPoint.Z;

                if (item.StartPoint.Z > zMax) zMax = item.StartPoint.Z;
                if (item.EndPoint.Z > zMax) zMax = item.EndPoint.Z;
                if (item is IArc)
                    if ((item as IArc).ViaPoint.Z > zMax) zMax = (item as IArc).ViaPoint.Z;

            }

            centerRotation = new Point3D((yMin + yMax) / 2, (xMin + xMax) / 2, (zMin + zMax) / 2);

            foreach (var item in moves)
            {
                if (item.EntityType == ParserLib.Globals.Globals.EEntityType.Line && item.IsBeamOn)
                {
                    DrawLine(item as LinearMove);
                }
                else if (item.EntityType == ParserLib.Globals.Globals.EEntityType.Line && item.IsBeamOn == false && moves.Count < 2000)
                {
                    DrawLine(item as LinearMove, true);
                }
                else if (item.EntityType == ParserLib.Globals.Globals.EEntityType.Arc && item.IsBeamOn)
                {
                    DrawArc((ArcMove)item);
                }
            }


            InitialTransform();
        }
    }

    public class From3Dto2DPointConversion : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Point3D p3D = (Point3D)value;
            return new Point(p3D.X, p3D.Y);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
