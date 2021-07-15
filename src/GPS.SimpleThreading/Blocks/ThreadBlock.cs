using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
// ReSharper disable AccessToModifiedClosure
// ReSharper disable UnusedMember.Global

namespace GPS.SimpleThreading.Blocks
{
    /// <summary>
    ///     Parallel thread block class that provides for
    ///     thread warmup, execution, and continuation.
    /// </summary>
    /// <remarks>
    ///     ## Features
    ///     * Allows capture of results of thread executions
    ///     * Allows warmup action per data item before spawning thread
    ///     * Allows continuation action per data item after executing thread
    ///     * Allows continuation of the entire set
    /// </remarks>
    public sealed class ThreadBlock<TData, TResult>
    {
        private readonly Func<TData?, TResult?> _action;
        private readonly Func<TData?, Task<TResult?>> _asyncAction;
        private readonly Func<ICollection<(TData? data, TResult? result)?>, Task>? _asyncContinuation;
        private readonly Action<ICollection<(TData? data, TResult? result)?>>? _continuation;

        private readonly ConcurrentDictionary<TData?, (TData? data, Exception result)?> _exceptions = new();
        private readonly ConcurrentDictionary<TData?, (TData? data, TResult? result)?> _results = new();

        private bool _locked;

        private ConcurrentQueue<TData?> _queue = new();

        /// <summary>
        ///     Constructor accepting the action and block continuation.
        /// </summary>
        public ThreadBlock(
            Func<TData?, TResult?> action,
            Action<ICollection<(TData? data, TResult? result)?>>? continuation = null)
        {
            _action = action;
            _asyncAction = data =>
            {
                var result = action(data);
                return Task.FromResult(result);
            };

            _continuation = continuation;
            _asyncContinuation = tuples =>
            {
                continuation?.Invoke(tuples);
                return Task.CompletedTask;
            };
        }

        /// <summary>
        ///     Constructor accepting the asynchronous action and block continuation.
        /// </summary>
        public ThreadBlock(
            Func<TData?, Task<TResult?>> asyncAction,
            Func<ICollection<(TData? data, TResult? result)?>, Task>? asyncContinuation = null)
        {
            _asyncAction = asyncAction;
            _action = data => asyncAction.Invoke(data).GetAwaiter().GetResult();
            _asyncContinuation = asyncContinuation;
            _continuation = tuples => asyncContinuation?.Invoke(tuples).GetAwaiter().GetResult();
        }

        /// <summary>
        ///     Maximum number of concurrent threads (default = 1).
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = 1;

        /// <summary>
        ///     Point-in-time results providing a stable result set
        ///     for processing results as the block runs.
        /// </summary>
        public ConcurrentDictionary<TData?, (TData? data, TResult? result)?> Results
        {
            get
            {
                var snapshot = _results.ToArray();

                var results = new ConcurrentDictionary<TData?, (TData? data, TResult? result)?>();

                foreach (var kvp in snapshot)
                {
                    results.AddOrUpdate(kvp.Key, kvp.Value, (_, _) => kvp.Value);
                }

                return results;
            }
        }

        /// <summary>
        ///     Point-in-time exceptions providing a stable ConcurrentDictionary
        ///     for processing exceptions as the block runs.
        /// </summary>
        public ConcurrentDictionary<TData?, (TData? data, Exception exception)?> Exceptions
        {
            get
            {
                var snapshot = _exceptions.ToArray();

                var exceptions = new ConcurrentDictionary<TData?, (TData? data, Exception exception)?>();

                foreach (var kvp in snapshot)
                {
                    exceptions.AddOrUpdate(kvp.Key, kvp.Value, (_, _) => kvp.Value);
                }

                return exceptions;
            }
        }

        /// <summary>
        ///     Add single data item.
        /// </summary>
        public void Add(TData? item)
        {
            if (!_locked) _queue.Enqueue(item);
            else throw new LockedException();
        }

        /// <summary>
        ///     Adds range of data items from an IEnumerable
        /// </summary>
        public void AddRange(IEnumerable<TData?> collection)
        {
            if (!_locked) Parallel.ForEach(collection, _queue.Enqueue);
            else throw new LockedException();
        }

        /// <summary>
        ///     Adds range of data items from an IProducerConsumerCollection.
        /// </summary>
        public void AddRange(IProducerConsumerCollection<TData?> collection)
        {
            if (!_locked) Parallel.ForEach(collection, _queue.Enqueue);
            else throw new LockedException();
        }

        /// <summary>
        ///     Adds range of data items from an IEnumerable that preserves original order.
        /// </summary>
        public void OrderedAddRange(IEnumerable<TData?> collection)
        {
            if (!_locked) collection.ToList().ForEach(_queue.Enqueue);
            else throw new LockedException();
        }

