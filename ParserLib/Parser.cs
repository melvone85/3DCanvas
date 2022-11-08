using ParserLib.Interfaces;
using ParserLib.Services.Parsers.Interfaces;
using System.Collections.Generic;

namespace ParserLib
{
    public class Parser
    {
        private readonly IParser _parser;
        public Parser(IParser parser)
        {
            _parser = parser;
        }

        public IEnumerable<IEntity> GetMoves() 
        { 
            return _parser.GetMoves();
        }
    }
}
