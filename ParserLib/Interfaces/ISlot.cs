using ParserLib.Models;

namespace ParserLib.Interfaces
{
    internal interface ISlot : IEntity
    {
        CircularEntity Arc1 { get; set; }
        CircularEntity Arc2 { get; set; }
        Entity Line1 { get; set; }
        Entity Line2 { get; set; }

        Entity LeadIn { get; set; }
    }
}