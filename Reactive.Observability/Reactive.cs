using Reactive.Observability.Binding;

namespace Reactive.Observability;

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
