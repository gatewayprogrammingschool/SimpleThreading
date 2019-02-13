using System;
using System.Threading;
using System.Threading.Tasks;

namespace GPS.SimpleThreading.Management
{
    public sealed class FunctionThreadScope<TData, TResult> : 
        ThreadScopeBase<FunctionThreadScopeWrapper<TData, TResult>>, IDisposable
    {
        public FunctionThreadScope(CancellationTokenSource token = null) : base(token)
        {
        }
        
        public void AddThread(FunctionThread<TData, TResult> thread)
        {
            lock (this)
            {
                if (!IsCancelled)
                {
                    var wrapper = new FunctionThreadScopeWrapper<TData, TResult>(thread, _counter++, Token);

                    _threads.TryAdd(wrapper.ID, wrapper);
                }
            }
        }

        public Task AddAndStartThread(FunctionThread<TData, TResult> thread, TData data)
        {
            AddThread(thread);

            return thread.Start(data);
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

                    lock (this)
                    {
                        foreach (var item in _threads.Keys)
                        {
                            _threads.TryRemove(item, out FunctionThreadScopeWrapper<TData, TResult> thread);
                            thread?.Dispose();
                       }
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
