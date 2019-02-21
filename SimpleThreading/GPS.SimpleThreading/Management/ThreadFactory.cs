using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GPS.SimpleThreading.Management
{
    public class ThreadFactory
    {
        ILogger _logger;
        
        public ThreadFactory(ILogger logger)
        {
            _logger = logger;
        }

        public ThreadFactory()
        {
            var collection = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            var loggerFactory = new LoggerFactory();

            collection.AddSingleton<ILoggerFactory>(loggerFactory);

            _logger = collection.BuildServiceProvider()
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger<ILogger>();
        }

        private int threadCounter = 0;
        public Thread NewUnScopedThread(
            ParameterizedThreadStart threadStart,
            ThreadPriority priority = ThreadPriority.Normal,
            ApartmentState apartmentState = ApartmentState.MTA,
            string threadName = "Unscoped")
        {
            var thread = new Thread(threadStart);
            thread.Name = $"{threadName}: {threadCounter++}";
            thread.SetApartmentState(apartmentState);
            thread.Priority = priority;

            using(var scope = _logger.BeginScope<string>("NewUnScopedThread(ThreadStart)"))
            {
                _logger.LogInformation($"Created Thread {thread.Name} - {apartmentState} - {priority}");
                return thread;
            }
        }

        public Thread NewUnScopedThread(
            Action action,
            ThreadPriority priority = ThreadPriority.Normal,
            ApartmentState apartmentState = ApartmentState.MTA,
            string threadName = "Unscoped")
        {
            var wrapper = new ActionWrapper<object>(action);

            var thread = NewUnScopedThread(
                wrapper.WrappedAction,
                priority,
                apartmentState,
                threadName);
                
            using(var scope = _logger.BeginScope<string>("NewUnScopedThread(Action)"))
            {
                _logger.LogInformation($"Created Thread {thread.Name} - {apartmentState} - {priority}");
                return thread;
            }

        }

        public Thread NewUnScopedThread<T>(
            Action<T> action,
            ThreadPriority priority = ThreadPriority.Normal,
            ApartmentState apartmentState = ApartmentState.MTA,
            string threadName = "Unscoped")
        {
            var wrapper = new ActionWrapper<T>(action);

            var thread = NewUnScopedThread(
                wrapper.WrappedAction,
                priority,
                apartmentState,
                threadName);
                                
            using(var scope = _logger.BeginScope<string>("NewUnScopedThread(Action<T>)"))
            {
                _logger.LogInformation($"Created Thread {thread.Name} - {apartmentState} - {priority}");
                return thread;
            }

        }

        public FunctionThread<TData, TResult>
            NewUnScopedFunctionThread<TData, TResult>(
                Func<TData, TResult> function,
                ThreadPriority priority = ThreadPriority.Normal,
                ApartmentState apartmentState = ApartmentState.MTA,
                string threadName = "Unscoped")
        {
            var thread = new FunctionThread<TData, TResult>(
                _logger, function, priority, apartmentState, 
                $"{threadName}: {threadCounter}");
                
            using(var scope = _logger.BeginScope<string>("NewUnscopedFunctionThread(Func<TData, TReturn>)"))
            {
                _logger.LogInformation($"Created FunctionThread {thread.Name} - {apartmentState} - {priority}");
                return thread;
            }
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