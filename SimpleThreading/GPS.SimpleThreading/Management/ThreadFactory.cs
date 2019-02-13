using System;
using System.Collections.Concurrent;
using System.Threading;

namespace GPS.SimpleThreading.Management
{
    public static class ThreadFactory
    {
        private static int threadCounter = 0;
        public static Thread NewUnScopedThread(
            ParameterizedThreadStart threadStart,
            ThreadPriority priority = ThreadPriority.Normal,
            ApartmentState apartmentState = ApartmentState.MTA,
            string threadName = "Unscoped")
        {
            var thread = new Thread(threadStart);
            thread.Name = $"{threadName}: {threadCounter++}";
            thread.SetApartmentState(apartmentState);
            thread.Priority = priority;

            return thread;
        }

        public static Thread NewUnScopedThread(
            Action action,
            ThreadPriority priority = ThreadPriority.Normal,
            ApartmentState apartmentState = ApartmentState.MTA,
            string threadName = "Unscoped")
        {
            var wrapper = new ActionWrapper<object>(action);

            return NewUnScopedThread(
                wrapper.WrappedAction,
                priority,
                apartmentState,
                threadName);
        }

        public static Thread NewUnScopedThread<T>(
            Action<T> action,
            ThreadPriority priority = ThreadPriority.Normal,
            ApartmentState apartmentState = ApartmentState.MTA,
            string threadName = "Unscoped")
        {
            var wrapper = new ActionWrapper<T>(action);

            return NewUnScopedThread(
                wrapper.WrappedAction,
                priority,
                apartmentState,
                threadName);
        }

        public static FunctionThread<TData, TResult>
            NewUnScopedFunctionThread<TData, TResult>(
                Func<TData, TResult> function,
                ThreadPriority priority = ThreadPriority.Normal,
                ApartmentState apartmentState = ApartmentState.MTA,
                string threadName = "Unscoped")
        {
            var thread = new FunctionThread<TData, TResult>(
                function, priority, apartmentState, $"{threadName}: {threadCounter}");

            return thread;
        }

        private class ActionWrapper<T>
        {
            private Action<T> _action = null;
            public ActionWrapper(Action<T> action)
            {
                _action = action;
            }

            public ActionWrapper(Action action)
            {
                _action = new Action<T>(o => action());
            }

            public void WrappedAction(object data)
            {
                if (!(data is T)) throw new ArgumentException(
                     "Argument passed does not match the Action parameter type.",
                      nameof(data));

                _action((T)data);
            }
        }

    }
}