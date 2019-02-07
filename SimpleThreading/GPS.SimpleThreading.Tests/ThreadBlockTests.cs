using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using GPS.SimpleThreading.Blocks;
using GPS.SimpleExtensions;
using Xunit;
using Xunit.Abstractions;

namespace GPS.SimpleThreading.Tests
{
    public class ThreadBlockTests
    {
        ITestOutputHelper _log;

        public ThreadBlockTests(ITestOutputHelper log)
        {
            _log = log;
        }

        int _continuationCounter = 0;
        int _processorCounter = 0;
        private DateTimeOffset _start;

        [Fact]
        public void ContrivedTest()
        {

        }

        [Fact]
        public void Continuous()
        {
            _continuationCounter = 0;
            _processorCounter = 0;
            _start = DateTimeOffset.Now;

            var dataSet = CreateDataSet(100, 200, 500);

            var block = new ThreadBlock<int?, string>(
                Processor,
                BlockContinuation);

            block.AddRange(dataSet);

            var parallelism = 16;

            var token = new CancellationTokenSource();

            var task = block.ExecuteContinuous(token, parallelism, Warmup, ThreadContinuation);

            System.Threading.Thread.Sleep(60000);

            block.AddRange(dataSet);

            task.Wait(new TimeSpan(0, 1, 0));

            Assert.Equal(dataSet.Length * 2, block.Results.Count);
        }

        public int?[] CreateDataSet(int size = 500, int min = 250, int max = 2500)
        {
            var dataSet = new int?[size];

            var rand = new System.Random();

            for (int i = 0; i < dataSet.Length; ++i)
            {
                dataSet[i] = rand.Next(min, max);
            }

            return dataSet;
        }

        string Processor(int? data)
        {
            int counter;
            
            lock(_log) counter = _processorCounter++;

            System.Threading.Thread.Sleep(data.Value);

            var result = $"{counter} - Waited {data} milliseconds";
            return result;
        }

        void Warmup(int? data)
        {
            _log.WriteLine($"Contrived Warmup for {data}");
        }

        void ThreadContinuation(Task task, (int? data, string result)? result)
        {
            int counter;
            
            lock(_log) counter = _continuationCounter++;

            _log.WriteLine($"[{DateTimeOffset.Now - _start}] {counter} - Contrived Thread Continuation result: {result.Value.data}, {result.Value.result}");
        }

        void BlockContinuation(ICollection<(int? data, string result)?> results)
        {
            _log.WriteLine($"Results count: {results.Count}");
        }

    }
}
