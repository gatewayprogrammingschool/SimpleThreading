using System;
using System.Threading;
using System.Threading.Tasks;

namespace GPS.SimpleThreading.Management
{
    public class ActionThreadScopeWrapper<TData> : IDisposable
    {
        public int ID { get; private set; } = 0;

        public CancellationTokenSource Token { get; private set; }
        public ManualResetEventSlim WaitHandle { get; private set; }

        public Thread Thread = null;

        public ActionThreadScopeWrapper(Thread thread, int id,
         CancellationTokenSource token = null)
        {
            ID = id;
            WaitHandle = new ManualResetEventSlim(false);
            Token = token ?? new CancellationTokenSource();

            Thread = thread;
            Thread.Name = $"Action {ID}";
        }

        public void Start()
        {
            Thread?.Start();
        }

        public void Start(TData data)
        {

            Thread?.Start(data);
        }

        public Task GetTask(TData data, int timeout)
        {
            var tcs = new TaskCompletionSource<object>();

            try
            {
                if (!WaitHandle.Wait(timeout, Token.Token))
                {
                    tcs.SetCanceled();
                }
                else
                {
                    tcs.SetResult(null);
                }
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }

            return tcs.Task;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {                    
                    Token.Cancel();
                    WaitHandle.Dispose();
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
