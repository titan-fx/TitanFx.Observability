using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace TitanFx.Observability.SourceGenerator.Internal;

internal static class DelegateObservable
{
    private abstract class Variant
    {
        public abstract string Name { get; }
        public abstract string ElementType { get; }
        public abstract string TypeParameters(IEnumerable<string> arguments);
        public abstract string ActionType(IEnumerable<string> arguments);
        public abstract string Execute(string delegateName, IEnumerable<string> arguments);
        public abstract string OnNext(string value, string observer);
    }

    private sealed class ActionVariant : Variant
    {
        public override string Name => "ActionObservable";

        public override string ElementType => "Never";

        public override string TypeParameters(IEnumerable<string> arguments)
        {
            return string.Join(", ", arguments);
        }

        public override string ActionType(IEnumerable<string> arguments)
        {
            return $"Action<{string.Join(", ", arguments)}>";
        }

        public override string Execute(string delegateName, IEnumerable<string> arguments)
        {
            return $"{delegateName}({string.Join(", ", arguments)}); return default;";
        }

        public override string OnNext(string value, string observer)
        {
            return "";
        }
    }

    private sealed class FuncVariant : Variant
    {
        public override string Name => "FuncObservable";

        public override string ElementType => "TResult";

        public override string TypeParameters(IEnumerable<string> arguments)
        {
            return $"{string.Join(", ", arguments)}, TResult";
        }

        public override string ActionType(IEnumerable<string> arguments)
        {
            return $"Func<{string.Join(", ", arguments)}, TResult>";
        }

        public override string Execute(string delegateName, IEnumerable<string> arguments)
        {
            return $"return {delegateName}({string.Join(", ", arguments)});";
        }

        public override string OnNext(string value, string observer)
        {
            return $"{observer}.OnNext({value});";
        }
    }

    private static readonly Variant[] _variants = [new ActionVariant(), new FuncVariant()];

    public static void Emit(IncrementalGeneratorPostInitializationContext context)
    {
        var sb = new StringBuilder();

        _ = sb.AppendLine("#nullable enable");
        _ = sb.AppendLine("using System;");
        _ = sb.AppendLine("using System.Threading;");
        _ = sb.AppendLine("using System.Collections.Generic;");
        _ = sb.AppendLine("using System.Runtime.ExceptionServices;");

        _ = sb.AppendLine("namespace TitanFx.Observability.Observables;");

        for (var i = 1; i < 17; i++)
        {
            var typeIds = Enumerable.Range(1, i);
            foreach (var variant in _variants)
            {
                var name = variant.Name;
                var tReturn = variant.ElementType;
                var argTypes = typeIds.Select(static i => $"T{i}");
                var typeParameters = variant.TypeParameters(argTypes);
                var actionType = variant.ActionType(argTypes);

                _ = sb.AppendLine(
                    $$"""
                internal sealed class {{name}}<{{typeParameters}}>(
                {{string.Join("\r\n", typeIds.Select(static i => $$"""
                    IObservable<T{{i}}> source{{i}},
                """))}}
                    {{actionType}} action
                ) : IObservable<{{tReturn}}>
                {
                    public IDisposable Subscribe(IObserver<{{tReturn}}> observer)
                    {
                        return new Subscription(
                {{string.Join("\r\n", typeIds.Select(static i => $$"""
                            source{{i}},
                """))}}
                            action,
                            observer
                        );
                    }

                    private sealed class Subscription : IDisposable 
                    {
                        private readonly IObserver<{{tReturn}}> _observer;
                        private readonly {{actionType}} _action;
                        private readonly Lock _lock;
                        private const uint _allSet = 0x{{~(~0U << i):X4}}u;
                        private uint _completed;
                        private uint _hasValue;
                {{string.Join("\r\n", typeIds.Select(static i => $$"""
                        private const uint _t{{i}}Flag = 0x{{1U << (i - 1):X4}}u;
                        private T{{i}}? _t{{i}};
                        private readonly IDisposable _t{{i}}Subscription;
                """))}}

                        public Subscription(
                {{string.Join("\r\n", typeIds.Select(static i => $$"""
                            IObservable<T{{i}}> source{{i}},
                """))}}
                            {{actionType}} action,
                            IObserver<{{tReturn}}> observer
                        )
                        {
                            _observer = observer;
                            _action = action;
                            _lock = new();

                            try 
                            {
                {{string.Join("\r\n", typeIds.Select(static i => $$"""
                                _t{{i}}Subscription = source{{i}}.Subscribe(new T{{i}}Observer(this));
                """))}}
                            }
                            catch 
                            {
                                Dispose();
                                throw;
                            }
                        }

                        private {{variant.ElementType}} Execute()
                        {
                            {{variant.Execute("_action", typeIds.Select(static i => $"_t{i}!"))}}
                        }

                        private void OnAnyCompleted()
                        {
                            if (_completed == _allSet)
                                _observer.OnCompleted();
                        }

                        private void OnAnyError(Exception error)
                        {
                            _observer.OnError(error);
                        }

                        private void OnAnyNext()
                        {
                            if (_hasValue != _allSet)
                                return;

                            {{variant.ElementType}} result;
                            try
                            {
                                result = Execute();
                            }
                            catch (Exception exception)
                            {
                                _observer.OnError(exception);
                                return;
                            }
                            {{variant.OnNext("result", "_observer")}}
                        }
                        
                        public void Dispose()
                        {
                {{string.Join("\r\n", typeIds.Select(static i => $$"""
                            using (_t{{i}}Subscription)
                """))}}
                            {}
                        }

                        private abstract class Observer<T>(uint flag, Subscription subscription) : IObserver<T>
                        {
                            public void OnCompleted()
                            {
                                lock(subscription._lock)
                                    subscription._completed |= flag;
                                subscription.OnAnyCompleted();
                            }
                
                            public void OnError(Exception error)
                            {
                                subscription.OnAnyError(error);
                            }
                
                            public void OnNext(T value)
                            {
                                Set(subscription, value);
                                if ((subscription._hasValue & flag) == 0)
                                {
                                    lock(subscription._lock)
                                        subscription._hasValue |= flag;
                                }
                                subscription.OnAnyNext();
                            }

                            protected abstract void Set(Subscription subscription, T value);
                        }

                {{string.Join("\r\n", typeIds.Select(static i => $$"""
                        private sealed class T{{i}}Observer(Subscription subscription) : Observer<T{{i}}>(_t{{i}}Flag, subscription)
                        {
                            protected override void Set(Subscription subscription, T{{i}} value)
                            {
                                subscription._t{{i}} = value;
                            }
                        }
                """))}}
                    }
                }
                """
                );
            }
        }

        context.AddSource("DelegateObservable.g.cs", sb.ToString());
        _ = sb.Clear();
    }
}
