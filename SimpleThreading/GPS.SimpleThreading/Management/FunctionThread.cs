using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

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
            ILogger logger,
            Func<TData, TResult> function,
            ThreadPriority priority = ThreadPriority.Normal,
            ApartmentState apartmentState = ApartmentState.MTA,
            string threadName = "Unscoped")
        {
            _logger = logger;

            if (function == null)
            {
                throw new ArgumentNullException(nameof(function),
                    "The target function may not be null.");
            }

            _worker = new ThreadWorker(_logger, false);

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

        public Task<TResult> StartResultSync(TData data, int timeout = -1)
        {
            var token = new CancellationTokenSource().Token;

            return StartResultSync(data, token, timeout);
        }

        public Task<TResult> StartResultSync(TData data, CancellationToken token, int timeout = -1)
        {
            _taskCompletionSource = new TaskCompletionSource<TResult>();

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
                        _taskCompletionSource.TrySetException(Abort());
                        _taskCompletionSource.TrySetCanceled();
                        _taskCompletionSource.TrySetResult(default(TResult));
                        return _taskCompletionSource.Task;
                    }
                    catch (Exception ex)
                    {
                        _taskCompletionSource.TrySetException(ex);
                    }
                }
            }
            else
            {
                try
                {
                    _mre.Wait(token);
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        _taskCompletionSource.TrySetException(Abort());
                        _taskCompletionSource.TrySetCanceled();
                        _taskCompletionSource.TrySetResult(default(TResult));
                        return _taskCompletionSource.Task;
                    }
                    catch (Exception ex)
                    {
                        _taskCompletionSource.TrySetException(ex);
                    }
                }
                finally
                {
                    _logger.LogInformation($"Thread {_thread.Name} ended. Token was " +
                        $"{(token.IsCancellationRequested ? "" : " not")} cancelled.");
                }
            }

            _mre.Dispose();
            _taskCompletionSource.TrySetResult(_result);
            return _taskCompletionSource.Task;
        }

        public Exception Abort()
        {
            try
            {
                _thread.Abort();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Platform does not support aborting threads.");
                return ex;
            }

            return null;
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

        public Task<TResult> StartSync(TData data)
        {
            return StartWorker(data, true);
        }

        public Task<TResult> StartAsync(TData data)
        {
            return StartWorker(data, false);
        }

        private Task<TResult> StartWorker(TData data, bool synchronous)
        {
            void Completed(TResult result)
            {
                _taskCompletionSource.SetResult(result);
                _logger.LogInformation($"Task {_thread.Name} completed.");
            }

            try
            {
                var worker = new ThreadWorker(_logger, synchronous);

                worker.ThreadDone += Completed;

                worker.Run(new Tuple<Func<TData, TResult>, TData>(_function, data));
            }
            catch (Exception ex)
            {
                _taskCompletionSource.TrySetException(ex);
            }

            return Task;
        }

        private class ThreadWorker
        {
            ILogger _logger = null;
            private bool _synchronous;

            public ThreadWorker(ILogger logger, bool synchronous)
            {
                _logger = logger;
                _synchronous = synchronous;
            }
            public event ThreadDoneHandler ThreadDone;

            public void Run(object wrapped)
            {
                const string nullString = "<null>";
                var wrapper = wrapped as Tuple<Func<TData, TResult>, TData>;

                if (wrapper == null)
                {
                    var ex = new ArgumentException(
                        "Argument passed does not match the Function parameter type.",
                        nameof(wrapped));

                    _logger?.LogError(ex,
                        $"Wrapper was not of the correct type. Received {wrapper.GetType().FullName}");

                    throw ex;
                }

                if (_synchronous)
                {
                    _logger.LogInformation($"Function {wrapper.Item1.GetType().FullName} " +
                                            $"invoking with [{(wrapper.Item2?.ToString() ?? nullString)}]");

                    var result = wrapper.Item1(wrapper.Item2);

                    ThreadDone?.Invoke(result);
                }
                else
                {
                    var t = new Thread(DoAction);
                    t.Start(wrapped);
                }
            }

            private void DoAction(object wrapped)
            {
                Run(wrapped);
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