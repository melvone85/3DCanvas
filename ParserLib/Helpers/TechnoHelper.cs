namespace ParserLib.Helpers
{
    public static class TechnoHelper
    {
        public enum ELineType
        {
            CutLine1 = 1,
            CutLine2 = 2,
            CutLine3 = 3,
            CutLine4 = 4,
            CutLine5 = 5,
            Marking = 6,
            Microwelding = 7,
            Rapid = 8
        }

        public enum EEntityType
        {
            Line = 1,
            Arc,
            Circle,
            Slot,
            Rapid,
            Poly,
            Rect,
            Keyhole,
            Hole
        }
    }
}
