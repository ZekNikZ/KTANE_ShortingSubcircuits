using System.Collections.Generic;

namespace ShortingSubcircuits {
    public static class Util {
        public delegate T Supplier<T>();

        public static List<T> ListOf<T>(int count, Supplier<T> supplier) {
            List<T> list = new List<T>(count);
            for (int i = 0; i < count; ++i) {
                list.Add(supplier.Invoke());
            }

            return list;
        }
    }

    public class Pair<T1, T2> {
        public T1 First { get; private set; }
        public T2 Second { get; private set; }

        public Pair(T1 first, T2 second) {
            this.First = first;
            this.Second = second;
        }
    }
}
