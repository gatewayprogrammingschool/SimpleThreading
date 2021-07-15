using System;

namespace GPS.SimpleThreading.Blocks
{
    public class LockedException : Exception
    {
        public LockedException()
            : base("ThreadBlock is locked.")
        {
        }

        public LockedException(string message) : base(message)
        {
        }

        public LockedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public LockedException(Exception innerException)
            : base("ThreadBlock is locked.", innerException)
        {
        }
    }
}