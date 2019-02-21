using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using GPS.SimpleThreading.Exceptions;
using GPSThreadFactory = GPS.SimpleThreading.Management.ThreadFactory;

namespace GPS.SimpleThreading.Blocks
{
    /// <summary>
    /// Parallel thread block class that provides for
    /// thread warmup, execution, and continuation.
    /// </summary>
    /// <remarks>
    /// ## Features
    /// * Allows capture of results of thread executions
    /// * Allows warmup action per data item before spawning thread
    /// * Allows continuation action per data item after executing thread
    /// * Allows continuation of the entire set
    /// </remarks>
    public sealed partial class ThreadBlock<TDataItem, TResult>
    {
        private readonly ConcurrentBag<DataResultPair> _results =
            new ConcurrentBag<DataResultPair>();

        private readonly ConcurrentQueue<TDataItem> _dataQueue =
            new ConcurrentQueue<TDataItem>();

        private bool _locked;
        private readonly Func<TDataItem, TResult> _threadProcessor;

        private CancellationTokenSource _tokenSource = new CancellationTokenSource();

        /// <summary>
        /// Constructor accepting the action and block continuation.
        /// </summary>
        public ThreadBlock(
            Func<TDataItem, TResult> threadProcessor)
        {
            _threadProcessor = threadProcessor;
        }

        /// <summary>
        ///  Signals the cancellation token source to cancel the batch.
        /// </summary>
        public void Stop()
        {
            if (_tokenSource != null && _tokenSource.Token.CanBeCanceled)
            {
                _tokenSource.Cancel();
            }
        }

        /// <summary>
        /// Add single data item.
        /// </summary>
        public void Add(TDataItem item)
        {
            if (!_locked) _dataQueue.Enqueue(item);
        }

        /// <summary>
        /// Adds range of data items from an IEnumerable
        /// </summary>
        public void AddRange(IEnumerable<TDataItem> collection)
        {
            Parallel.ForEach(collection, Add);
        }

        /// <summary>
        /// Adds range of data items from an ICollection.
        /// </summary>
        public void AddRange(ICollection<TDataItem> collection)
        {
            Parallel.ForEach(collection, Add);
        }

        /// <summary>
        /// Adds range of data items from an IProducerConsumerCollection.
        /// </summary>
        public void AddRange(IProducerConsumerCollection<TDataItem> collection)
        {
            Parallel.ForEach(collection, Add);
        }

        /// <summary>
        /// Maximum number of concurrent threads (default = 1).
        /// </summary>
        public int MaxDegreeOfParallelism { get; private set; } = 1;

        /// <summary>
        /// Locks the data of the block, allowing processing.
        /// </summary>
        public void LockList()
        {
            _locked = true;
        }

        /// <summary>
        /// Clears the remaining items from the Queue
        /// </summary>
        public void ClearList()
        {
            if (!_locked)
            {
                while (_dataQueue.Any())
                {
                    _dataQueue.TryDequeue(out TDataItem output);
                }
            }
        }

        private bool _continuous = false;

        /// <summary>
        /// Begins execution asynchronously, allowing for 
        /// more data to be added.
        /// </summary>
        /// <param name="cancelTokenSource"></param>
        /// <param name="maxDegreeOfParallelism"></param>
        /// <param name="warmupItem"></param>
        /// <param name="Action<Task"></param>
        /// <param name="data"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public Task ExecuteContinuous(
            CancellationTokenSource cancelTokenSource,
            int maxDegreeOfParallelism = -1,
            int threadTimeout = -1
        )
        {
            _continuous = true;
            var task = new TaskFactory().StartNew(() =>
                Executor(cancelTokenSource,
                        maxDegreeOfParallelism,
                        false, threadTimeout));

            return task;
        }

