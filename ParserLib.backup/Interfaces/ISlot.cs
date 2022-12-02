using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserLib.Interfaces
{
    internal interface ISlot:IEntity
    {
        IArc Arc1 { get; set; }
        IArc Arc2 { get; set; }
        ILine Line1 { get; set; }
        ILine Line2 { get; set; }
    }
}
