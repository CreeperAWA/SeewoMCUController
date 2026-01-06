using System;

namespace Cvte.Mcu
{
    public class AsyncOperation<T>
    {
        public static AsyncOperation<T> Create(out Action<T, Exception> result)
        {
            var operation = new AsyncOperation<T>();
            result = operation.SetResult;
            return operation;
        }

        private T? _result;
        private Exception? _exception;
        private bool _completed;
        private readonly object _lock = new object();

        public T Result
        {
            get
            {
                lock (_lock)
                {
                    if (!_completed)
                    {
                        throw new InvalidOperationException("Operation not completed yet");
                    }
                    return _result!;
                }
            }
        }

        public Exception Error
        {
            get
            {
                lock (_lock)
                {
                    if (!_completed)
                    {
                        throw new InvalidOperationException("Operation not completed yet");
                    }
                    return _exception!;
                }
            }
        }

        public bool IsCompleted
        {
            get
            {
                lock (_lock)
                {
                    return _completed;
                }
            }
        }

        private void SetResult(T result, Exception error)
        {
            lock (_lock)
            {
                _result = result;
                _exception = error;
                _completed = true;
            }
        }
    }
}