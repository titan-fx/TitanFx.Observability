using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace TitanFx.Observability.SourceGenerator.Internal.UnitTests;

internal static class DelegateObservableTests
{
    private abstract class Variant
    {
        public abstract string Name { get; }
        public abstract string OnCall { get; }
        public abstract Items<string> TypeParameters(IEnumerable<string> arguments);
        public abstract string ActionType(IEnumerable<string> arguments);

        public abstract string Assert(string subject, IEnumerable<int> sources);
    }

    private sealed class ActionVariant : Variant
    {
        public override string Name => "ActionObservable";

        public override string OnCall => "DoesNothing()";

        public override Items<string> TypeParameters(IEnumerable<string> arguments)
        {
            return new(arguments);
        }

        public override string ActionType(IEnumerable<string> arguments)
        {
            return $"Action<{string.Join(", ", arguments)}>";
        }

        public override string Assert(string subject, IEnumerable<int> sources)
        {
            return $"{subject}.ShouldBeEmpty()";
        }
    }

    private sealed class FuncVariant : Variant
    {
        public override string Name => "FuncObservable";

        public override string OnCall =>
            "WithReturnType<int>().ReturnsLazily(x => x.Arguments.Sum(v => (int)v!))";

        public override Items<string> TypeParameters(IEnumerable<string> arguments)
        {
            return new([.. arguments, "int"]);
        }

        public override string ActionType(IEnumerable<string> arguments)
        {
            return $"Func<{string.Join(", ", arguments)}, int>";
        }

        public override string Assert(string subject, IEnumerable<int> sources)
        {
            return $"{subject}.ShouldBe({sources.Sum()}).Only()";
        }
    }

    private static readonly Variant[] _variants = [new ActionVariant(), new FuncVariant()];

    public static void Emit(IncrementalGeneratorPostInitializationContext context)
    {
        var sb = new StringBuilder();

        _ = sb.AppendLine("#nullable enable");
        _ = sb.AppendLine("using AwesomeAssertions;");
        _ = sb.AppendLine("using System;");
        _ = sb.AppendLine("using System.Threading;");
        _ = sb.AppendLine("using System.Reactive;");
        _ = sb.AppendLine("using System.Reactive.Subjects;");
        _ = sb.AppendLine("using System.Collections.Generic;");
        _ = sb.AppendLine("using System.Runtime.ExceptionServices;");
        _ = sb.AppendLine("using FakeItEasy;");
        _ = sb.AppendLine("using Xunit;");

        _ = sb.AppendLine("namespace TitanFx.Observability.Observables;");

        foreach (var variant in _variants)
        {
            _ = sb.AppendLine($"partial class {variant.Name}Tests");
            _ = sb.AppendLine("{");

            for (var arity = 1; arity < 17; arity++)
            {
                var typeIds = new Items<int>(Enumerable.Range(1, arity));
                var typeParameters = variant.TypeParameters(typeIds.Select(static i => $"int"));
                var actionType = variant.ActionType(typeIds.Select(static i => $"int"));
                var sources = new Items<string>(typeIds.Select(i => $"source{i}"));
                EmitOnNextTest(sb, variant, arity, typeParameters, actionType, sources);
                EmitOnCompletedTest(sb, variant, arity, typeParameters, actionType, sources);
                EmitOnErrorTests(sb, variant, arity, typeParameters, actionType, sources);
                EmitErrorOnSubscriptionTests(
                    sb,
                    variant,
                    arity,
                    typeParameters,
                    actionType,
                    sources
                );
                EmitErrorOnCallbackError(sb, variant, arity, typeParameters, actionType, sources);
            }

            _ = sb.AppendLine("}");
        }

        context.AddSource("DelegateObservableTests.g.cs", sb.ToString());
        _ = sb.Clear();
    }

