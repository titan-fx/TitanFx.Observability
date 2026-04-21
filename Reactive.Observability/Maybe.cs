using System;
using System.Diagnostics.CodeAnalysis;

namespace Reactive.Observability;

internal readonly struct Maybe<T>(T value)
{
    private readonly T _value = value;

    public bool HasValue { get; } = true;
    public T Value => HasValue ? _value : throw new InvalidOperationException("No value is set");

    public readonly T? GetValueOrDefault() => _value;

    public readonly T GetValueOrDefault(T @default) => HasValue ? _value : @default;

    public static implicit operator Maybe<T>(T value) => new(value);

    public static explicit operator T(Maybe<T> value) => value.Value;

    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return HasValue;
    }
}

internal static class Maybe
{
    extension<T>(Maybe<T> @this)
        where T : struct
    {
        public T? ToNullable()
        {
            return @this.HasValue ? @this.GetValueOrDefault() : null;
        }
    }
}
