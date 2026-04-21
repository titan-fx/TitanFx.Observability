using System;

namespace Reactive.Observability;

public static class AggregatorExtensions
{
    extension<T>(ReadOnlySpan<T> source)
    {
        public T BisectAggregate(Func<T, T, T> combine)
        {
            ArgumentOutOfRangeException.ThrowIfZero(source.Length, nameof(source));

            Span<T> buffer = new T[(source.Length + 1) / 2];
            while (source.Length > 1)
            {
                var end = source.Length / 2;
                for (var i = 0; i < end; i++)
                {
                    buffer[i] = combine(source[i * 2], source[(i * 2) + 1]);
                }
                if (end < buffer.Length)
                {
                    buffer[^1] = source[^1];
                }
                source = buffer;
                buffer = buffer[..((source.Length + 1) / 2)];
            }
            return source[0];
        }
    }
}
