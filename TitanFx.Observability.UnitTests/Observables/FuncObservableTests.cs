using FakeItEasy;
using Xunit;

namespace TitanFx.Observability.Observables;

public static partial class FuncObservableTests
{
    [Fact]
    public static void FuncObservable_WithoutArguments_ImmediatelyExecutesAndYields()
    {
        // arrange
        var callback = A.Fake<Func<int>>(opt => opt.Strict());
        var sut = new FuncObservable<int>(callback);
        _ = A.CallTo(() => callback()).Returns(123);

        // act
        using var sub = sut.Test();

        // assert
        sub.ShouldBe(123).ThenBeCompleted().Only();
        _ = A.CallTo(callback).MustHaveHappenedOnceExactly();
    }
}
