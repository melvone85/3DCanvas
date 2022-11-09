using ParserLib.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ParserLib.Services.Parsers.Interfaces
{
    public interface IParser
    {
        string Filename { get; set; }
        Task<List<IEntity>> GetMoves();        

    }
}