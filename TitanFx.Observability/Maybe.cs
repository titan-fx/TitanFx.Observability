using System.Diagnostics.CodeAnalysis;

namespace TitanFx.Observability;

internal readonly struct Maybe<T>(T value)
{
    private readonly T _value = value;

    public bool HasValue { get; } = true;

    public readonly T? GetValueOrDefault() => _value;

    public static implicit operator Maybe<T>(T value) => new(value);

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
