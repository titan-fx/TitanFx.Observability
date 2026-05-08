using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace TitanFx.Observability.SourceGenerator.Internal;

internal static class ReactiveBuilders
{
    public static void Emit(IncrementalGeneratorPostInitializationContext context)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine("#nullable enable");
        _ = sb.AppendLine("using System;");
        _ = sb.AppendLine("using System.Linq.Expressions;");
        _ = sb.AppendLine("using System.Runtime.CompilerServices;");
        _ = sb.AppendLine("using TitanFx.Observability.Binding;");
        _ = sb.AppendLine("namespace TitanFx.Observability;");
        _ = sb.AppendLine("static partial class Reactive");
        _ = sb.AppendLine("{");

        _ = sb.AppendLine(
            $$"""
                public static IObservable<TResult?> Observe<TResult>(Expression<Func<TResult>> expression)
                    => Build(Provider, expression)();
                public static IObservable<TResult?> Observe<TResult>(this ReactiveProvider provider, Expression<Func<TResult>> expression)
                    => Build(provider, expression)();


                public static Func<IObservable<TResult?>> Build<TResult>(Expression<Func<TResult>> expression) 
                    => Build(Provider, expression);
                public static Func<IObservable<TResult?>> Build<TResult>(this ReactiveProvider provider, Expression<Func<TResult>> expression) 
                    => Unsafe.As<Func<IObservable<TResult?>>>(provider.Build(expression));
            """
        );

        for (var i = 1; i < 17; i++)
        {
            var argTypes = Enumerable.Range(1, i);
            var typeParams = argTypes.Select(i => $"T{i}").ToArray();
            var typeParamsStr = string.Join(", ", typeParams);

            _ = sb.AppendLine(
                $$"""
                    public static Builder<{{typeParamsStr}}> WithInput<{{typeParamsStr}}>() 
                        => new(Provider);
                    public static Builder<{{typeParamsStr}}> WithInput<{{typeParamsStr}}>(this ReactiveProvider provider) 
                        => new(provider);
                    public static Func<{{typeParamsStr}}, IObservable<TResult?>> Build<{{typeParamsStr}}, TResult>(Expression<Func<{{typeParamsStr}}, TResult>> expression) 
                        => Build(Provider, expression);
                    public static Func<{{typeParamsStr}}, IObservable<TResult?>> Build<{{typeParamsStr}}, TResult>(this ReactiveProvider provider, Expression<Func<{{typeParamsStr}}, TResult>> expression) 
                        => Unsafe.As<Func<{{typeParamsStr}}, IObservable<TResult?>>>(provider.Build(expression));
                    public readonly struct Builder<{{typeParamsStr}}>(ReactiveProvider provider)
                    {
                        public Func<{{typeParamsStr}}, IObservable<TResult?>> Build<TResult>(Expression<Func<{{typeParamsStr}}, TResult>> expression)
                            => Reactive.Build(provider, expression);
                    }
                """
            );
        }
        _ = sb.AppendLine("}");
        context.AddSource("ReactiveBuilders.g.cs", sb.ToString());
        _ = sb.Clear();
    }
}
