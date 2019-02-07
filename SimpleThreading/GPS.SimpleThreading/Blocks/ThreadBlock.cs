using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
    public sealed class ThreadBlock<TData, TResult>
    {
        private readonly ConcurrentDictionary<Task, (TData data, TResult result)?> _results =
            new ConcurrentDictionary<Task, (TData data, TResult result)?>();

        private readonly ConcurrentQueue<TData> _baseQueue =
            new ConcurrentQueue<TData>();

        private bool _locked;
        private readonly Func<TData, TResult> _threadProcessor;
        private readonly Action<ICollection<(TData data, TResult result)?>> _blockContinuation;

        /// <summary>
        /// Constructor accepting the action and block continuation.
        /// </summary>
        public ThreadBlock(
            Func<TData, TResult> threadProcessor,
            Action<ICollection<(TData data, TResult result)?>> blockContinuation = null)
        {
            _threadProcessor = threadProcessor;
            _blockContinuation = blockContinuation;
        }

        /// <summary>
        /// Add single data item.
        /// </summary>
        public void Add(TData item)
        {
            if (!_locked) _baseQueue.Enqueue(item);
        }

        /// <summary>
        /// Adds range of data items from an IEnumerable
        /// </summary>
        public void AddRange(IEnumerable<TData> collection)
        {
            Parallel.ForEach(collection, Add);
        }

        /// <summary>
        /// Adds range of data items from an ICollection.
        /// </summary>
        public void AddRange(ICollection<TData> collection)
        {
            Parallel.ForEach(collection, Add);
        }

        /// <summary>
        /// Adds range of data items from an IProducerConsumerCollection.
        /// </summary>
        public void AddRange(IProducerConsumerCollection<TData> collection)
        {
            Parallel.ForEach(collection, Add);
        }

        /// <summary>
        /// Maximum number of concurrent threads (default = 1).
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = 1;

        // /// <summary>
        // /// Removes a data item from the block.
        // /// </summary>
        // public bool Remove(TData item)
        // {
        //     TData itemToRemove;

        //     if (!_locked)
        //         return _baseQueue.(out itemToRemove);

        //     return false;
        // }

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
                while (_baseQueue.Any())
                {
                    _baseQueue.TryDequeue(out TData output);
                }
            }
        }

        /// <summary>
        /// Begins execution asynchronously, allowing for 
        /// more data to be added.
        /// </summary>
        /// <param name="cancel"></param>
        /// <param name="maxDegreeOfParallelism"></param>
        /// <param name="warmupItem"></param>
        /// <param name="Action<Task"></param>
        /// <param name="data"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public Task ExecuteContinuous(
            CancellationTokenSource cancel,
            int maxDegreeOfParallelism = -1,
            Action<TData> warmupItem = null,
            Action<Task, (TData data, TResult result)?> threadContinuation = null
        )
        {
            var task = new TaskFactory().StartNew(() =>
                Execute(cancel,
                        maxDegreeOfParallelism,
                        warmupItem,
                        threadContinuation,
                        false));

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
            Action<TData> warmupItem = null,
            Action<Task, (TData data, TResult result)?> threadContinuation = null
        )
        {
            var task = new TaskFactory().StartNew(() =>
                Execute(new CancellationTokenSource(),
                        maxDegreeOfParallelism,
                        warmupItem,
                        threadContinuation,
                        false));

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
            Action<TData> warmupItem = null,
            Action<Task, (TData data, TResult result)?> threadContinuation = null,
            bool requireLock = true
        )
        {
            Execute(new CancellationTokenSource(),
                maxDegreeOfParallelism,
                warmupItem,
                threadContinuation,
                requireLock);
        }

        /// <summary>
        /// Executes the action over the set of data.
        /// </summary>
        /// <param name="cancel"></param>
        /// <param name="maxDegreeOfParallelism"></param>
        /// <param name="warmupItem"></param>
        /// <param name="Action<Task"></param>
        /// <param name="data"></param>
        /// <param name="result"></param>
        public void Execute(
            CancellationTokenSource cancel,
            int maxDegreeOfParallelism = -1,
            Action<TData> warmupItem = null,
            Action<Task, (TData data, TResult result)?> threadContinuation = null,
            bool requireLock = true)
        {
            if (!_locked && (requireLock && _locked))
            {
                throw new NotLockedException();
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
            var allTasks = new Dictionary<TData, Task>();

            int depth = 0;

            var continueOn = true;

            while (continueOn)
            {
                if (cancel.IsCancellationRequested)
                {
                    continueOn = false;
                    break;
                }

                while (continueOn && _baseQueue.Count == 0)
                {
                    System.Threading.Thread.Sleep(1);

                    if (cancel.IsCancellationRequested)
                    {
                        continueOn = false;
                        break;
                    }
                }

                while (continueOn && _baseQueue.Count > 0)
                {
                    if (cancel.IsCancellationRequested)
                    {
                        continueOn = false;
                        break;
                    }

                    _baseQueue.TryDequeue(out TData item);

                    if (warmupItem != null) warmupItem(item);

                    var task = new Task<TResult>(() => _threadProcessor(item));

                    task.ContinueWith((resultTask, data) =>
                    {
                        if (!resultTask.IsCanceled)
                        {
                            var returnValue = ((TData, TResult)?)(data, resultTask.Result);

                            _results.AddOrUpdate(resultTask, returnValue,
                                (itemData, resultTaskResult) => resultTaskResult);

                            threadContinuation?.Invoke(resultTask, returnValue);
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
                        if (cancel.IsCancellationRequested)
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
                        task.Start(TaskScheduler.Current);

                        lock (padLock)
                        {
                            depth++;
                        }
                    }
                }
            }

            if (!cancel.IsCancellationRequested)
            {
                var dd = 0;

                lock (padLock)
                {
                    dd = depth;
                }

                while (dd > 0)
                {
                    if (cancel.IsCancellationRequested)
                    {
                        break;
                    }

                    Thread.Sleep(1);
                    lock (padLock)
                    {
                        dd = depth;
                    }
                }
            }

            _blockContinuation?.Invoke(_results.Values);
        }

        /// <summary>
        /// Point-in-time results providing a stable result set
        /// for processing results as the block runs.
        /// </summary>
        public ConcurrentDictionary<Task, (TData data, TResult result)?> Results
        {
            get
            {
                var results = new ConcurrentDictionary<Task, (TData data, TResult result)?>();

                foreach (var key in _results.Keys)
                {
                    var result = _results[key];
                    var value = key;

                    results.AddOrUpdate(value, result, (resultKey, resultValue) => resultValue);
                }

                return results;
            }
        }
    }
}
