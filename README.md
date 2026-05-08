# TitanFx.Observability

Easy tool for watching changes to in-memory data structures, and creating structures which broadcast changes to listeners.

## Creating a reactive object

When using the `IReactive` interface, any member can be watched on the object. Be that a property, field or method.

```csharp
using TitanFx.Observability;

// The key requirement is to implement IReactive, but ReactiveObject provides an baseline implementation of it.
public class Account : ReactiveObject 
{
    // Simplest kind of setter for a property which impacts nothing else on this object.
    public required string DisplayName { get; set => Set(ref field, value); }

    // A setter where the value of this property affects the value of another member.
    public required DateTimeOffset DateOfBirth 
    { 
        get; 
        set => Set(ref field, value, [nameof(DateOfBirth), nameof(IsOlderThan), nameof(Age)]); 
    }

    public int Age => (DateTimeOffset.UtcNow - DateOfBirth).Days / 365.25;

    public int Score;
    public void NotifyScoreHasChanged()
    {
        OnMemberChanged(nameof(Score));
    }

    public bool IsOlderThan(int age) 
    {
        return Age > age;
    }
}
```

## Watching for changes to the object

```csharp

var account = new Account { DisplayName = "Bob", DateOfBirth = DateTimeOffset.UtcNow };

IObservable<string> greeting = Reactive.Observe(() => $"Hello {account.DisplayName}! You are {account.Age} years old.");

// Every time the account.DisplayName or Age(via DateOfBirth) is changed, the updated value will be written to the console.
using var subscription = greeting.Subscribe(g => Console.WriteLine(g));
// Console: "Hello Bob! You are 0 years old"

account.DisplayName = "My cool new username";
// Console: "Hello My cool new username! You are 0 years old";

account.DateOfBirth = DateTimeOffset.UtcNow.AddYears(-20);
// Console: "Hello My cool new username! You are 20 years old";

```

### Supported expressions

All expressions which can be written as lambdas are supported:

```csharp
var ctx = SomeReactiveObject();

// Constants - They return the first value and then call OnCompleted immediately upon subscription
Reactive.Observe(() => "Some constant string");
Reactive.Observe(() => default(int));
Reactive.Observe(() => 123.456);

// Deep member access
Reactive.Observe(() => ctx.Some.Deep.Property.Or.Field.Or.Method().Including.Extension.Methods());

// Multiple sources
var src1 = MySource(1);
var src2 = MySource(2);
var src3 = MySource(3);
Reactive.Observe(() => src1.Value + src2.Value + src3.Value); // This will update when any of the .Values are changed.

// Ternaries / conditionals
MyType? v;
Reactive.Observe(() => ctx.TryGetValue(out v) ? v : default);

// Null coalesce
Reactive.Observe(() => ctx.Value1 ?? ctx.Value2);

// Implicit null propagation
Reactive.Observe(() => ctx.Some.Nullable!.Path);
Reactive.Observe(() => ctx.MyNullableInt!.Value.ToString());

// Array construction
Reactive.Observe(() => new[]{ ctx.Value1, ctx.Value2 });

// Anonymous types
Reactive.Observe(() => new { ctx.Value1, ctx.Value2 });

// Complex initialization expressions
Reactive.Observe(() => new Data { 
    V1 = ctx.Value1, // Property/field initialization
    Inner = { 
        V2 = ctx.Value2 // Nested initialization
    }, 
    Items = { // Collection initialization
        ctx.Value3, 
        ctx.Value4, 
        ctx.Value5 
    } 
});

// String formatting
Reactive.Observe(() => $"Value1 is {ctx.Value1}");

// All operators
Reactive.Observe(() => ctx.Value1 + ctx.Value2 * 5);

// Linq
var target = SomeOtherReactiveObject();
Reactive.Observe(() => ctx.Items.Where(i => i == target.Current)); // This will emit a change whenever target.Current or ctx.Items change.

// And any others that I have missed.
```

Some features of expression trees, (such as try/catch, loops, blocks, and switches) are at most partially supported.
The intention currently is to support all syntax which can be wrtitten as a lambda rather than having to use the `Expression.Blah` methods. Support for any other types of expressions will purely be coincidental 

### Reusability

If you want to watch multiple of the same type of object, it might be easier to create a delegate instead

```csharp
Func<Account, IObservable<string?>> watchDisplayName = Reactive.WithInput<Account>().Build(account => account.DisplayName);
```

This way you can skip the overhead of processing the expression tree and quickly watch multiple objects.

## Manually watching for changes

```csharp

var account = new Account { DisplayName = "Bob" };

// Subscribing this way only triggers the callback when the value is changed. Simply subscribing does not do anything.
using var subscription = account.Watch(nameof(account.DisplayName), () => Console.WriteLine($"Hello {account.DisplayName}"));

account.DisplayName = "Bob420";
// Console: "Hello Bob420!"
```

## Support for out of the box notification schemes

While the supplied `IReactive` interface is the preferred way of listening for changes to an object, if you have existing objects which already implement `INotifyPropertyChanged` or `INotifyCollectionChanged` then the default observer will also watch for changes via those interfaces.

## Null safety

Due to expressions not supporting null propagation via the `?` operator, your observable expression will have null checks inserted into it to attempt to avoid `NullReferenceException`s from being thrown. This works with `x.Field`, `x.Property`, `x.Method(..)`, `x.ExtensionMethod(..)`, and `x[..]` expressions.

```csharp

Reactive.Observe(() => root.SomeNullableProp!.MyStringValue);
// gets transformed to
Reactive.Observe(() => {
    var value = root.SomeNullableProp;
    if (value is null)
        return default(string);
    return value.MyStringValue;
})

```

## Watching static members

While not implemented by the default binder, support for watching static members is available. You will need to create your own `ReactiveProvider` instance and supply it a custom `IReactiveBinder` instance.

```csharp
using TitanFx.Observability.Binding;

var myBinder = new ReactiveBinder(
    new DefaultReactiveBinderItem(),
    new NotifyPropertyChangedBinderItem(),
    new NotifyCollectionChangedBinderItem(),
    new MyCustomBinderItem()
);
var myReactive = new ReactiveProvider(myBinder);

Source.Value = "Testing";
using var subscription = myReactive.Observe(() => Source.Value).Subscribe(Console.WriteLine);
// Console: Testing

Source.Value = "Success!";
// Console: Success!

sealed class MyCustomBinder : IReactiveBinderItem
{
    public bool IsInstanceSupported<T>(MemberInfo member) => false;
    public IObservable<T> WatchInstance<T>(T instance, MemberInfo member) => throw new NotSupportedException();

    public bool IsStaticSupported(MemberInfo member) 
        => member.DeclaringType == typeof(Source) 
            && member.Name == nameof(Source.Value);

    public IObservable<Unit> WatchStatic(MemberInfo member) 
    {
        return Observable.FromEvent(
            callback => Source.OnValueChanged += callback,
            callback => Source.OnValueChanged -= callback
        );
    }
}

static class Source
{
    public static event Action? OnValueChanged;
    public static string Value { 
        get; 
        set 
        {
            field = value;
            OnValueChanged();
        }
    }
}

```
