using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ParserLib.Models;

namespace ParserLib.Interfaces
{
    public interface IHole : IEntity
    {

        CircularEntity Circle { get; set; } 
        Entity LeadIn { get; set; }

        double Radius { get; set; }


        
        
    }
}