        /// <summary>
        ///     Adds range of data items from an IProducerConsumerCollection that preserves original order.
        /// </summary>
        public void OrderedAddRange(IProducerConsumerCollection<TData?> collection)
        {
            if (!_locked) collection.ToList().ForEach(_queue.Enqueue);
            else throw new LockedException();
        }

        /// <summary>
        ///     Removes a data item from the block.
        /// </summary>
        public bool Remove(TData? item)
        {
            if (_locked) return false;

            var items = _queue.ToList();
            if (items.Remove(item)) _queue = new ConcurrentQueue<TData?>(items);

            return false;
        }

        /// <summary>
        ///     Locks the data of the block, allowing processing.
        /// </summary>
        public void LockList()
        {
            _locked = true;
        }

        /// <summary>
        ///     Executes the action over the set of data.
        /// </summary>
        public void Execute(
            int maxDegreeOfParallelism = -1,
            Action<TData?>? warmupItem = null,
            Action<Task, (TData? data, TResult? result)?>? threadContinuation = null,
            CancellationToken token = default)
        {
            var warmupItemAsync = new Func<TData?, Task>(item =>
            {
                warmupItem?.Invoke(item);
                return Task.CompletedTask;
            });

            var threadContinuationAsync = new Func<Task, (TData? data, TResult? result)?, Task>((task, tuple) =>
            {
                threadContinuation?.Invoke(task, tuple);
                return Task.CompletedTask;
            });

            ExecuteAsync(maxDegreeOfParallelism, warmupItemAsync, threadContinuationAsync, token).GetAwaiter().GetResult();
        }

        public async Task<IEnumerable<TResult?>> ExecuteAsync(
            int maxDegreeOfParallelism = -1,
            Func<TData?, Task>? warmupItem = null,
            Func<Task, (TData? data, TResult? result)?, Task>? threadContinuation = null,
            CancellationToken token = default)
        {
            ConcurrentBag<Task> tasks = new();

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

            while (_queue.Count > 0)
            {
                TData? item = default;
                Task<TResult?>? t = default;

                try
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    if (!_queue.TryDequeue(out item))
                    {
                        if (_locked)
                        {
                            break;
                        }

                        await Task.Delay(TimeSpan.FromSeconds(1), token);

                        continue;
                    }

                    if (warmupItem is not null)
                    {
                        await warmupItem(item);
                    }

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            t = _asyncAction(item);
                            await t;
                        }
                        catch (Exception ex)
                        {
                            _exceptions.AddOrUpdate(item, (item, ex), (_, _) => (item, ex));
                        }

                        if (t is null) return default;

                        try
                        {
                            await Continuation(t, item);
                        }
                        catch (Exception ex)
                        {
                            _exceptions.AddOrUpdate(item, (item, ex), (_, _) => (item, ex));
                        }

                        Debug.WriteLine($"{t.Id}: Finished: {t.Result}");
                        return t.Result;
                    }, token));

                    Debug.WriteLine($"tasks.Count: {tasks.Count}");

                    var isRunning = tasks.Where(tsk => tsk.Status is TaskStatus.Running or TaskStatus.WaitingForActivation).ToArray();

                    Debug.WriteLine($"isRunning.Length: {isRunning.Length}");

                    if (isRunning.Length >= maxDegreeOfParallelism)
                    {
                        Task.WaitAny(isRunning, token);
                    }

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _exceptions.AddOrUpdate(item, (item, ex), (_, _) => (item, ex));
                }
            }

            try
            {
                var isRunning = tasks.Where(tsk => tsk.Status is TaskStatus.Running or TaskStatus.WaitingForActivation).ToArray();
                Debug.WriteLine($"isRunning.Length: {isRunning.Length}");

                if (isRunning.Length > 0)
                {
                    Task.WaitAll(isRunning, token);
                }

                Debug.WriteLine("Completed all tasks.");

                if (_asyncContinuation is not null)
                {
                    await _asyncContinuation(_results.Values);
                }
            }
            catch (Exception ex)
            {
                _exceptions.AddOrUpdate(default, (default, ex), (_, _) => (default, ex));
            }

            return _results.Values.Select(r => r.Value.result);

            async Task Continuation(Task<TResult?> resultTask, TData? data)
            {
                if (resultTask.Exception == null)
                {
                    var returnValue = ((TData?, TResult?)?)(data, resultTask.Result);

                    if (threadContinuation is not null)
                    {
                        await threadContinuation(resultTask, returnValue);
                    }

                    _results.AddOrUpdate(data, returnValue, (_, _) => returnValue);
                }
                else
                {
                    _exceptions.AddOrUpdate(data, (data, resultTask.Exception), (_, _) => (data, resultTask.Exception));
                }
            }
        }
    }
}