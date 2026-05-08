using TitanFx.Observability.Binding;

namespace TitanFx.Observability;

public static partial class Reactive
{
    public static ReactiveProvider Provider { get; } =
        new(
            new ReactiveBinder(
                new DefaultReactiveBinderItem(),
                new NotifyPropertyChangedBinderItem(),
                new NotifyCollectionChangedBinderItem()
            )
        );
}