        /// <summary>
        /// Begins execution asynchronously, allowing for 
        /// more data to be added.
        /// </summary>
        /// <param name="maxDegreeOfParallelism"></param>
        /// <param name="warmupItem"></param>
        /// <param name="Action<Task"></param>
        /// <param name="data"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public Task ExecuteContinuous(
            int maxDegreeOfParallelism = -1,
            int threadTimeout = -1
        )
        {
            _continuous = true;
            var task = new TaskFactory().StartNew(() =>
                Executor(new CancellationTokenSource(),
                        maxDegreeOfParallelism,
                        false, threadTimeout));

            return task;
        }

        /// <summary>
        /// Execution without a Cancellation Token
        /// </summary>
        /// <param name="maxDegreeOfParallelism"></param>
        /// <param name="warmupItem"></param>
        /// <param name="Action<Task"></param>
        /// <param name="data"></param>
        /// <param name="result"></param>
        public void Execute(
            int maxDegreeOfParallelism = -1,
            bool requireLock = true,
            int threadTimeout = -1
        )
        {
            _continuous = false;
            Executor(new CancellationTokenSource(),
                maxDegreeOfParallelism,
                requireLock, threadTimeout);
        }

        /// <summary>
        /// Executes the action over the set of data.
        /// </summary>
        /// <param name="cancelTokenSource"></param>
        /// <param name="maxDegreeOfParallelism"></param>
        /// <param name="warmupItem"></param>
        /// <param name="Action<Task"></param>
        /// <param name="data"></param>
        /// <param name="result"></param>
        public void Execute(
            CancellationTokenSource cancelTokenSource,
            int maxDegreeOfParallelism = -1,
            bool requireLock = true,
            int threadTimeout = -1)
        {
            _continuous = false;

            Executor(
                cancelTokenSource,
                maxDegreeOfParallelism,
                requireLock, threadTimeout
            );
        }

        public bool IsRunning { get; private set; }

        private void Executor(CancellationTokenSource cancelTokenSource,
            int maxDegreeOfParallelism = -1,
            bool requireLock = true,
            int threadTimeout = -1)
        {
            if (IsRunning)
            {
                throw new AlreadyRunningException();
            }

            if (!_locked && requireLock)
            {
                throw new NotLockedException();
            }

            if (cancelTokenSource != null)
            {
                _tokenSource = cancelTokenSource;
            }

            if (maxDegreeOfParallelism == -1)
            {
                maxDegreeOfParallelism = MaxDegreeOfParallelism;
            }

            if (maxDegreeOfParallelism < 1)
            {
                throw new ArgumentOutOfRangeException(
                    "Must supply positive value for either " +
                    $"{nameof(maxDegreeOfParallelism)} or " +
                    $"this.{nameof(MaxDegreeOfParallelism)}.");
            }

            var padLock = new object();
            var allTasks = new Dictionary<TDataItem, Task>();

            int depth = 0;

            var continueOn = true;

            while (continueOn && !_tokenSource.IsCancellationRequested)
            {
                IsRunning = true;

                if (cancelTokenSource.IsCancellationRequested)
                {
                    continueOn = false;
                    break;
                }

                while (continueOn && _dataQueue.Count == 0
                    && !cancelTokenSource.IsCancellationRequested)
                {
                    System.Threading.Thread.Sleep(1);
                }

                if (cancelTokenSource.IsCancellationRequested)
                {
                    continueOn = false;
                    break;
                }

                while (continueOn && _dataQueue.Count > 0
                    && !cancelTokenSource.IsCancellationRequested)
                {
                    _dataQueue.TryDequeue(out TDataItem item);

                    DataWarmup?.Invoke(item);

                    var thread = new GPSThreadFactory().NewUnScopedFunctionThread<TDataItem, TResult>(
                        this._threadProcessor, 
                        apartmentState: ApartmentState.MTA, 
                        threadName: "Thread Block"
                    );

                    thread.Task.ContinueWith((resultTask, data) =>
                    {
                        if (!resultTask.IsCanceled)
                        {
                            var returnValue = new DataResultPair(resultTask, (TDataItem)data, resultTask.Result);

                            _results.Add(returnValue);

                            DataProcessed?.Invoke(returnValue);
                        }
                        lock (padLock)
                        {
                            depth--;
                        }
                    }, item);

                    int d = 0;
                    lock (padLock)
                    {
                        d = depth;
                    }

                    while (d >= maxDegreeOfParallelism)
                    {
                        if (cancelTokenSource.IsCancellationRequested)
                        {
                            continueOn = false;
                            break;
                        }

                        System.Threading.Thread.Sleep(1);
                        lock (padLock)
                        {
                            d = depth;
                        }
                    }

                    if (continueOn)
                    {                        
                        thread.StartSync(item);

                        lock (padLock)
                        {
                            depth++;
                        }
                    }
                }


                if (!cancelTokenSource.IsCancellationRequested)
                {
                    var dd = 0;

                    lock (padLock)
                    {
                        dd = depth;
                    }

                    while (dd > 0)
                    {
                        if (cancelTokenSource.IsCancellationRequested)
                        {
                            break;
                        }

                        Thread.Sleep(1);
                        lock (padLock)
                        {
                            dd = depth;
                        }
                    }

                    if (!cancelTokenSource.IsCancellationRequested && _continuous)
                    {
                        EmptyQueue?.Invoke();
                    }
                }


                if (_tokenSource.IsCancellationRequested)
                {
                    continueOn = false;
                    BatchCancelled?.Invoke();
                }

                continueOn = _continuous;
            }

            IsRunning = false;

            BatchFinished?.Invoke(Results);
        }

        /// <summary>
        /// Point-in-time results providing a stable result set
        /// for processing results as the block runs.
        /// </summary>
        public IEnumerable<DataResultPair> Results
        {
            get
            {
                var resultList = _results.ToList();

                if(resultList.Any())
                {
                    foreach(var result in resultList)
                    {
                        yield return result;
                    }
                }
            }
        }

        /// <summary>
        /// Triggered with the the queue becomes empty.
        /// </summary>
        public delegate void EmptyQueueHandler();
        public event EmptyQueueHandler EmptyQueue;

        /// <summary>
        /// Triggered when preparing to process the data.
        /// </summary>
        /// <param name="data">Data to be prepared.</param>
        public delegate void DataWarmupHandler(TDataItem data);
        public event DataWarmupHandler DataWarmup;

        /// <summary>
        /// Triggered when All data has been processed.
        /// </summary>
        /// <param name="resultPair">Processed data.</param>
        public delegate void DataProcessedHandler(DataResultPair resultPair);
        public event DataProcessedHandler DataProcessed;

        /// <summary>
        /// Triggered when the cancellation token is triggered.
        /// </summary>
        public delegate void BatchCancelledHandler();
        public event BatchCancelledHandler BatchCancelled;

        /// <summary>
        /// Triggered when the batch has finished.
        /// </summary>
        /// <param name="results">Enumerable of the results.</param>
        public delegate void BatchFinishedHandler(IEnumerable<DataResultPair> results);
        public event BatchFinishedHandler BatchFinished;
    }
}
