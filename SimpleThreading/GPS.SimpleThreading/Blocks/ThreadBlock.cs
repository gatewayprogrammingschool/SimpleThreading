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
        private readonly ConcurrentDictionary<TData, (TData data, TResult result)?> _results =
            new ConcurrentDictionary<TData, (TData data, TResult result)?>();

        private readonly ConcurrentDictionary<TData, (TData data, Exception result)?> _exceptions =
            new ConcurrentDictionary<TData, (TData data, Exception result)?>();

        private readonly ConcurrentBag<TData> _baseList =
            new ConcurrentBag<TData>();

        private bool _locked;
        private readonly Func<TData, TResult> _action;
        private readonly Action<ICollection<(TData data, TResult result)?>> _continuation;

        /// <summary>
        /// Constructor accepting the action and block continuation.
        /// </summary>
        public ThreadBlock(
            Func<TData, TResult> action,
            Action<ICollection<(TData data, TResult result)?>> continuation = null)
        {
            _action = action;
            _continuation = continuation;
        }

        /// <summary>
        /// Add single data item.
        /// </summary>
        public void Add(TData item)
        {
            if (!_locked) _baseList.Add(item);
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

        /// <summary>
        /// Removes a data item from the block.
        /// </summary>
        public bool Remove(TData item)
        {
            TData itemToRemove;

            if (!_locked)
                return _baseList.TryTake(out itemToRemove);

            return false;
        }

        /// <summary>
        /// Locks the data of the block, allowing processing.
        /// </summary>
        public void LockList()
        {
            _locked = true;
        }

        /// <summary>
        /// Executes the action over the set of data.
        /// </summary>
        public void Execute(
            int maxDegreeOfParallelization = -1,
            Action<TData> warmupItem = null,
            Action<Task, (TData data, TResult result)?> threadContinuation = null)
        {
            if (!_locked) throw new NotLockedException();

            if (maxDegreeOfParallelization == -1)
            {
                maxDegreeOfParallelization = MaxDegreeOfParallelism;
            }

            if (maxDegreeOfParallelization < 1)
            {
                throw new ArgumentOutOfRangeException(
                    "Must supply positive value for either " +
                    $"{nameof(maxDegreeOfParallelization)} or " +
                    $"this.{nameof(MaxDegreeOfParallelism)}.");
            }

            var padLock = new object();
            var queue = new Queue<TData>(_baseList);
            var allTasks = new Dictionary<TData, Task>();

            int depth = 0;

            while (queue.Count > 0)
            {
                var item = queue.Dequeue();

                try
                {
                    if (warmupItem != null) warmupItem(item);

                    var task = new Task<TResult>(() => _action(item));

                    task.ContinueWith((resultTask, data) =>
                    {
                        var returnValue = ((TData, TResult)?)(data, resultTask.Result);

                        if (threadContinuation != null)
                        {
                            threadContinuation(resultTask, returnValue);
                        }

                        _results.AddOrUpdate(item, returnValue,
                            (itemData, resultTaskResult) => resultTaskResult);

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

                    while (d >= maxDegreeOfParallelization)
                    {
                        System.Threading.Thread.Sleep(1);
                        lock (padLock)
                        {
                            d = depth;
                        }
                    }

                    task.Start(TaskScheduler.Current);

                    lock (padLock)
                    {
                        depth++;
                    }
                }
                catch (Exception ex)
                {
                    _exceptions.AddOrUpdate(item, (item, ex), (itemData, exception) =>
                    {
                        return (item, ex);
                    });
                }
            }

            var dd = 0;

            lock (padLock)
            {
                dd = depth;
            }

            while (dd > 0)
            {
                Thread.Sleep(1);
                lock (padLock)
                {
                    dd = depth;
                }
            }

            _continuation?.Invoke(_results.Values);
        }

        /// <summary>
        /// Point-in-time results providing a stable result set
        /// for processing results as the block runs.
        /// </summary>
        public ConcurrentDictionary<TData, (TData data, TResult result)?> Results
        {
            get
            {
                var results = new ConcurrentDictionary<TData, (TData data, TResult result)?>();

                foreach (var key in _results.Keys)
                {
                    var result = _results[key];
                    var value = key;

                    results.AddOrUpdate(value, result, (resultKey, resultValue) => resultValue);
                }

                return results;
            }
        }

        public ConcurrentDictionary<TData, (TData data, Exception exception)?> Exceptions
        {
            get
            {
                var exceptions = new ConcurrentDictionary<TData, (TData data, Exception exception)?>();

                foreach(var key in _exceptions.Keys)
                {
                    var result = _exceptions[key];
                    var value = key;

                    exceptions.AddOrUpdate(value, result, (resultKey, resultValue) => resultValue);
                }

                return exceptions;
            }
        }
    }
}
