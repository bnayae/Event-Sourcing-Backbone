using System.Threading.Channels;

using EventSourcing.Backbone.Building;
using EventSourcing.Backbone.Tests.Entities;

using FakeItEasy;

using Microsoft.Extensions.Logging;

using Xunit;
using Xunit.Abstractions;



namespace EventSourcing.Backbone.Tests;

public class EndToEndVersionAware_NamingAppendUnderscore_Tests: EndToEndVersionAwareBase
{
    private readonly IVersionAwareAppendUnderscoreConsumer _subscriber = A.Fake<IVersionAwareAppendUnderscoreConsumer>();

    #region Ctor

    public EndToEndVersionAware_NamingAppendUnderscore_Tests(
            ITestOutputHelper outputHelper,
            Func<IProducerStoreStrategyBuilder, ILogger, IProducerStoreStrategyBuilder>? producerChannelBuilder = null,
             Func<IConsumerStoreStrategyBuilder, ILogger, IConsumerStoreStrategyBuilder>? consumerChannelBuilder = null)
            : base(outputHelper, producerChannelBuilder, consumerChannelBuilder)
    {
        A.CallTo(() => _subscriber.Execute_2Async(A<ConsumerMetadata>.Ignored, A<DateTime>.Ignored))
                .ReturnsLazily(() => ValueTask.CompletedTask);
        A.CallTo(() => _subscriber.Execute_1Async(A<ConsumerMetadata>.Ignored, A<int>.Ignored))
                .ReturnsLazily(() => ValueTask.CompletedTask);
        A.CallTo(() => _subscriber.Execute_4Async(A<ConsumerMetadata>.Ignored, A<TimeSpan>.Ignored))
                .ReturnsLazily(() => ValueTask.CompletedTask);
    }

    #endregion // Ctor

    protected override string Name { get; } = "append-underscore";

    [Fact]
    public async Task End2End_VersionAware_AppendUnderscore_Test()
    {
        IVersionAwareAppendUnderscoreProducer producer =
            _producerBuilder
                    //.WithOptions(producerOption)
                    .Uri(URI)
                    .WithLogger(TestLogger.Create(_outputHelper))
                    .BuildVersionAwareAppendUnderscoreProducer();

        var ts = TimeSpan.FromSeconds(1);
        await producer.Execute_4Async(ts);
        await producer.Execute_1Async(10);
        await producer.Execute_1Async(11);

        var cts = new CancellationTokenSource();
        var subscription =
             _consumerBuilder
                     .WithOptions(cfg => cfg with { MaxMessages = 3 })
                     .WithCancellation(cts.Token)
                     .Uri(URI)
                     .WithLogger(TestLogger.Create(_outputHelper))
                     .SubscribeVersionAwareAppendUnderscoreConsumer(_subscriber);

        await subscription.Completion;

        A.CallTo(() => _subscriber.Execute_2Async(A<ConsumerMetadata>.Ignored, A<DateTime>.Ignored))
            .MustNotHaveHappened();
        A.CallTo(() => _subscriber.Execute_1Async(A<ConsumerMetadata>.Ignored, 10))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _subscriber.Execute_1Async(A<ConsumerMetadata>.Ignored, 11))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _subscriber.Execute_4Async(A<ConsumerMetadata>.Ignored, ts))
            .MustHaveHappenedOnceExactly();
    }
}
