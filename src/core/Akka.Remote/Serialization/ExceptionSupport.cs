//-----------------------------------------------------------------------
// <copyright file="WrappedPayloadSupport.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Globalization;
using System.Reflection;
using Akka.Actor;
using Akka.Util;
using Google.Protobuf;

namespace Akka.Remote.Serialization
{
    internal class ExceptionSupport
    {
        private static readonly TypeInfo ExceptionTypeInfo = typeof(Exception).GetTypeInfo();
        public const BindingFlags All = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private readonly FieldInfo _className;
        private readonly FieldInfo _innerException;
        private readonly FieldInfo _stackTraceString;
        private readonly FieldInfo _source;
        private readonly FieldInfo _message;

        public ExceptionSupport(ExtendedActorSystem system)
        {
            _className = ExceptionTypeInfo.GetField("_className", All);
            _innerException = ExceptionTypeInfo.GetField("_innerException", All);
            _message = ExceptionTypeInfo.GetField("_message", All);
            _source = ExceptionTypeInfo.GetField("_source", All);
            _stackTraceString = ExceptionTypeInfo.GetField("_stackTraceString", All);
        }

        public byte[] SerializeException(Exception exception)
        {
            return ExceptionToProto(exception).ToByteArray();
        }

        public Proto.Msg.ExceptionData ExceptionToProto(Exception exception)
        {
            var message = new Proto.Msg.ExceptionData();
            message.Source = exception.Source ?? "";
            message.Message = exception.Message;
            message.StackTrace = exception.StackTrace ?? "";
            if (exception.InnerException != null)
                message.Cause = ExceptionToProto(exception.InnerException);

            message.ClassName = exception.GetType().TypeQualifiedName();
            return message;
        }

        public Exception DeserializeException(byte[] bytes)
        {
            var proto = Proto.Msg.ExceptionData.Parser.ParseFrom(bytes);
            return ExceptionFromProto(proto);
        }

        public Exception ExceptionFromProto(Proto.Msg.ExceptionData proto)
        {
            var type = Type.GetType(proto.ClassName) ?? typeof(Exception);
            var exception = (Exception)Activator.CreateInstance(type);
            _source.SetValue(exception, proto.Source);
            _message.SetValue(exception, proto.Message);
            if (!string.IsNullOrEmpty(proto.StackTrace)) _stackTraceString.SetValue(exception, proto.StackTrace);
            if (proto.Cause != null) _innerException.SetValue(exception, ExceptionFromProto(proto.Cause));
            return exception;
        }
    }
}
