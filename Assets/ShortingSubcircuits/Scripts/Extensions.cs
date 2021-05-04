using System;
using System.Collections.Generic;
using System.Linq;

namespace ShortingSubcircuits {
    public static class Extensions {
        public delegate void BiAction<T1, T2>(T1 first, T2 second);
        
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> items, int maxItems) {
            return items.Select((item, inx) => new {item, inx}).GroupBy(x => x.inx / maxItems)
                .Select(g => g.Select(x => x.item));
        }

        public static IEnumerable<T> Peek<T>(this IEnumerable<T> items, Action<T> action) {
            var enumerable = items.ToList();
            foreach (var item in enumerable) {
                action.Invoke(item);
            }
            return enumerable;
        }
        
        public static IEnumerable<T> Peek<T>(this IEnumerable<T> items, BiAction<T, int> action) {
            var enumerable = items.ToList();
            for (var index = 0; index < enumerable.Count; index++) {
                var item = enumerable[index];
                action.Invoke(item, index);
            }
            return enumerable;
        }
    }
}
