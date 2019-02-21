using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using GPS.SimpleThreading.Management;
using GPS.SimpleExtensions;
using Xunit;
using Xunit.Abstractions;
using GPSThreadFactory = GPS.SimpleThreading.Management.ThreadFactory;
using System.Collections;

public class FunctionThreadTests
{
    ITestOutputHelper _log;
    GPSThreadFactory _threadFactory = new GPSThreadFactory();

    public FunctionThreadTests(ITestOutputHelper log)
    {
        _log = log;
    }

    private string TestFunction(int data)
    {
        return data.ToString();
    }

    private string LongRunningFunction(int data)
    {
        Thread.Sleep(60000);

        return TestFunction(data);
    }

    [Fact]
    public FunctionThread<int, string> CreateThread()
    {
        var functionThread = _threadFactory.NewUnScopedFunctionThread<int, string>(
            TestFunction);

        Assert.NotNull(functionThread);

        return functionThread;
    }

    [Theory]
    [ClassData(typeof(IntDataSet))]
    public void GetResults(int data)
    {
        var thread = CreateThread();

        var result = thread.StartResultSync(data);

        Assert.Equal(data.ToString(), result.Result);
    }

    [Theory]
    [ClassData(typeof(IntDataSet))]
    public void CancelThreadWithTimeout(int data)
    {
        var thread = _threadFactory.NewUnScopedFunctionThread<int, string>(this.LongRunningFunction);

        var result = thread.StartResultSync(data, 500);

        Assert.False(result.IsCompletedSuccessfully);

        try
        {
            Assert.True(result.IsCanceled);
            _log.WriteLine("Thread successfully aborted.");
        }
        catch (Exception)
        {
            Assert.NotNull(result.Exception);
            Assert.IsType<System.PlatformNotSupportedException>(result.Exception.InnerException);
            _log.WriteLine(result.Exception.Message);
        }
    }

    [Theory]
    [ClassData(typeof(IntDataSet))]
    public void CancelThreadWithToken(int data)
    {
        var tokenSource = new CancellationTokenSource();
        var thread = _threadFactory.NewUnScopedFunctionThread<int, string>(this.LongRunningFunction);
        Task<string> result = null;

        var runner = Task.Run(new Func<Task<object>>(async () =>
        {
            await Task.Delay(500);
            tokenSource.Cancel();

            while (result == null) await Task.Delay(5);

            Assert.False(result.IsCompletedSuccessfully);

            try
            {
                Assert.True(result.IsCanceled);
                _log.WriteLine("Thread successfully aborted.");
            }
            catch (Exception)
            {
                Assert.NotNull(result.Exception);
                Assert.IsType<System.PlatformNotSupportedException>(result.Exception.InnerException);
                _log.WriteLine(result.Exception.Message);
            }

            return null;
        }));

        result = thread.StartResultSync(data, tokenSource.Token);

        Assert.True(runner.Wait(1100));
    }

    [Fact]
    public void AbortThread()
    {
        var thread = _threadFactory.NewUnScopedFunctionThread<int, string>(this.LongRunningFunction);

        var task = thread.StartAsync(1);

        var result = thread.Abort();

        if (result != null)
        {
            Assert.IsType<PlatformNotSupportedException>(result);
        }
    }
}

public class IntDataSet : IEnumerable<object[]>
{
    object[] _data = {
        10, 20, 30, 40, 50, 60, 70, 80, 90, 100,
    };

    public IEnumerator<object[]> GetEnumerator()
    {
        foreach (var item in _data) yield return new object[] { item };
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}