﻿using NServiceBus.Faults;
using NServiceBus.ObjectBuilder;
using NServiceBus.Unicast.Queuing;
using NServiceBus.Unicast.Queuing.Msmq;
using NServiceBus.Unicast.Transport;
using NServiceBus.Unicast.Transport.Transactional;

namespace NServiceBus.Satellites
{
    public interface ISatelliteTransportBuilder
    {        
        ITransport Build();
    }

    public class SatelliteTransportBuilder : ISatelliteTransportBuilder
    {
        public IBuilder Builder { get; set; }
        public TransactionalTransport MainTransport { get; set; }

        public ITransport Build()
        {
            var nt = 1; // MainTransport != null ? MainTransport.NumberOfWorkerThreads == 0 ? 1 : MainTransport.NumberOfWorkerThreads : 1;
            var mr = MainTransport != null ? MainTransport.MaxRetries : 1;
            var tx = MainTransport != null ? MainTransport.IsTransactional : !ConfigureVolatileQueues.IsVolatileQueues;

            var fm = MainTransport != null
                         ? Builder.Build(MainTransport.FailureManager.GetType()) as IManageMessageFailures
                         : Builder.Build<IManageMessageFailures>();

            return new TransactionalTransport
            {
                MessageReceiver = MainTransport != null
                         ? Builder.Build(MainTransport.MessageReceiver.GetType()) as IReceiveMessages
                         : Builder.Build<MsmqMessageReceiver>(),
                IsTransactional = tx,
                NumberOfWorkerThreads = nt,
                MaxRetries = mr,
                FailureManager = fm
            };            
        }
    }
}