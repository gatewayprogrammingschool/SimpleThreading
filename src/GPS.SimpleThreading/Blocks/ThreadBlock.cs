using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
#pragma warning disable CS8629 // Nullable value type may be null.
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
        private readonly Func<TData?, Task<TResult?>> _asyncAction;
        private readonly Func<ICollection<(TData? data, TResult? result)?>, Task>? _asyncContinuation;

        private readonly ConcurrentDictionary<Option<TData>, (Option<TData> data, Exception result)?> _exceptions = new();
        private readonly ConcurrentDictionary<Option<TData>, (Option<TData> data, Option<TResult> result)?> _results = new();

        private bool _locked;

        private ConcurrentQueue<Option<TData>> _queue = new();

        /// <summary>
        ///     Constructor accepting the action and block continuation.
        /// </summary>
        public ThreadBlock(
            Func<TData?, TResult?> action,
            Action<ICollection<(TData? data, TResult? result)?>>? continuation = null)
        {
            _asyncAction = data =>
            {
                var result = action(data);
                return Task.FromResult(result);
            };

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
            _asyncContinuation = asyncContinuation;
        }

        /// <summary>
        ///     Maximum number of concurrent threads (default = 1).
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = 1;

        /// <summary>
        ///     Point-in-time results providing a stable result set
        ///     for processing results as the block runs.
        /// </summary>
        public ConcurrentDictionary<Option<TData>, (Option<TData> data, Option<TResult> result)?> Results
        {
            get
            {
                var snapshot = _results.ToArray();

                var results = new ConcurrentDictionary<Option<TData>, (Option<TData> data, Option<TResult> result)?>();

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
        public ConcurrentDictionary<Option<TData>, (Option<TData> data, Exception exception)?> Exceptions
        {
            get
            {
                var snapshot = _exceptions.ToArray();

                var exceptions = new ConcurrentDictionary<Option<TData>, (Option<TData> data, Exception exception)?>();

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
            if (!_locked) _queue.Enqueue(new Option<TData>(item));
            else throw new LockedException();
        }

        /// <summary>
        ///     Adds range of data items from an IEnumerable
        /// </summary>
        public void AddRange(IEnumerable<TData?> collection)
        {
            if (!_locked) Parallel.ForEach(collection.Select(item => new Option<TData>(item)), _queue.Enqueue);
            else throw new LockedException();
        }

        /// <summary>
        ///     Adds range of data items from an IProducerConsumerCollection.
        /// </summary>
        public void AddRange(IProducerConsumerCollection<TData?> collection)
        {
            if (!_locked) Parallel.ForEach(collection.Select(item => new Option<TData>(item)), _queue.Enqueue);
            else throw new LockedException();
        }

        /// <summary>
        ///     Adds range of data items from an IEnumerable that preserves original order.
        /// </summary>
        public void OrderedAddRange(IEnumerable<TData?> collection)
        {
            if (!_locked)
            {
                var toAdd = collection
                    .Select(item => new Option<TData>(item))
                    .ToList();

                toAdd.ForEach(_queue.Enqueue);
            }
            else throw new LockedException();
        }

        /// <summary>
        ///     Adds range of data items from an IProducerConsumerCollection that preserves original order.
        /// </summary>
        public void OrderedAddRange(IProducerConsumerCollection<TData?> collection)
        {
            if (!_locked)
            {
                var toAdd = collection
                    .Select(item => new Option<TData>(item))
                    .ToList();

                toAdd.ForEach(_queue.Enqueue);
            }
            else throw new LockedException();
        }

        /// <summary>
        ///     Removes a data item from the block.
        /// </summary>
        public bool Remove(TData? item)
        {
            if (_locked) return false;

            var items = _queue.ToList();
            if (items.Remove(new Option<TData>(item)))
            {
                _queue = new ConcurrentQueue<Option<TData>>(items);
            }

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

        public async Task<IEnumerable<Option<TResult>>> ExecuteAsync(
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
                Option<TData> item = default;
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
                        await warmupItem(item.Value);
                    }

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            t = _asyncAction(item.Value);
                            await t;
                        }
                        catch (Exception ex)
                        {
                            _exceptions.AddOrUpdate(item, (item, ex), (_, _) => (item, ex));
                        }

                        if (t is null) return default;

                        try
                        {
                            await Continuation(t, item.Value);
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
                    var results = _results.Values.Select(r => new (TData? data, TResult? result)?((r.Value.data.Value, r.Value.result.Value))).ToList();
                    await _asyncContinuation(results);
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

                    var toSave = (new Option<TData>(returnValue.Value.Item1),
                        new Option<TResult>(returnValue.Value.Item2));
                    _results.AddOrUpdate(new Option<TData>(data), toSave, (_, _) => toSave);
                }
                else
                {
                    var toSave = (new Option<TData>(data), resultTask.Exception);
                    _exceptions.AddOrUpdate(new Option<TData>(data), toSave, (_, _) => toSave);
                }
            }
        }
    }

    public struct Option<T>
    {
        public Option(T? value)
        {
            Value = value;
        }

        public T? Value { get; set; }

        public static Option<T> None = default;
        public static Option<T> Some(T value) => new(value);
        public override string ToString() => Value?.ToString() ?? string.Empty;
    }
}
#pragma warning restore CS8629 // Nullable value type may be null.
