using System;

namespace GPS.SimpleThreading.Exceptions
{
    [Serializable]
    public class AlreadyRunningException : Exception
    {
        public AlreadyRunningException() :
            base("ThreadBlock is already running.")
        { }

        public AlreadyRunningException(string message)
        : base(message) { }

        public AlreadyRunningException(
                string message, Exception inner)
            : base(message, inner) { }

        public AlreadyRunningException(Exception inner)
            : base("ThreadBlock is already running.", inner)
        {
        }

        protected AlreadyRunningException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
