namespace TimeoutMigrator
{
    using NServiceBus;
    using NServiceBus.MessageMutator;

    public class DestinationOverride : IMutateOutgoingTransportMessages, INeedInitialization
    {
        public static Address CurrentDestination { get; set; }

        public void MutateOutgoing(object[] messages, TransportMessage transportMessage)
        {
            transportMessage.ReplyToAddress = CurrentDestination;
        }

        public void Init()
        {
            Configure.Instance.Configurer.ConfigureComponent<DestinationOverride>(DependencyLifecycle.SingleInstance);
        }
    }
}