//-----------------------------------------------------------------------
// <copyright file="MessageSerializer.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Serialization;
using Google.Protobuf;

namespace Akka.Persistence.Serialization
{
    /// <summary>
    /// TBD
    /// </summary>
    public interface IMessage { }

    /// <summary>
    /// TBD
    /// </summary>
    public class MessageSerializer : Serializer
    {
        private Lazy<Information> TransportInformation { get; }

        private Lazy<Akka.Serialization.Serialization> Serialization { get; }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="system">TBD</param>
        public MessageSerializer(ExtendedActorSystem system)
            : base(system)
        {
            TransportInformation = new Lazy<Information>(() =>
            {
                var address = system.Provider.DefaultAddress;
                return address.HasLocalScope
                    ? null
                    : new Information() { Address = address, System = system };
            });

            Serialization = new Lazy<Akka.Serialization.Serialization>(() =>
            {
                return system.Serialization;
            });
        }

        /// <summary>
        /// TBD
        /// </summary>
        public override bool IncludeManifest => true;

        /// <summary>
        /// Serializes persistent messages. Delegates serialization of a persistent
        /// message's payload to a matching `akka.serialization.Serializer`.
        /// </summary>
        /// <param name="obj">TBD</param>
        /// <exception cref="ArgumentException">TBD</exception>
        /// <returns>TBD</returns>
        public override byte[] ToBinary(object obj)
        {
            switch (obj)
            {
                case IPersistentRepresentation p:
                    return PersistentMessageBuilder(p).ToByteArray();
                case AtomicWrite a:
                    return AtomicWriteBuilder(a).ToByteArray();
                case AtLeastOnceDeliverySnapshot a:
                    return AtLeastOnceDeliverySnapshotBuilder(a).ToByteArray();
                default:
                    throw new ArgumentException($"Can't serialize object of type {obj.GetType()}");
            }
        }

        /// <summary>
        /// Deserializes persistent messages. Delegates deserialization of a persistent
        /// message's payload to a matching `akka.serialization.Serializer`.
        /// </summary>
        /// <param name="bytes">TBD</param>
        /// <param name="type">TBD</param>
        /// <exception cref="ArgumentException">TBD</exception>
        /// <returns>TBD</returns>
        public override object FromBinary(byte[] bytes, Type type)
        {
            if (type == null || type == typeof(Persistent) || type == typeof(IPersistentRepresentation))
                return PersistentMessageFrom(bytes);
            if (type == typeof(AtomicWrite))
                return AtomicWriteFrom(bytes);
            if (type == typeof(AtLeastOnceDeliverySnapshot))
                return SnapshotFrom(bytes);

            throw new ArgumentException(typeof(MessageSerializer) + " cannot deserialize object of type " + type);
        }

        //
        // ToBinary helpers
        //

        private Protobuf.Msg.PersistentMessage PersistentMessageBuilder(IPersistentRepresentation p)
        {
            var message = new Protobuf.Msg.PersistentMessage();

            if (p.Sender != ActorRefs.NoSender)
                message.Sender = Akka.Serialization.Serialization.SerializedActorPath(p.Sender);

            message.PersistenceId = p.PersistenceId;
            message.Manifest = p.Manifest;
            message.Payload = PersistentPayloadBuilder(p.Payload);
            message.SequenceNr = p.SequenceNr;
            message.Deleted = p.IsDeleted;
            message.WriterUuid = p.WriterGuid;
               
            return message;
        }

        private Protobuf.Msg.PersistentPayload PersistentPayloadBuilder(object payload)
        {
            Protobuf.Msg.PersistentPayload PayloadBuilder()
            {
                var serializer = Serialization.Value.FindSerializerFor(payload);
                var persistentPayload = new Protobuf.Msg.PersistentPayload();

                if (serializer is SerializerWithStringManifest ser2)
                {
                    var manifest = ser2.Manifest(payload);
                    if (manifest != Persistent.Undefined)
                    {
                        persistentPayload.PayloadManifest = ByteString.CopyFromUtf8(manifest);
                    }
                }
                else
                {
                    if (serializer.IncludeManifest)
                    {
                        persistentPayload.PayloadManifest = ByteString.CopyFromUtf8(TypeQualifiedNameForManifest(payload.GetType()));
                    }
                }

                persistentPayload.Payload = ByteString.CopyFrom(serializer.ToBinary(payload));
                persistentPayload.SerializerId = serializer.Identifier;
                return persistentPayload;
            }

            if (TransportInformation.Value != null)
            {
                return Akka.Serialization.Serialization.SerializeWithTransport(
                    TransportInformation.Value.System,
                    TransportInformation.Value.Address,
                    () => PayloadBuilder());
             }
            else
            {
                return PayloadBuilder();
            }
        }

