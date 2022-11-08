using ParserLib.Interfaces;
using System.Collections.Generic;

namespace ParserLib.Services.Parsers.Interfaces
{
    public interface IParser
    {
        string Filename { get; set; }
        List<IEntity> GetMoves();        
        List<IEntity> ParseMacro(string line);
        IEntity ParseLine(string line);

    }
}