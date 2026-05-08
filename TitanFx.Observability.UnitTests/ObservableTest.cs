using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Linq;
using AwesomeAssertions;

namespace TitanFx.Observability;

internal static class ObservableTest
{
    public static ObservableTest<T> Test<T>(this IObservable<T> source)
    {
        return new(source);
    }
}

internal sealed class ObservableTest<T> : IDisposable
{
    private readonly IDisposable _subscription;
    private readonly ConcurrentQueue<Notification<T>> _messages = [];
    private readonly Assertion _assertion;

    public ObservableTest(IObservable<T> source)
    {
        _assertion = new(_messages);
        _subscription = source.Materialize().Subscribe(OnNext);
    }

    private void OnNext(Notification<T> notification)
    {
        _messages.Enqueue(notification);
    }

    public void ShouldBeEmpty()
    {
        _assertion.Only();
    }

    public Assertion ShouldBe(T value)
    {
        return _assertion.ThenBe(value);
    }

    public Assertion ShouldBe(T value, out T actual)
    {
        return _assertion.ThenBe(value, out actual);
    }

    public Assertion ShouldBeCompleted()
    {
        return _assertion.ThenBeCompleted();
    }

    public Assertion ShouldBeError(Exception exception)
    {
        return _assertion.ThenBeError(exception);
    }

    public sealed class Assertion(ConcurrentQueue<Notification<T>> messages)
    {
        public void Only()
        {
            _ = messages.TryDequeue(out var notification);
            _ = notification.Should().BeNull();
        }

        public Assertion ThenBe(T value)
        {
            return ThenBe(value, out _);
        }

        public Assertion ThenBe(T value, out T actual)
        {
            _ = messages.TryDequeue(out var notification).Should().BeTrue();
            _ = notification
                .Should()
                .BeEquivalentTo(
                    Notification.CreateOnNext(value),
                    op => op.ComparingByMembers<Notification<T>>()
                );
            actual = notification.Value;
            return this;
        }

        public Assertion ThenBeCompleted()
        {
            _ = messages.TryDequeue(out var notification).Should().BeTrue();
            _ = notification
                .Should()
                .BeEquivalentTo(
                    Notification.CreateOnCompleted<T>(),
                    op => op.ComparingByMembers<Notification<T>>()
                );
            return this;
        }

        public Assertion ThenBeError(Exception exception)
        {
            _ = messages.TryDequeue(out var notification).Should().BeTrue();
            _ = notification
                .Should()
                .BeEquivalentTo(
                    Notification.CreateOnError<T>(exception),
                    op =>
                        op.ComparingByMembers<Notification<T>>()
                            .Excluding(x => x.Value)
                            .Excluding(x => x.Exception!.StackTrace)
                            .Excluding(x => x.Exception!.Source)
                            .Excluding(x => x.Exception!.TargetSite)
                );
            return this;
        }
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}
