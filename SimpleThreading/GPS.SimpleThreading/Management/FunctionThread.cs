using System;
using System.Threading;
using System.Threading.Tasks;

namespace GPS.SimpleThreading.Management
{
    public class FunctionThread<TData, TResult> : IDisposable
    {
        private readonly ILogger _logger = null;
        private Func<TData, TResult> _function = null;
        private TResult _result;
        private Thread _thread;

        private ManualResetEventSlim _mre;
        public ManualResetEventSlim WaitHandle => _mre;

        private ThreadWorker _worker = null;

        public FunctionThread(
            Func<TData, TResult> function,
            ThreadPriority priority = ThreadPriority.Normal,
            ApartmentState apartmentState = ApartmentState.MTA,
            string threadName = "Unscoped")
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function),
                    "The target function may not be null.");
            }

            _worker = new ThreadWorker();

            _function = function;
            _thread = new Thread(_worker.Run);
            _thread.Name = threadName;
            _thread.SetApartmentState(apartmentState);
            _thread.Priority = priority;
        }

        private void ThreadHandled(TResult result)
        {
            _result = result;
            _mre.Set();
        }

        public TResult StartResult(TData data, CancellationToken token, int timeout = -1)
        {
            _mre = new ManualResetEventSlim(false);

            _worker.ThreadDone -= ThreadHandled;
            _worker.ThreadDone += ThreadHandled;

            _thread.Start(new Tuple<Func<TData, TResult>, TData>(_function, data));

            if (timeout > -1)
            {
                if (!_mre.Wait(timeout, token))
                {
                    try
                    {
                        _thread.Abort();
                    }
                    catch
                    {

                    }

                    return default(TResult);
                }
            }
            else
            {
                _mre.Wait(token);
            }

            _mre.Dispose();

            return _result;
        }

        public IAsyncResult StartAsync(TData data)
        {
            _worker.ThreadDone -= ThreadHandled;
            _worker.ThreadDone += ThreadHandled;

            var asyncResult = new ThreadAsyncResult(_thread);

            _thread.Start(new Tuple<Func<TData, TResult>, TData>(_function, data));

            return asyncResult;
        }

        public Task<TResult> Task
        {
            get
            {
                if (_taskCompletionSource == null)
                {
                    _taskCompletionSource = new TaskCompletionSource<TResult>();
                }

                return _taskCompletionSource.Task;
            }
        }

        public Task<TResult> Start(TData data)
        {
            void Completed(TResult result)
            {
                _taskCompletionSource.SetResult(result);
            }

            try
            {
                var worker = new ThreadWorker();

                worker.ThreadDone += Completed;

                worker.Run(new Tuple<Func<TData, TResult>, TData>(_function, data));
            }
            catch (Exception ex)
            {
                _taskCompletionSource.TrySetException(ex);
            }

            return _taskCompletionSource.Task;
        }

        private class ThreadAsyncResult : IAsyncResult, IDisposable
        {
            public object AsyncState => _result;

            public WaitHandle AsyncWaitHandle => _mre;

            public bool CompletedSynchronously => false;

            public bool IsCompleted { get; private set; }

            private Thread _thread = null;

            public TResult _result = default(TResult);

            private ManualResetEvent _mre = new ManualResetEvent(false);

            public ThreadAsyncResult(Thread thread)
            {
                _thread = thread;
            }

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        _mre.Dispose();
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

        private class ThreadWorker
        {
            public event ThreadDoneHandler ThreadDone;

            public void Run(object wrapped)
            {
                var wrapper = wrapped as Tuple<Func<TData, TResult>, TData>;
                
                if (wrapper == null)
                {
                    throw new ArgumentException(
                        "Argument passed does not match the Function parameter type.",
                        nameof(wrapped));
                }

                var result = wrapper.Item1((TData)wrapper.Item2);

                ThreadDone?.Invoke(result);

            }
        }

        private delegate void ThreadDoneHandler(TResult result);

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls
        private TaskCompletionSource<TResult> _taskCompletionSource;

        public string Name
        {
            get => _thread.Name;
            internal set => _thread.Name = value;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _thread.Abort();
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