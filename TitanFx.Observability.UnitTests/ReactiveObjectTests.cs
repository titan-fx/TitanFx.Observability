using FakeItEasy;
using Xunit;

namespace TitanFx.Observability;

public static class ReactiveObjectTests
{
    [Fact]
    public static void ReactiveObject_AllowsMultipleSubscriptionsToOneProperty()
    {
        // arrange
        var callbacks = Enumerable.Range(0, 100).Select(_ => A.Fake<Action>()).ToList();
        var sut = new ReactiveProperties<int, int, int, int, int>
        {
            V1 = 0,
            V2 = 100,
            V3 = 200,
            V4 = 300,
            V5 = 400,
        };
        var subscriptions = new List<IDisposable>(100);

        // act
        foreach (var callback in callbacks)
            subscriptions.Add(sut.Watch(nameof(sut.V3), callback));

        // assert
        var rand = new Random(420);
        while (subscriptions.Count > 0)
        {
            sut.V1++;
            sut.V2++;
            sut.V3++;
            sut.V4++;
            sut.V5++;
            foreach (var callback in callbacks)
            {
                _ = A.CallTo(callback).MustHaveHappenedOnceExactly();
                Fake.ClearRecordedCalls(callback);
            }

            var index = rand.Next(callbacks.Count);
            subscriptions[index].Dispose();
            callbacks.RemoveAt(index);
            subscriptions.RemoveAt(index);
        }
    }

    [Fact]
    public static void ReactiveObject_AllowsMultipleSubscriptionsToAllProperties()
    {
        // arrange
        var callbacks = Enumerable.Range(0, 100).Select(_ => A.Fake<Action>()).ToList();
        var sut = new ReactiveProperties<int, int, int, int, int>
        {
            V1 = 0,
            V2 = 100,
            V3 = 200,
            V4 = 300,
            V5 = 400,
        };
        var subscriptions = new List<IDisposable>(100);

        // act
        foreach (var callback in callbacks)
            subscriptions.Add(sut.Watch(null, callback));

        // assert
        var rand = new Random(420);
        while (subscriptions.Count > 0)
        {
            sut.V1++;
            sut.V2++;
            sut.V3++;
            sut.V4++;
            sut.V5++;
            foreach (var callback in callbacks)
            {
                _ = A.CallTo(callback).MustHaveHappened(5, Times.Exactly);
                Fake.ClearRecordedCalls(callback);
            }

            var index = rand.Next(callbacks.Count);
            subscriptions[index].Dispose();
            callbacks.RemoveAt(index);
            subscriptions.RemoveAt(index);
        }
    }
}
