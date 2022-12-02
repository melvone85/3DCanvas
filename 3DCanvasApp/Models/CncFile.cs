using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Canvas3DViewer.Models
{
    public class CncFile
    {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public DateTime LastWriteTime { get => lastWriteTime; set => lastWriteTime = value; }

            private string _material;

            public string Material
            {
                get
                {
                    return _material;
                }
                set
                {
                    _material = value;
                }
            }

            private double _thickness;
            private DateTime lastWriteTime;

            public double Thickness
            {
                get
                {
                    return _thickness;
                }
                set
                {
                    _thickness = value;
                }
            }
        
    }
}