        private Protobuf.Msg.AtomicWrite AtomicWriteBuilder(AtomicWrite aw)
        {
            var atomicWrite = new Protobuf.Msg.AtomicWrite();

            foreach (var p in (IEnumerable<IPersistentRepresentation>)aw.Payload)
            {
                atomicWrite.Payload.Add(PersistentMessageBuilder(p));
            }

            return atomicWrite;
        }

        private Protobuf.Msg.AtLeastOnceDeliverySnapshot AtLeastOnceDeliverySnapshotBuilder(AtLeastOnceDeliverySnapshot snap)
        {
            var atLeastOnceDeliverySnapshot = new Protobuf.Msg.AtLeastOnceDeliverySnapshot();
            atLeastOnceDeliverySnapshot.CurrentDeliveryId = snap.CurrentDeliveryId;

            foreach (var unconfirmed in snap.UnconfirmedDeliveries)
            {
                var unconfirmedBuilder = new Protobuf.Msg.AtLeastOnceDeliverySnapshot.Types.UnconfirmedDelivery();
                unconfirmedBuilder.DeliveryId = unconfirmed.DeliveryId;
                unconfirmedBuilder.Destination = unconfirmed.Destination.ToString();
                unconfirmedBuilder.Payload = PersistentPayloadBuilder(unconfirmed.Message);

                atLeastOnceDeliverySnapshot.UnconfirmedDeliveries.Add(unconfirmedBuilder);
            }

            return atLeastOnceDeliverySnapshot;
        }

        //
        // FromBinary helpers
        //

        private IPersistentRepresentation PersistentMessageFrom(byte[] bytes)
        {
            var persistentMessage = Protobuf.Msg.PersistentMessage.Parser.ParseFrom(bytes);

            return PersistentMessageFrom(persistentMessage);
        }

        private IPersistentRepresentation PersistentMessageFrom(Protobuf.Msg.PersistentMessage persistentMessage)
        {
            return new Persistent(
                payload: PayloadFromProto(persistentMessage.Payload),
                sequenceNr: persistentMessage.SequenceNr,
                persistenceId: persistentMessage.PersistenceId,
                manifest: persistentMessage.Manifest,
                isDeleted: persistentMessage.Deleted,
                sender: !string.IsNullOrEmpty(persistentMessage.Sender) ? system.Provider.ResolveActorRef(persistentMessage.Sender) : null,
                writerGuid: persistentMessage.WriterUuid);
        }

        private object PayloadFromProto(Protobuf.Msg.PersistentPayload persistentPayload)
        {
            return Serialization.Value.Deserialize(
                persistentPayload.Payload.ToByteArray(),
                persistentPayload.SerializerId,
                persistentPayload.PayloadManifest.ToStringUtf8());
        }

        private AtomicWrite AtomicWriteFrom(byte[] bytes)
        {
            var persistentMessage = Protobuf.Msg.AtomicWrite.Parser.ParseFrom(bytes);

            return new AtomicWrite(persistentMessage.Payload.Select(e => PersistentMessageFrom(e)).ToImmutableList());
        }

        private AtLeastOnceDeliverySnapshot SnapshotFrom(byte[] bytes)
        {
            var snap = Protobuf.Msg.AtLeastOnceDeliverySnapshot.Parser.ParseFrom(bytes);

            var unconfirmedDeliveries = new UnconfirmedDelivery[snap.UnconfirmedDeliveries.Count];

            for (int i = 0; i < snap.UnconfirmedDeliveries.Count; i++)
            {
                var unconfirmed = snap.UnconfirmedDeliveries[i];
                var unconfirmedDelivery = new UnconfirmedDelivery(
                    deliveryId: unconfirmed.DeliveryId,
                    destination: ActorPath.Parse(unconfirmed.Destination),
                    message: PayloadFromProto(unconfirmed.Payload));
                unconfirmedDeliveries[i] = unconfirmedDelivery;
            }

            return new AtLeastOnceDeliverySnapshot(snap.CurrentDeliveryId, unconfirmedDeliveries);
        }
    }
}

