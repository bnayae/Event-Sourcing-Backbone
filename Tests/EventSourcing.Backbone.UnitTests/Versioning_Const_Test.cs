using EventSourcing.Backbone.UnitTests.Entities;

using Xunit;
using Xunit.Abstractions;



namespace EventSourcing.Backbone
{
    public class Versioning_Const_Test
    {
        private readonly ITestOutputHelper _outputHelper;

        public Versioning_Const_Test(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }


        [Fact]
        public void Consumer_Const_Test()
        {
            _outputHelper.WriteLine(IVersionAwareMixConsumer.Constants.Deprecated.ExecuteAsync.V0.P_String_Int32);
            Assert.Equal("String,Int32", IVersionAwareMixConsumer.Constants.Deprecated.ExecuteAsync.V0.P_String_Int32);
            _outputHelper.WriteLine(IVersionAwareMixConsumer.Constants.ExecuteAsync.V2.P_DateTime);
            Assert.Equal("DateTime", IVersionAwareMixConsumer.Constants.ExecuteAsync.V2.P_DateTime);
        }

    }
}
