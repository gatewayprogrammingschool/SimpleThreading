using System;
using System.Threading;
using System.Threading.Tasks;

namespace GPS.SimpleThreading.Management
{
    public class FunctionThreadScopeWrapper<TData, TResult> : IDisposable
    {
        public int ID { get; private set; } = 0;

        public CancellationTokenSource Token { get; private set; }
        public ManualResetEventSlim WaitHandle { get; private set; }

        public FunctionThread<TData, TResult> FunctionThread = null;

        public FunctionThreadScopeWrapper(
            FunctionThread<TData, TResult> thread,
            int id,
            CancellationTokenSource token = null)
        {
            ID = id;
            WaitHandle = thread.WaitHandle;
            Token = token ?? new CancellationTokenSource();

            FunctionThread = thread;
            FunctionThread.Name = $"Function {id}";
        }

        public Task<TResult> Task => FunctionThread.Task;

        public TResult GetResult(TData data, int timeout) =>
            FunctionThread.StartResult(data, Token.Token, timeout);

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    FunctionThread.Dispose();
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
