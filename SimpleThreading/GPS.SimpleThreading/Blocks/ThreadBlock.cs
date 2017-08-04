using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GPS.SimpleThreading.Blocks
{
    public sealed class ThreadBlock<T, TResult>
    {
        private readonly ConcurrentDictionary<Tuple<T, Task<TResult>>, TResult> _results =
            new ConcurrentDictionary<Tuple<T, Task<TResult>>, TResult>();

        private readonly ConcurrentBag<T> _baseList =
            new ConcurrentBag<T>();

        private bool _locked;
        private readonly Func<T, TResult> _action;

        public ThreadBlock(Func<T, TResult> action)
        {
            _action = action;
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
            int maxDegreeOfParallelization = 1,
            Action<T> warmupItem = null,
            Action<Task[]> continuation = null)
        {
            var padLock = new object();
            if (!_locked) throw new NotLockedException();

            var queue = new Queue<T>(_baseList);
            var allTasks = new List<Task>();

            int[] depth = {0};
            var factory = new TaskFactory(TaskScheduler.Default);

            while (queue.Any())
            {
                var item = queue.Dequeue();

                warmupItem?.Invoke(item);

                var task = new Task<TResult>(() => _action(item));
                
                task.ContinueWith(r =>
                {
                    _results.AddOrUpdate(new Tuple<T,Task<TResult>>(item, r), r.Result, (tuple, result) => r.Result);
                    lock (padLock)
                    {
                        depth[0]--;
                    }
                });

                allTasks.Add(task);
            }

            foreach (var t in allTasks)
            {
                int d = 0;
                lock (padLock)
                {
                    d = depth[0];
                }

                while (d >= maxDegreeOfParallelization)
                {
                    System.Threading.Thread.Sleep(5);
                    lock (padLock)
                    {
                        d = depth[0];
                    }
                }

                t.Start(TaskScheduler.Default);

                lock (padLock)
                {
                    depth[0]++;
                }
            }

            if (continuation != null)
            {
                factory.ContinueWhenAll(allTasks.ToArray(), continuation);
            }
        }

        public ConcurrentDictionary<T, TResult> Results
        {
            get
            {
                var results = new ConcurrentDictionary<T, TResult>();

                foreach (var key in _results.Keys)
                {
                    var result = _results[key];
                    var value = key.Item1;

                    results.AddOrUpdate(value, result, (arg1, result1) => result);
                }

                return results;
            }
        }
    }
}
