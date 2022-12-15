using ParserLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserLib.Interfaces
{
    internal interface IKeyhole : IEntity
    {
        CircularEntity Arc1 { get; set; }
        CircularEntity Arc2 { get; set; }
        Entity Line1 { get; set; }
        Entity Line2 { get; set; }
    }
}
