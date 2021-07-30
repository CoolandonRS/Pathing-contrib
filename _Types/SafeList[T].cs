﻿using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace BhModule.Community.Pathing {
    public class SafeList<T> : IList<T> {

        private class SafeEnumerator<TEnumerator> : IEnumerator<TEnumerator> {

            private readonly IEnumerator<TEnumerator> _inner;
            private readonly ReaderWriterLockSlim     _rwLock;

            public SafeEnumerator(IEnumerator<TEnumerator> inner, ReaderWriterLockSlim rwLock) {
                _inner = inner;
                _rwLock = rwLock;

                _rwLock.EnterReadLock();
            }

            public bool MoveNext() {
                return _inner.MoveNext();
            }

            public void Reset() {
                _inner.Reset();
            }

            public object Current => _inner.Current;

            TEnumerator IEnumerator<TEnumerator>.Current => _inner.Current;

            public void Dispose() {
                _rwLock.ExitReadLock();
            }

        }

        private readonly List<T>              _innerList;
        private readonly ReaderWriterLockSlim _listLock = new();

        public bool IsReadOnly => false;

        public bool IsEmpty { get; private set; } = true;

        public SafeList() {
            _innerList = new List<T>();
        }

        public SafeList(IEnumerable<T> existingControls) {
            _innerList = new List<T>(existingControls);

            this.IsEmpty = !_innerList.Any();
        }

        public IEnumerator<T> GetEnumerator() {
            return new SafeEnumerator<T>(_innerList.GetEnumerator(), _listLock);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(T item) {
            if (item == null || this.Contains(item)) return;

            _listLock.EnterWriteLock();
            _innerList.Add(item);
            _listLock.ExitWriteLock();

            this.IsEmpty = false;
        }

        public void AddRange(IEnumerable<T> items) {
            _listLock.EnterWriteLock();
            _innerList.AddRange(items);
            _listLock.ExitWriteLock();
        }

        public void Clear() {
            _listLock.EnterWriteLock();
            _innerList.Clear();
            _listLock.ExitWriteLock();

            this.IsEmpty = true;
        }

        public bool Contains(T item) {
            _listLock.EnterReadLock();

            try {
                return _innerList.Contains(item);
            } finally {
                _listLock.ExitReadLock();
            }
        }

        public void CopyTo(T[] array, int arrayIndex) {
            _listLock.EnterReadLock();

            try {
                _innerList.CopyTo(array, arrayIndex);
            } finally {
                _listLock.ExitReadLock();
            }
        }

        public bool Remove(T item) {
            _listLock.EnterWriteLock();

            try {
                return _innerList.Remove(item);
            } finally {
                this.IsEmpty = !_innerList.Any();
                _listLock.ExitWriteLock();
            }
        }

        public int Count {
            get {
                _listLock.EnterReadLock();

                try {
                    return _innerList.Count;
                } finally {
                    _listLock.ExitReadLock();
                }
            }
        }

        public IReadOnlyCollection<T> AsReadOnly() {
            return new ReadOnlyCollection<T>(this);
        }

        public List<T> GetNoLockList() {
            _listLock.EnterReadLock();

            try {
                return new List<T>(_innerList);
            } finally {
                _listLock.ExitReadLock();
            }
        }

        public T[] GetNoLockArray() {
            _listLock.EnterReadLock();
            var items = new T[_innerList.Count];
            _innerList.CopyTo(items, 0);
            _listLock.ExitReadLock();

            return items;
        }

        public int IndexOf(T item) {
            _listLock.EnterReadLock();

            try {
                return _innerList.Count;
            } finally {
                _listLock.ExitReadLock();
            }
        }

        public void Insert(int index, T item) {
            _listLock.EnterWriteLock();
            _innerList.Insert(index, item);
            _listLock.ExitWriteLock();
        }

        public void RemoveAt(int index) {
            _listLock.EnterWriteLock();
            _innerList.RemoveAt(index);
            _listLock.ExitWriteLock();
        }

        public T this[int index] {
            get {
                _listLock.EnterReadLock();

                try {
                    return _innerList[index];
                } finally {
                    _listLock.ExitReadLock();
                }
            }
            set {
                _listLock.EnterWriteLock();
                _innerList[index] = value;
                _listLock.ExitWriteLock();
            }
        }

        ~SafeList() {
            _listLock?.Dispose();
        }

    }
}
