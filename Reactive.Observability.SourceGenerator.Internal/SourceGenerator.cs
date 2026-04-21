using Microsoft.CodeAnalysis;

namespace Reactive.Observability.SourceGenerator.Internal;

[Generator]
internal sealed class SourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(EmitEach);
    }

    private void EmitEach(IncrementalGeneratorPostInitializationContext context)
    {
        DelegateObservable.Emit(context);
        ReactiveBuilders.Emit(context);
    }
}
