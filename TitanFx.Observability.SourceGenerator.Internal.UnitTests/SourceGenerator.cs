using Microsoft.CodeAnalysis;

namespace TitanFx.Observability.SourceGenerator.Internal.UnitTests;

[Generator]
internal sealed class SourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(EmitEach);
    }

    private void EmitEach(IncrementalGeneratorPostInitializationContext context)
    {
        DelegateObservableTests.Emit(context);
        ReactiveBuildersTests.Emit(context);
    }
}
