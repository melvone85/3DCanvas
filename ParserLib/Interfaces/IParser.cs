namespace ParserLib.Interfaces
{
    public interface IParser
    {
        string Filename { get; set; }

        IProgramContext GetProgramContext();
    }
}