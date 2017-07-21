using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPS.SimpleThreading.Blocks
{
    public sealed class ThreadBlock<T, TResult>
    {
        private readonly ThreadSafeDictionary<Tuple<T, Task<TResult>>, TResult> _results =
            new ThreadSafeDictionary<Tuple<T, Task<TResult>>, TResult>();

        private readonly ThreadSafeList<T> _baseList =
            new ThreadSafeList<T>();

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
            _baseList.AddRange(collection);
        }

        public int MaxDegreeOfParallelism { get; set; } = 1;

        public bool Remove(T item)
        {
            return _baseList.Remove(item);
        }

        public void RemoveAt(int index)
        {
            _baseList.RemoveAt(index);
        }

        public int IndexOf(T item)
        {
            return _baseList.IndexOf(item);
        }

        public void LockList()
        {
            _locked = true;
        }

        public void Execute(
            int maxDegreeOfParallelization = 1,
            Action<Task[]> continuation = null)
        {
            var padLock = new object();
            if (!_locked) throw new NotLockedException();

            var queue = new Queue<T>(_baseList);
            var allTasks = new List<Task>();

            var depth = 0;
            var factory = new TaskFactory(TaskScheduler.Default);

            while (queue.Any())
            {
                var item = queue.Dequeue();

                var task = new Task<TResult>(() => _action(item));
                
                task.ContinueWith(r =>
                {
                    _results.Add(new Tuple<T,Task<TResult>>(item, r), r.Result);
                    lock (padLock)
                    {
                        depth--;
                    }
                });

                allTasks.Add(task);
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

                t.Start(TaskScheduler.Default);

                lock (padLock)
                {
                    depth++;
                }
            }

            if (continuation != null)
            {
                factory.ContinueWhenAll(allTasks.ToArray(), continuation);
            }
        }

        public List<KeyValuePair<T, TResult>> Results
        {
            get
            {
                var results = new List<KeyValuePair<T, TResult>>();

                foreach (var key in _results.Keys)
                {
                    var result = _results[key];
                    var value = key.Item1;

                    results.Add(new KeyValuePair<T, TResult>(value, result));
                }

                return results;
            }
        }
    }

    public class NotLockedException : Exception
    {
        public NotLockedException()
            : base("ThreadBlock is not locked.")
        {
        }

        public NotLockedException(string message) : base(message)
        {
        }

        public NotLockedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public NotLockedException(Exception innerException)
            : base("ThreadBlock is not locked.", innerException)
        {
        }
    }
}
