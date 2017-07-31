using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPS.SimpleThreading
{
    public class ThreadSafeDictionary<K, V> : IDictionary<K, V>
    {
        private readonly object _padLock = new object();
        private readonly Dictionary<K, V> _baseDictionary = new Dictionary<K, V>();

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            lock (_padLock)
            {
                return _baseDictionary.GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (_padLock)
            {
                return _baseDictionary.GetEnumerator();
            }
        }

        public Func<K, V, KeyValuePair<K, V>> Constructor { get; set; }

        public void Add(KeyValuePair<K, V> item)
        {
            lock (_padLock)
            {
                if (!ContainsKey(item.Key))
                {
                    _baseDictionary.Add(item.Key, item.Value);
                }
                else
                {
                    _baseDictionary[item.Key] = item.Value;
                }
            }
        }

        public void Add(K key, V item)
        {
            lock (_padLock)
            {
                Add(Constructor.Invoke(key, item));
            }
        }

        public void Clear()
        {
            lock (_padLock)
            {
                _baseDictionary.Clear();
            }
        }

        public bool Contains(KeyValuePair<K, V> item)
        {
            lock (_padLock)
            {
                return _baseDictionary.Contains(item);
            }
        }

        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            lock (_padLock)
            {
                foreach (var key in _baseDictionary.Keys)
                {
                    var newArray = new KeyValuePair<K, V>[array.Length + _baseDictionary.Count];
                    Array.ConstrainedCopy(array, 0, newArray, 0, arrayIndex);
                    Array.ConstrainedCopy(_baseDictionary.ToArray(), 0, newArray, arrayIndex, _baseDictionary.Count);
                    Array.ConstrainedCopy(array, arrayIndex, newArray, arrayIndex + _baseDictionary.Count, array.Length - arrayIndex);
                }
            }
        }

        public bool Remove(KeyValuePair<K, V> item)
        {
            lock (_padLock)
            {
                return _baseDictionary.Remove(item.Key);
            }
        }

        public int Count
        {
            get { lock(_padLock) return _baseDictionary.Count; }
        }

        public bool IsReadOnly => false;

        public bool ContainsKey(K key)
        {
            lock (_padLock)
            {
                return _baseDictionary.ContainsKey(key);
            }
        }

        public bool Remove(K key)
        {
            lock (_padLock)
            {
                return _baseDictionary.Remove(key);
            }
        }

        public bool TryGetValue(K key, out V value)
        {
            lock (_padLock)
            {
                return _baseDictionary.TryGetValue(key, out value);
            }
        }

        public V this[K key]
        {
            get
            {
                lock (_padLock)
                {
                    if (!_baseDictionary.ContainsKey(key))
                    {
                        Add(Constructor.Invoke(key, default(V)));
                    }

                    return _baseDictionary[key];
                }
            }
            set
            {
                lock (_padLock)
                {
                    if (!_baseDictionary.ContainsKey(key))
                    {
                        Add(Constructor.Invoke(key, value));
                    }

                    _baseDictionary[key] = value;
                }
            }
        }

        public ICollection<K> Keys
        {
            get
            {
                lock (_padLock)
                {
                    return _baseDictionary.Keys;
                }
            }
        }

        public ICollection<V> Values
        {
            get
            {
                lock (_padLock)
                {
                    return _baseDictionary.Values;
                }
            }
        }
    }
}
