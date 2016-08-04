//-----------------------------------------------------------------------
// <copyright file="EventBusSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.TestKit;
using Akka.Util.Internal;
using Xunit;
using FluentAssertions;

namespace Akka.Tests.Event
{
    /// <summary>
    /// I used <see cref="TestActorEventBus"/> for both specs, since ActorEventBus and EventBus 
    /// are even to each other at the time, spec is written.
    /// </summary>
    internal class TestActorEventBus : ActorEventBus<object, Type>
    {
        protected override bool IsSubClassification(Type parent, Type child)
        {
            return child.IsAssignableFrom(parent);
        }

        protected override void Publish(object evt, IActorRef subscriber)
        {
            subscriber.Tell(evt);
        }

        protected override bool Classify(object evt, Type classifier)
        {
            return evt.GetType().IsAssignableFrom(classifier);
        }

        protected override Type GetClassifier(object @event)
        {
            return @event.GetType();
        }
    }

    internal class TestActorWrapperActor : ActorBase
    {
        private readonly IActorRef _ref;

        public TestActorWrapperActor(IActorRef actorRef)
        {
            _ref = actorRef;
        }

        protected override bool Receive(object message)
        {
            _ref.Forward(message);
            return true;
        }
    }

    public struct Notification
    {
        public Notification(IActorRef @ref, int payload) : this()
        {
            Ref = @ref;
            Payload = payload;
        }

        public IActorRef Ref { get; set; }
        public int Payload { get; set; }
    }

    public class EventBusSpec : AkkaSpec
    {
        private readonly ActorEventBus<object, Type> _bus;
        private readonly IEnumerable<Notification> _events;
        private readonly Notification _event;
        private readonly Type _classifier;
        private readonly IActorRef _subscriber;

        public EventBusSpec()
        {
            _bus = new TestActorEventBus();
            _events = CreateEvents(100);
            _event = _events.First();
            _classifier = typeof(Notification);
            _subscriber = TestActor;
        }

        [Fact]
        public void ActorEventBus_must_allow_subscribers()
        {
            _bus.Subscribe(_subscriber, _classifier).Should().BeTrue();
        }

        [Fact]
        public void ActorEventBus_must_allow_to_unsubscribe_already_existing_subscriber()
        {
            _bus.Subscribe(_subscriber, _classifier).Should().BeTrue();
            _bus.Unsubscribe(_subscriber, _classifier).Should().BeTrue();
        }

        [Fact]
        public void ActorEventBus_must_not_allow_to_unsubscribe_not_existing_subscriber()
        {
            var sub = CreateSubscriber(TestActor);
            _bus.Unsubscribe(sub, _classifier).Should().BeFalse();
            DisposeSubscriber(Sys, sub);
        }

        [Fact]
        public void ActorEventBus_must_not_allow_to_subscribe_same_subscriber_to_same_channel_twice()
        {
            _bus.Subscribe(_subscriber, _classifier).Should().BeTrue();
            _bus.Subscribe(_subscriber, _classifier).Should().BeFalse();
            _bus.Unsubscribe(_subscriber, _classifier).Should().BeTrue();
        }

        [Fact]
        public void ActorEventBus_must_not_allow_to_unsubscribe_same_subscriber_from_the_same_channel_twice()
        {
            _bus.Subscribe(_subscriber, _classifier).Should().BeTrue();
            _bus.Unsubscribe(_subscriber, _classifier).Should().BeTrue();
            _bus.Unsubscribe(_subscriber, _classifier).Should().BeFalse();
        }

        [Fact]
        public void ActorEventBus_must_allow_to_add_multiple_subscribers()
        {
            var subscribers = Enumerable.Range(0, 10).Select(_ => CreateSubscriber(TestActor)).ToList();
            var events = CreateEvents(10);
            var classifiers = events.Select(GetClassifierFor).ToList();

            subscribers.Zip(classifiers, Tuple.Create).ForEach(t => _bus.Subscribe(t.Item1, t.Item2).Should().BeTrue());
            subscribers.Zip(classifiers, Tuple.Create).ForEach(t => _bus.Unsubscribe(t.Item1, t.Item2).Should().BeTrue());

            subscribers.ForEach(c => DisposeSubscriber(Sys, c));
        }