    private readonly struct Items<T>(IEnumerable<T> individual) : IEnumerable<T>
    {
        private readonly T[] _items = [.. individual];
        public int Length => _items.Length;
        public ref T this[int index] => ref _items[index];

        public IEnumerator<T> GetEnumerator() => _items.AsEnumerable().GetEnumerator();

        public override string ToString() => string.Join(", ", _items);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static void Arrange(
        StringBuilder sb,
        Variant variant,
        Items<string> typeParameters,
        string actionType,
        Items<string> sources
    )
    {
        _ = sb.AppendLine(
            $$"""
                    // arrange
                    var callback = A.Fake<{{actionType}}>(opt => opt.Strict());
            """
        );

        foreach (var source in sources)
        {
            _ = sb.AppendLine(
                $$"""
                        using var {{source}} = new Subject<int>();
                """
            );
        }

        _ = sb.AppendLine(
            $$"""
                    var sut = new {{variant.Name}}<{{typeParameters}}>({{sources}}, callback);
            """
        );
    }

    private static void EmitOnCompletedTest(
        StringBuilder sb,
        Variant variant,
        int arity,
        Items<string> typeParameters,
        string actionType,
        Items<string> sources
    )
    {
        _ = sb.AppendLine(
            $$"""
                [Fact]
                public static void {{variant.Name}}_With{{arity}}Arguments_CompletesWhenAllSourcesHaveCompleted()
                {
            """
        );

        Arrange(sb, variant, typeParameters, actionType, sources);

        _ = sb.AppendLine(
            $$"""

                    // act
                    using var sub = sut.Test();

                    // assert
            """
        );

        foreach (var source in sources.SkipLast(1))
        {
            _ = sb.AppendLine(
                $$"""
                        {{source}}.OnCompleted();
                        sub.ShouldBeEmpty();
                """
            );
        }

        _ = sb.AppendLine(
            $$"""
                    {{sources.Last()}}.OnCompleted();
                    sub.ShouldBeCompleted().Only();
                    sub.Dispose();
                    sub.ShouldBeEmpty();
                }
            """
        );
    }

    private static void EmitOnErrorTests(
        StringBuilder sb,
        Variant variant,
        int arity,
        Items<string> typeParameters,
        string actionType,
        Items<string> sources
    )
    {
        _ = sb.AppendLine(
            $$"""
                [Theory]
            {{string.Join(
                "\n",
                Enumerable.Range(0, sources.Length).Select(i => $"    [InlineData({i})]")
            )}}
                public static void {{variant.Name}}_With{{arity}}Arguments_ErrorsWhenAnySourceErrors(int errorIndex)
                {
            """
        );

        Arrange(sb, variant, typeParameters, actionType, sources);

        _ = sb.AppendLine(
            $$"""
                    var sources = new[]{ {{sources}} };
                    var error = new Exception("Success");

                    // act
                    using var sub = sut.Test();

                    // assert
                    sources[errorIndex].OnError(error);
                    sub.ShouldBeError(error).Only();
                    sub.Dispose();
                    sub.ShouldBeEmpty();
                }
            """
        );
    }

    private static void EmitErrorOnSubscriptionTests(
        StringBuilder sb,
        Variant variant,
        int arity,
        Items<string> typeParameters,
        string actionType,
        Items<string> sources
    )
    {
        _ = sb.AppendLine(
            $$"""
                [Theory]
            {{string.Join(
                "\n",
                Enumerable.Range(0, sources.Length).Select(i => $"    [InlineData({i})]")
            )}}
                public static void {{variant.Name}}_With{{arity}}Arguments_DisposesWhenASourceErrorsDuringSubscription(int errorIndex)
                {
            """
        );

        _ = sb.AppendLine(
            $$"""
                    // arrange
                    var callback = A.Fake<{{actionType}}>(opt => opt.Strict());
            """
        );

        foreach (var source in sources)
        {
            _ = sb.AppendLine(
                $$"""
                        var {{source}} = A.Fake<IObservable<int>>(opt => opt.Strict());
                """
            );
        }

        _ = sb.AppendLine(
            $$"""
                    var sut = new {{variant.Name}}<{{typeParameters}}>({{sources}}, callback);
                    var sources = new[]{ {{sources}} };
                    var subscriptions = new IDisposable[errorIndex];
                    var error = new Exception("Success");
                    for (var i = 0; i < errorIndex; i++)
                    {
                        subscriptions[i] = A.Fake<IDisposable>(opt => opt.Strict());
                        A.CallTo(() => sources[i].Subscribe(A<IObserver<int>>.Ignored)).Returns(subscriptions[i]);
                        A.CallTo(() => subscriptions[i].Dispose()).DoesNothing();
                    }
                    A.CallTo(() => sources[errorIndex].Subscribe(A<IObserver<int>>.Ignored)).Throws(error);

                    // act
                    using var sub = sut.Test();

                    // assert
                    sub.ShouldBeError(error).Only();
                    foreach (var subscription in subscriptions)
                    {
                        A.CallTo(() => subscription.Dispose()).MustHaveHappenedOnceExactly();
                    }
                }
            """
        );
    }

    private static void EmitErrorOnCallbackError(
        StringBuilder sb,
        Variant variant,
        int arity,
        Items<string> typeParameters,
        string actionType,
        Items<string> sources
    )
    {
        _ = sb.AppendLine(
            $$"""
                [Fact]
                public static void {{variant.Name}}_With{{arity}}Arguments_EmitsTheErorrWhenTheCallbackThrows()
                {
            """
        );

        Arrange(sb, variant, typeParameters, actionType, sources);

        var state = new Items<int>(Enumerable.Range(1, arity));
        _ = sb.AppendLine(
            $$"""
                    var sources = new[]{ {{sources}} };
                    var error = new Exception("Success");
                    A.CallTo(() => callback({{state}})).Throws(error);

                    // act
                    using var sub = sut.Test();

                    // assert
            """
        );

        var i = 1;
        foreach (var source in sources)
        {
            _ = sb.AppendLine(
                $$"""
                        {{source}}.OnNext({{i++}});
                """
            );
        }

        _ = sb.AppendLine(
            $$"""
                    sub.ShouldBeError(error).Only();
                }
            """
        );
    }

    private static void EmitOnNextTest(
        StringBuilder sb,
        Variant variant,
        int arity,
        Items<string> typeParameters,
        string actionType,
        Items<string> sources
    )
    {
        _ = sb.AppendLine(
            $$"""
                [Fact]
                public static void {{variant.Name}}_With{{arity}}Arguments_ExecutesEachTimeASourceUpdates()
                {
            """
        );

        Arrange(sb, variant, typeParameters, actionType, sources);

        _ = sb.AppendLine(
            $$"""
                    A.CallTo(callback).{{variant.OnCall}};

                    // act
                    using var sub = sut.Test();

                    // assert
                    A.CallTo(callback).MustNotHaveHappened();
            """
        );

        var i = 1;
        foreach (var source in sources.SkipLast(1))
        {
            _ = sb.AppendLine(
                $$"""
                        {{source}}.OnNext({{i++}});
                        sub.ShouldBeEmpty();
                        A.CallTo(callback).MustNotHaveHappened();
                """
            );
        }

        var state = new Items<int>(Enumerable.Range(1, sources.Length));

        _ = sb.AppendLine(
            $$"""
                    {{sources.Last()}}.OnNext({{i}});
                    {{variant.Assert("sub", state)}};
                    A.CallTo(() => callback({{state}})).MustHaveHappenedOnceExactly();
            """
        );

        i = 0;
        foreach (var source in sources)
        {
            state[i++] = -i;
            _ = sb.AppendLine(
                $$"""
                        Fake.ClearRecordedCalls(callback);
                        {{source}}.OnNext(-{{i}});
                        {{variant.Assert("sub", state)}};
                        A.CallTo(() => callback({{state}})).MustHaveHappenedOnceExactly();
                """
            );
        }
        _ = sb.AppendLine(
            $$"""
                    sub.Dispose();
                    sub.ShouldBeEmpty();
                }
            """
        );
    }
}
