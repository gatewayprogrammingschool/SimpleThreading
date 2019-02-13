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

namespace GPS.SimpleThreading.Tests.Blocks
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

            var token = new CancellationTokenSource();

            var batchCount = 1;
            var numberOfBatches = 1;

            var dataSet = CreateDataSet(50, 200, 500);

            var block = new ThreadBlock<int?, string>(
                DataProcessor);

            block.BatchFinished += OnBatchFinished;
            block.DataProcessed += OnDataProcessed;
            block.DataWarmup += OnDataWarmup;

            block.BatchCancelled += () =>
            {
                _log.WriteLine("Batch successfully cancelled.");
            };

            block.EmptyQueue += () =>
            {
                if (batchCount < numberOfBatches)
                {
                    block.AddRange(dataSet);
                    batchCount++;
                }
                else
                {
                    block.Stop();
                }
            };

            block.AddRange(dataSet);

            var parallelism = 16;

            block.LockList();

            block.Execute(token, parallelism);

            //task.Wait();

            Assert.Equal(dataSet.Length * numberOfBatches, block.Results.Count());
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

        string DataProcessor(int? data)
        {
            int counter;

            lock (_log) counter = _processorCounter++;

            System.Threading.Thread.Sleep(data.Value);

            var result = $"{counter} - Waited {data} milliseconds";
            return result;
        }

        void OnDataWarmup(int? data)
        {
            _log.WriteLine($"Contrived Warmup for {data}");
        }

        void OnDataProcessed(ThreadBlock<int?, string>.DataResultPair result)
        {
            int counter;

            lock (_log) counter = _continuationCounter++;

            _log.WriteLine($"[{DateTimeOffset.Now - _start}] {counter} - Result: {result.Data}, {result.Result}");
        }

        void OnBatchFinished(IEnumerable<ThreadBlock<int?, string>.DataResultPair> results)
        {
            _log.WriteLine($"Results count: {results.Count()}");
        }

    }
}
