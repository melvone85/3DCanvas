using ParserLib.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ParserLib
{
    public class Parser
    {
        private readonly IParser _parser;
        public Parser(IParser parser)
        {
            _parser = parser;
        }

        public IProgramContext GetProgramContext() 
        { 
            return _parser.GetProgramContext();
        }
    }
}
