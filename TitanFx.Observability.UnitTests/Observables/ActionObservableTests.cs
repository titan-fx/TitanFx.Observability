using FakeItEasy;
using Xunit;

namespace TitanFx.Observability.Observables;

public static partial class ActionObservableTests
{
    [Fact]
    public static void ActionObservable_WithoutArguments_ImmediatelyExecutesAndYields()
    {
        // arrange
        var callback = A.Fake<Action>(opt => opt.Strict());
        var sut = new ActionObservable(callback);
        _ = A.CallTo(callback).DoesNothing();

        // act
        using var sub = sut.Test();

        // assert
        sub.ShouldBeCompleted().Only();
        _ = A.CallTo(callback).MustHaveHappenedOnceExactly();
    }
}