        [Fact]
        public void ActorEventBus_must_publishing_events_without_any_subscribers_shouldnot_be_a_problem()
        {
            _bus.Publish(_event);
        }

        [Fact]
        public void ActorEventBus_must_publish_to_the_only_subscriber()
        {
            _bus.Subscribe(_subscriber, _classifier);
            _bus.Publish(_event);
            ExpectMsg(_event);
            ExpectNoMsg(1.Seconds());
            _bus.Unsubscribe(_subscriber, _classifier);
        }

        [Fact]
        public void ActorEventBus_must_publish_to_the_only_subscriber_multiple_times()
        {
            _bus.Subscribe(_subscriber, _classifier);
            _bus.Publish(_event);
            _bus.Publish(_event);
            _bus.Publish(_event);
            ExpectMsg(_event);
            ExpectMsg(_event);
            ExpectMsg(_event);
            ExpectNoMsg(1.Seconds());
            _bus.Unsubscribe(_subscriber, _classifier);
        }

        [Fact]
        public void ActorEventBus_must_publish_the_given_event_to_all_intended_subscribers()
        {
            var range = Enumerable.Range(0, 10).ToList();
            var subscribers = range.Select(c => CreateSubscriber(TestActor)).ToList();
            subscribers.ForEach(s => _bus.Subscribe(s, _classifier).Should().BeTrue());
            _bus.Publish(_event);
            range.ForEach(_ => ExpectMsg(_event));
            subscribers.ForEach(s =>
            {
                _bus.Unsubscribe(s, _classifier).Should().BeTrue();
                DisposeSubscriber(Sys, s);
            });
        }

        [Fact]
        public void ActorEventBus_must_not_publish_the_given_event_to_any_other_subscribers_than_the_intended_ones()
        {
            var otherSubscriber = CreateSubscriber(TestActor);
            var otherClassifier = GetClassifierFor(_events.Drop(1).First());
            _bus.Subscribe(_subscriber, _classifier);
            _bus.Subscribe(otherSubscriber, otherClassifier);
            _bus.Publish(_event);
            ExpectMsg(_event);
            _bus.Unsubscribe(_subscriber, _classifier);
            _bus.Unsubscribe(otherSubscriber, otherClassifier);
            ExpectNoMsg(1.Seconds());
        }

        [Fact]
        public void ActorEventBus_must_not_publish_event_to_former_subscriber()
        {
            _bus.Subscribe(_subscriber, _classifier);
            _bus.Unsubscribe(_subscriber, _classifier);
            _bus.Publish(_event);
            ExpectNoMsg(1.Seconds());
        }

        [Fact]
        public void ActorEventBus_must_cleanup_subscribers()
        {
            DisposeSubscriber(Sys, _subscriber);
        }

        [Fact]
        public void ActorEventBus_must_unsubscribe_subscriber_when_it_terminates()
        {
            var a1 = CreateSubscriber(Sys.DeadLetters);
            var subs = CreateSubscriber(TestActor);
            Func<int, Notification> m = i => new Notification(a1, i);
            var p = CreateTestProbe();
            Sys.EventStream.Subscribe(p.Ref, typeof(Debug));

            // TODO: JVM uses a1 instead of _classifier
            _bus.Subscribe(subs, _classifier);
            _bus.Publish(m(1));
            ExpectMsg(m(1));

            Watch(subs);
            subs.Tell(PoisonPill.Instance);
            ExpectTerminated(subs);
            ExpectUnsubscribedByUnsubscriber(p, subs);

            _bus.Publish(m(2));
            ExpectNoMsg(1.Seconds());

            DisposeSubscriber(Sys, subs);
            DisposeSubscriber(Sys, a1);
        }

