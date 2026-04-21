using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Reactive.Observability;

internal static class ReadOnlyCollectionSliceExtensions
{
    extension<T>(ReadOnlyCollection<T> @this)
    {
        public ReadOnlyCollection<T> Slice(int offset, int length)
        {
            var arr = new T[length];
            for (var i = 0; i < length; i++)
                arr[i] = @this[offset + i];
            return arr.AsReadOnly();
        }

        public ReadOnlyCollection<T> Slice(Range range)
        {
            var (offset, length) = range.GetOffsetAndLength(@this.Count);
            return @this.Slice(offset, length);
        }
    }
}
