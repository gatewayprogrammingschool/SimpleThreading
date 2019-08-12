using System;

namespace GPS.SimpleThreading.Blocks
{
    public class NotLockedException : Exception
    {
        public NotLockedException()
            : base("ThreadBlock is not locked.")
        {
        }

        public NotLockedException(string message) : base(message)
        {
        }

        public NotLockedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public NotLockedException(Exception innerException)
            : base("ThreadBlock is not locked.", innerException)
        {
        }
    }
}
