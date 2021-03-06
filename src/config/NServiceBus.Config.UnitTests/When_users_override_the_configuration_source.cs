namespace NServiceBus.Config.UnitTests
{
    using System;
    using ConfigurationSource;
    using NUnit.Framework;

    [TestFixture]
    public class When_users_override_the_configuration_source
    {
        private IConfigurationSource userConfigurationSource;

        [SetUp]
        public void SetUp()
        {
            userConfigurationSource = new UserConfigurationSource();
            Configure.With(new Type[]{})
                .CustomConfigurationSource(userConfigurationSource);
        }
        
        [Test]
        public void NService_bus_should_resolve_configuration_from_that_source()
        {
            var section = Configure.GetConfigSection<TestConfigurationSection>();

            Assert.AreEqual(section.TestSetting,"TestValue");
        }

    }

    public class UserConfigurationSource : IConfigurationSource
    {
        T IConfigurationSource.GetConfiguration<T>()
        {
            var section = new TestConfigurationSection {TestSetting = "TestValue"};

            return section as T;
        }
    }
}