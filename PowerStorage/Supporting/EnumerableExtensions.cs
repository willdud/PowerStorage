using System;
using System.Collections.Generic;

namespace PowerStorage.Supporting
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> Zip<TA, TB, T>(this IEnumerable<TA> seqA, IEnumerable<TB> seqB, Func<TA, TB, T> func)
        {
            if (seqA == null) throw new ArgumentNullException("seqA");
            if (seqB == null) throw new ArgumentNullException("seqB");
 
            return Zip35Deferred(seqA, seqB, func);
        }
        private static IEnumerable<T> Zip35Deferred<TA, TB, T>(this IEnumerable<TA> seqA, IEnumerable<TB> seqB, Func<TA, TB, T> func)
        {
            using (var iteratorA = seqA.GetEnumerator())
            using (var iteratorB = seqB.GetEnumerator())
            {
                while (iteratorA.MoveNext() && iteratorB.MoveNext())
                {
                    yield return func(iteratorA.Current, iteratorB.Current);
                }
            }
        }
    }
}
