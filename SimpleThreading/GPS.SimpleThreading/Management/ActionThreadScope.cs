using System;
using System.Threading;
using System.Threading.Tasks;

namespace GPS.SimpleThreading.Management
{
    public sealed class ActionThreadScope<TData> : 
        GPS.SimpleThreading.Management.ThreadScopeBase<ActionThreadScopeWrapper<TData>>, IDisposable
    {
        public ActionThreadScope(CancellationTokenSource token = null) : base(token)
        {
        }

        public ActionThreadScopeWrapper<TData> AddThread(Thread thread)
        {
            lock (this)
            {
                if (!IsCancelled)
                {
                    var actionWrapper = new ActionThreadScopeWrapper<TData>(thread, _counter++, Token);

                    if(_threads.TryAdd(actionWrapper.ID, actionWrapper))
                    {
                        return actionWrapper;
                    }
                }

                return null;
            }
        }

        public Task AddAndStartThread(Thread thread, TData data, int timeout = -1)
        {
            var wrapper = AddThread(thread);

            return wrapper.GetTask(data, timeout);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    CancelAll();

                    foreach (var item in _threads.Keys)
                    {
                        _threads.TryRemove(item, out ActionThreadScopeWrapper<TData> thread);
                        thread?.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
