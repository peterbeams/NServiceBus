﻿namespace NServiceBus.Unicast.Tests
{
    using System;
    using Contexts;
    using NUnit.Framework;
   
    [TestFixture]
    public class When_processing_a_message_successfully_with_a_registered_message_module : using_the_unicastbus
    {
        [Test]
        public void Should_invoke_the_begin_and_end_on_the_message_module()
        {

            var endCalled = false;

            var messageModule = new StubMessageModule();
            bool beginCalled = false;

            var receivedMessage = Helpers.Helpers.EmptyTransportMessage();

            messageModule.OnBegin = () =>
                                        {
                                            Assert.False(endCalled);
                                            beginCalled = true;
                                            Assert.AreEqual( bus.CurrentMessageContext.Id, receivedMessage.Id);  
                                        };

            messageModule.OnEnd = () =>
            {
                Assert.True(beginCalled);
                endCalled = true;
                Assert.AreEqual(bus.CurrentMessageContext.Id, receivedMessage.Id);
            };
            FuncBuilder.Register<IMessageModule>(()=>messageModule);


            ReceiveMessage(receivedMessage);

            Assert.True(beginCalled);
            Assert.True(endCalled);
			Assert.True(FuncBuilder.HasBeenReleased(messageModule));
        }
    }

    [TestFixture]
    public class When_a_message_if_forwarded_via_the_fault_manager : using_the_unicastbus
    {
        [Test]
        public void Should_invoke_begin_and_end_message()
        {
            var endCalled = false;

            var messageModule = new StubMessageModule();
            bool beginCalled = false;

            messageModule.OnBegin = () =>
            {
                Assert.False(endCalled);
                beginCalled = true;
            };

            messageModule.OnEnd = () =>
            {
                Assert.True(beginCalled);
                endCalled = true;
            };
            FuncBuilder.Register<IMessageModule>(() => messageModule);


            SimulateMessageBeeingAbortedDueToRetryCountExceeded(Helpers.Helpers.EmptyTransportMessage());

            Assert.True(beginCalled);
            Assert.True(endCalled);
			Assert.True(FuncBuilder.HasBeenReleased(messageModule));
        }

        [Test]
        public void Should_not_invoke_handle_error()
        {
        }

    }

    public class StubMessageModule:IMessageModule
    {
        public Action OnBegin = () => { };
        public Action OnEnd = () => { };
        public Action OnError = () => Assert.Fail("Error occurred");
        public void HandleBeginMessage()
        {
            OnBegin();
        }

        public void HandleEndMessage()
        {
            OnEnd();
        }

        public void HandleError()
        {
            OnError();
        }
    }
}