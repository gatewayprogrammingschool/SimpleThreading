using System.Collections.Concurrent;
using System.Threading;

namespace GPS.SimpleThreading.Management
{
    public abstract class ThreadScopeBase<TScopeWrapper>
    {
        protected ConcurrentDictionary<int, TScopeWrapper> _threads = null;
        internal int _counter = 0;

        public CancellationTokenSource Token { get; private set; }

        public bool IsCancelled { get; private set; }

        public ThreadScopeBase(CancellationTokenSource token = null)
        {
            _threads = new ConcurrentDictionary<int, TScopeWrapper>();

            Token = token ?? new CancellationTokenSource();
        }

        public void CancelAll()
        {
            lock (this)
            {
                if (IsCancelled) return;

                IsCancelled = true;

                Token.Cancel();
            }
        }
        
        public bool TryRemoveThread(int id)
        {
            return _threads.TryRemove(id, out TScopeWrapper removed);
        }
    }
}
