using Canvas3DViewer.Commands;
using Canvas3DViewer.Converters;
using Canvas3DViewer.Models;
using ParserLib;
using ParserLib.Interfaces;
using ParserLib.Models;
using ParserLib.Services.Parsers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using static ParserLib.Helpers.TechnoHelper;

namespace Canvas3DViewer.ViewModels
{
    public class CncFilesViewModel : BaseViewModel
    {
        public string Filename { get; set; }
        private string _extFile;
        private ObservableCollection<CncFile> _cncFiles;
        private ObservableCollection<string> _extFiles;

        private CncFile _selectedCncFile;
        private string cncFilesProgramPath;

        public CncFilesViewModel()
        {
            ExtFiles = new ObservableCollection<string>
            {
                "*.mpf",
                "*.iso"
            };
            SelectedExtensionFile = Properties.Settings.Default.ExtensionFile;

            LoadProgramsList();
            CncFilesProgramPath = Properties.Settings.Default.CncProgramsPath;
        }

        public CncFile SelectedCncFile
        {
            get
            {
                return _selectedCncFile;
            }
            set
            {
                _selectedCncFile = value;
                OnPropertyChanged(nameof(SelectedCncFile));

                Filename = _selectedCncFile.FullPath;
            }
        }

        public ObservableCollection<CncFile> CncFiles
        {
            get => _cncFiles;
            set { _cncFiles = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> ExtFiles
        {
            get => _extFiles;
            set { _extFiles = value; OnPropertyChanged(); }
        }

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

                OnPropertyChanged();
            }
        }

        public void LoadProgramsList()
        {
            if (System.IO.Directory.Exists(Properties.Settings.Default.CncProgramsPath))
            {
                System.IO.DirectoryInfo dt = new System.IO.DirectoryInfo(Properties.Settings.Default.CncProgramsPath);

                List<System.IO.FileInfo> lst = new List<System.IO.FileInfo>(dt.GetFiles(SelectedExtensionFile, System.IO.SearchOption.AllDirectories));

                var lstOrdered = lst.OrderBy(n => n.Name);

                CncFiles = new ObservableCollection<CncFile>();

                foreach (var item in lstOrdered)
                {
                    CncFiles.Add(new CncFile() { Name = item.Name, FullPath = item.FullName, LastWriteTime = item.LastWriteTime });
                }

                Parallel.ForEach(CncFiles, async (file) =>
                {
                    var t = await GetMaterialFromFile(file.FullPath);
                    file.Material = t.Item1;
                    file.Thickness = t.Item2;
                });
                OnPropertyChanged(nameof(CncFiles));

                //txtIsoPrograms.Text = Properties.Settings.Default.CncProgramsPath;
            }
        }

        public string CncFilesProgramPath
        {
            get => cncFilesProgramPath; 
            set
            {
                if (System.IO.Directory.Exists(value) && Properties.Settings.Default.CncProgramsPath != value)
                {
                    //CncFilesProgramPath = txtIsoPrograms.Text;
                    Properties.Settings.Default.CncProgramsPath = value;
                    Properties.Settings.Default.Save();
                    //(this.DataContext as CncFilesViewModel).LoadProgramsList();
                }
                cncFilesProgramPath = value;
                OnPropertyChanged(nameof(CncFilesProgramPath));
            }
        }

        private async Task<Tuple<string, double>> GetMaterialFromFile(string fileName)
        {
            using (var fileStream = System.IO.File.OpenRead(fileName))
            using (var streamReader = new System.IO.StreamReader(fileStream, Encoding.UTF8, true, 1024))
            {
                string line;
                while ((line = await streamReader.ReadLineAsync()) != null)
                {
                    try
                    {
                        var lineU = line.ToUpper().Trim();
                        if (lineU.StartsWith("<MATERIAL"))
                        {
                            string m = GetAttributesValue(lineU, "NAME");
                            string t = GetAttributesValue(lineU, "THICKNESS");
                            streamReader.Close();
                            fileStream.Close();
                            return new Tuple<string, double>(m, double.Parse(t));
                        }
                        else if (lineU.StartsWith("P_MATERIAL"))
                        {
                            var lineSplitted = lineU.Split(',');
                            var m = lineSplitted[0].Replace("\"", "").Trim();
                            var t = lineSplitted[1].Trim();
                            streamReader.Close();
                            fileStream.Close();
                            return new Tuple<string, double>(m, double.Parse(t));
                        }
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                    finally
                    {


                    }

                }
            }
            return new Tuple<string, double>("", 0.0);
        }

        private static string GetAttributesValue(string line, string name)
        {
            var chrIndex1 = 0;
            var chrIndex2 = 0;

            try
            {
                //Search for name
                chrIndex1 = line.IndexOf(name);
                if (chrIndex1 != -1)
                {
                    //Attributes Found
                    chrIndex1 = line.IndexOf("\"", chrIndex1 + name.Length);
                    chrIndex2 = line.IndexOf("\"", chrIndex1 + 1);
                    return line.Substring(chrIndex1 + 1, chrIndex2 - chrIndex1 - 1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return null;
        }

    }
}