        [Fact]
        public void ActorEventBus_must_keep_subscriber_even_if_its_subscription_actors_have_died()
        {
            // Deaths of monitored actors should not influence the subscription.
            // For example: one might still want to monitor messages classified to A
            // even though it died, and handle these in some way.
            var a1 = CreateSubscriber(Sys.DeadLetters);
            var subs = CreateSubscriber(TestActor);
            Func<int, Notification> m = i => new Notification(a1, i);

            // TODO: JVM uses a1 instead of _classifier
            _bus.Subscribe(subs, _classifier).Should().Be(true);

            _bus.Publish(m(1));
            ExpectMsg(m(1));

            Watch(a1);
            a1.Tell(PoisonPill.Instance);
            ExpectTerminated(a1);

            _bus.Publish(m(2));
            ExpectMsg(m(2));

            DisposeSubscriber(Sys, subs);
            DisposeSubscriber(Sys, a1);
        }

        [Fact]
        public void ActorEventBus_must_unregister_subscriber_only_after_it_unsubscribes_from_all_of_its_subscriptions()
        {
            var a1 = CreateSubscriber(Sys.DeadLetters);
            var a2 = CreateSubscriber(Sys.DeadLetters);
            var subs = CreateSubscriber(TestActor);
            Func<int, Notification> m1 = i => new Notification(a1, i);
            Func<int, Notification> m2 = i => new Notification(a2, i);

            var p = CreateTestProbe();
            Sys.EventStream.Subscribe(p.Ref, typeof(Debug));

            // TODO: JVM uses a1, a2 instead of _classifier
            _bus.Subscribe(subs, _classifier).Should().Be(true);
            _bus.Subscribe(subs, _classifier).Should().Be(true);

            _bus.Publish(m1(1));
            _bus.Publish(m2(1));
            ExpectMsg(m1(1));
            ExpectMsg(m2(1));

            _bus.Unsubscribe(subs, _classifier);
            _bus.Publish(m1(2));
            ExpectNoMsg(1.Seconds());
            _bus.Publish(m2(2));
            ExpectMsg(m2(2));

            _bus.Unsubscribe(subs, _classifier);
            ExpectUnregisterFromUnsubscriber(p, subs);
            _bus.Publish(m1(3));
            _bus.Publish(m2(3));
            ExpectNoMsg(1.Seconds());

            DisposeSubscriber(Sys, subs);
            DisposeSubscriber(Sys, a1);
            DisposeSubscriber(Sys, a2);
        }

        private IEnumerable<Notification> CreateEvents(int numberOfEvents)
        {
            return Enumerable.Range(0, numberOfEvents).Select(c => new Notification(CreateTestProbe().Ref, c));
        }

        private IActorRef CreateSubscriber(IActorRef pipeTo)
        {
            return Sys.ActorOf(Props.Create(() => new TestActorWrapperActor(pipeTo)));
        }

        private Type GetClassifierFor(Notification @event)
        {
            this._bus.Ge
            // TODO: not sure here
            return @event.Ref.GetType();
        }

        private void DisposeSubscriber(ActorSystem sys, IActorRef subscriber)
        {
            sys.Stop(subscriber);
        }

        private void ExpectUnregisterFromUnsubscriber(TestProbe p, IActorRef a)
        {
            string expectedMessage = $"unregistered watch of {a} in {_bus}";
            p.FishForMessage(message =>
            {
                var debug = message as Debug;
                if (debug?.Message != null && debug.Message.Equals(expectedMessage))
                {
                    return true;
                }

                return false;
            }, 1.Seconds(), hint: expectedMessage);
        }

        private void ExpectUnsubscribedByUnsubscriber(TestProbe p, IActorRef a)
        {
            string expectedMessage = $"actor {a} has terminated, unsubscribing it from {_bus}";
            p.FishForMessage(message =>
            {
                var debug = message as Debug;
                if (debug?.Message != null && debug.Message.Equals(expectedMessage))
                {
                    return true;
                }

                return false;
            }, 1.Seconds(), hint: expectedMessage);
        }
    }
}
