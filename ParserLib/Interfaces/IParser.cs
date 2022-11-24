using ParserLib.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ParserLib.Interfaces
{
    public interface IParser
    {
        string Filename { get; set; }
        Task<IProgramContext> GetProgramContext();
    }
}