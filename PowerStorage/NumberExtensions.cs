namespace PowerStorage
{
    public static class NumberExtensions
    {
        // These methods convert the seemingly unrelated asset values into Mw/Kw.
        // These are not conversion between Mw/Kw!

        public static int ToMw(this int sillValue)
        {
            return (sillValue * 16 + 500) / 1000;
        }

        public static int ToKw(this int sillValue)
        {
            return sillValue * 16;
        }
        
        public static int KwToSilly(this int kwValue)
        {
            return kwValue / 16;
        }
    }
}
