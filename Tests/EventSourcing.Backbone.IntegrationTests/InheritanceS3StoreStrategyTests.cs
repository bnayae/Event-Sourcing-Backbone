using Xunit;
using Xunit.Abstractions;

namespace EventSourcing.Backbone.Tests;

[Trait("provider", "s3")]
public class InheritanceS3StoreStrategyTests : InheritanceTests
{
    private static readonly S3ConsumerOptions OPTIONS = new S3ConsumerOptions
    {
        EnvironmentConvention = S3EnvironmentConvention.BucketPrefix,
        BasePath = "tests"
    };

    public InheritanceS3StoreStrategyTests(ITestOutputHelper outputHelper) :
            base(outputHelper,
                (b, logger) => b.AddS3Storage(OPTIONS),
                (b, logger) => b.AddS3Storage(OPTIONS))
    {
    }

}
