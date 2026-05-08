using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace TitanFx.Observability.SourceGenerator.Internal.UnitTests;

internal static class ReactiveBuildersTests
{
    public static void Emit(IncrementalGeneratorPostInitializationContext context)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine("#nullable enable");
        _ = sb.AppendLine("using System;");
        _ = sb.AppendLine("using Xunit;");
        _ = sb.AppendLine("using Target = TitanFx.Observability.ReactiveProperty<int>;");
        _ = sb.AppendLine("namespace TitanFx.Observability;");
        _ = sb.AppendLine("partial class ReactiveTests");
        _ = sb.AppendLine("{");

        foreach (
            var (kind, provider) in new[]
            {
                ("Default", "Reactive"),
                ("Given", "Reactive.Provider"),
            }
        )
        {
            _ = sb.AppendLine(
                $$"""
                    [Fact]
                    public static void Reactive_Observe_WatchesViaThe{{kind}}Provider()
                    {
                        // arrange
                        var target = new Target(0);
                        var sut = {{provider}}.Observe(() => target.Value);

                        // act
                        using var sub = sut.Test();

                        // assert
                        sub.ShouldBe(0).Only();
                        target.Value = 123;
                        sub.ShouldBe(123).Only();
                    }

                    [Fact]
                    public static void Reactive_Build_CreatesAFactoryWhichObservesViaThe{{kind}}Provider()
                    {
                        // arrange
                        var target = new Target(0);
                        var factory = {{provider}}.Build(() => target.Value);
                        var sut = factory();

                        // act
                        using var sub = sut.Test();

                        // assert
                        sub.ShouldBe(0).Only();
                        target.Value = 123;
                        sub.ShouldBe(123).Only();
                    }
                """
            );

            for (var i = 1; i < 17; i++)
            {
                var argTypes = Enumerable.Range(1, i);
                var typeParams = Enumerable.Repeat("Target", i);
                var typeParamsStr = string.Join(", ", typeParams);
                var args = argTypes.Select(i => $"s{i}");
                var sources = argTypes.Select(i => $"source{i}");
                var selector = string.Join(" + ", args.Select(s => $"{s}.Value"));
                var argsStr = string.Join(", ", args);
                var sourcesStr = string.Join(", ", sources);

                _ = sb.AppendLine(
                    $$"""
                        [Fact]
                        public static void Reactive_Build{{i}}_CreatesAFactoryWhichObservesViaThe{{kind}}Provider()
                        {
                            // arrange
                    {{string.Join("\n", sources.Select((s, i) => $$"""
                            var {{s}} = new Target({{i + 1}});
                    """))}}
                            var factory = {{provider}}.Build<{{typeParamsStr}}, int>(({{argsStr}}) => {{selector}});
                            var sut = factory({{sourcesStr}});

                            // act
                            using var sub = sut.Test();

                            // assert
                            sub.ShouldBe({{argTypes.Sum()}}).Only();
                    {{string.Join("\n", sources.Select((s, i) => $$"""
                            {{s}}.Value = -{{++i}};
                            sub.ShouldBe({{argTypes.Sum(v => v <= i ? -v : v)}}).Only();
                    """))}}
                        }

                        [Fact]
                        public static void Reactive_With{{i}}Inputs_Build_CreatesAFactoryWhichObservesViaThe{{kind}}Provider()
                        {
                            // arrange
                    {{string.Join("\n", sources.Select((s, i) => $$"""
                            var {{s}} = new Target({{i + 1}});
                    """))}}
                            var factory = {{provider}}.WithInput<{{typeParamsStr}}>().Build(({{argsStr}}) => {{selector}});
                            var sut = factory({{sourcesStr}});

                            // act
                            using var sub = sut.Test();

                            // assert
                            sub.ShouldBe({{argTypes.Sum()}}).Only();
                    {{string.Join("\n", sources.Select((s, i) => $$"""
                            {{s}}.Value = -{{++i}};
                            sub.ShouldBe({{argTypes.Sum(v => v <= i ? -v : v)}}).Only();
                    """))}}
                        }
                    """
                );
            }
        }

        _ = sb.AppendLine("}");
        context.AddSource("ReactiveBuildersTests.g.cs", sb.ToString());
        _ = sb.Clear();
    }
}
