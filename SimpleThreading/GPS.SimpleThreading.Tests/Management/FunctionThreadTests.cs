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

    public FunctionThreadTests(ITestOutputHelper log)
    {
        _log = log;
    }

    private string TestFunction(int data)
    {
        Thread.Sleep(60000);

        return data.ToString();
    }

    private string LongRunningFunction(int data)
    {
        return TestFunction(data);
    }

    [Fact]
    public FunctionThread<int, string> CreateThread()
    {
        var functionThread = GPSThreadFactory.NewUnScopedFunctionThread<int, string>(
            TestFunction);

        Assert.NotNull(functionThread);

        return functionThread;
    }

    [Theory]
    [ClassData(typeof(IntDataSet))]
    public void GetResults(int data)
    {
        var thread = CreateThread();

        var result = thread.StartResult(data, new CancellationTokenSource().Token);

        Assert.Equal(data.ToString(), result);
    }

    [Theory]
    [ClassData(typeof(IntDataSet))]
    public void CancelThreads(int data)
    {
        var thread = GPSThreadFactory.NewUnScopedFunctionThread<int, string>(this.LongRunningFunction);

        var result = thread.StartAsync(data);

        result.AsyncWaitHandle.WaitOne(500);

        Assert.Null(result.AsyncState);
    }
}

public class IntDataSet : IEnumerable<object[]>
{
    object[] _data = {
        10, 20, 30, 40, 50, 60, 70, 80, 90, 100,
    };

    public IEnumerator<object[]> GetEnumerator()
    {
        foreach(var item in _data) yield return new object[] { item };
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}