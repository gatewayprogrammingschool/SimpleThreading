using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GPS.SimpleThreading.Blocks
{
    public sealed class ThreadBlock<T, TResult>
    {
        private readonly ConcurrentDictionary<T, (T data, TResult result)?> _results =
            new ConcurrentDictionary<T,  (T data, TResult result)?>();

        private readonly ConcurrentBag<T> _baseList =
            new ConcurrentBag<T>();

        private bool _locked;
        private readonly Func<T, TResult> _action;
        private readonly Action<(T data, TResult result)?[]> _continuation;

        public ThreadBlock(
            Func<T, TResult> action,
            Action<(T data, TResult result)?[]> continuation = null)
        {
            _action = action;
            _continuation = continuation;
        }

        public void Add(T item)
        {
            _baseList.Add(item);
        }

        public void AddRange(IEnumerable<T> collection)
        {
            Parallel.ForEach(collection, Add);
        }

        public void AddRange(ICollection<T> collection)
        {
            Parallel.ForEach(collection, Add);
        }

        public void AddRange(IProducerConsumerCollection<T> collection)
        {
            Parallel.ForEach(collection, Add);
        }

        public int MaxDegreeOfParallelism { get; set; } = 1;

        public bool Remove(T item)
        {
            T itemToRemove;
            return _baseList.TryTake(out itemToRemove);
        }

        public void LockList()
        {
            _locked = true;
        }

        public void Execute(
            int maxDegreeOfParallelization = -1,
            Action<T> warmupItem = null,
            Action<Task, (T data, TResult result)?> threadContinuation = null)
        {
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
            if (!_locked) throw new NotLockedException();

            var queue = new Queue<T>(_baseList);
            var allTasks = new Dictionary<T, Task>();

            int depth = 0;

            while (queue.Any())
            {
                var item = queue.Dequeue();

                warmupItem?.Invoke(item);

                var task = new Task<TResult>(() => _action(item));

                task
                    .ContinueWith((resultTask, data) =>
                        {
                            var returnValue = ((T, TResult)?)(data, resultTask.Result);
                            
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


                allTasks.Add(item, task);
            }

            foreach (var t in allTasks)
            {
                int d = 0;
                lock (padLock)
                {
                    d = depth;
                }

                while (d >= maxDegreeOfParallelization)
                {
                    System.Threading.Thread.Sleep(5);
                    lock (padLock)
                    {
                        d = depth;
                    }
                }

                t.Value.Start(TaskScheduler.Default);

                lock (padLock)
                {
                    depth++;
                }
            }

            var dd = 0;

            lock (padLock)
            {
                dd = depth;
            }

            while (dd > 0)
            {
                Thread.Sleep(5);
                lock (padLock)
                {
                    dd = depth;
                }
            }

            _continuation?.Invoke(_results.Values.ToArray());
        }

        public ConcurrentDictionary<T, (T data, TResult result)?> Results
        {
            get
            {
                var results = new ConcurrentDictionary<T, (T data, TResult result)?>();

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
