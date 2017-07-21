using System;
using System.Collections;
using System.Collections.Generic;

namespace GPS.SimpleThreading
{
    public class ThreadSafeList<T> : IList<T>
    {
        private readonly object _padLock = new object();

        private readonly List<T> _baseList;

        public ThreadSafeList()
        {
            _baseList = new List<T>();
        }

        public ThreadSafeList(IEnumerable<T> sourceList)
        {
            _baseList = new List<T>(sourceList);
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (_padLock)
            {
                return _baseList.GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (_padLock)
            {
                return _baseList.GetEnumerator();
            }
        }

        public void Add(T item)
        {
            lock (_padLock)
            {
                _baseList.Add(item);
            }
        }

        public void AddRange(IEnumerable<T> sourceList)
        {
            lock (_padLock)
            {
                _baseList.AddRange(sourceList);
            }
        }

        public void InsertRange(int index, IEnumerable<T> collection)
        {
            lock (_padLock)
            {
                _baseList.InsertRange(index, collection);
            }
        }

        public void Clear()
        {
            lock (_padLock)
            {
                _baseList.Clear();
            }
        }

        public bool Contains(T item)
        {
            lock (_padLock)
            {
                return _baseList.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (_padLock)
            {
                _baseList.CopyTo(array, arrayIndex);
            }
        }

        public bool Remove(T item)
        {
            lock (_padLock)
            {
                return _baseList.Remove(item);
            }
        }

        public int RemoveAll(Predicate<T> d)
        {
            lock (_padLock)
            {
                return _baseList.RemoveAll(d);
            }
        }

        public void RemoveRange(int index, int count)
        {
            lock (_padLock)
            {
                _baseList.RemoveRange(index, count);
            }
        }

        public int Count
        {
            get
            {
                lock (_padLock)
                {
                    return _baseList.Count;
                }
            }
        }

        public bool IsReadOnly => false;

        public int IndexOf(T item)
        {
            lock (_padLock)
            {
                return _baseList.IndexOf(item);
            }
        }

        public void Insert(int index, T item)
        {
            lock (_padLock)
            {
                _baseList.Insert(index, item);
            }
        }

        public void RemoveAt(int index)
        {
            lock (_padLock)
            {
                _baseList.RemoveAt(index);
            }
        }

        public T this[int index]
        {
            get
            {
                lock (_padLock)
                {
                    return _baseList[index];
                }
            }
            set
            {
                lock (_padLock)
                {
                    _baseList[index] = value;
                }
            }
        }
    }
}
