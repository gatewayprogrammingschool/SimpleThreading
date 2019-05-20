using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

        [Fact]
        public void ContrivedTest()
        {
            string Processor(int data)
            {
                if(data %5 == 0) throw new ApplicationException("Data was multiple of 5");
                System.Threading.Thread.Sleep(data);
                return $"Waiting {data} miliseconds";
            }

            void Warmup(int data)
            {
                _log.WriteLine($"Contrived Warmup for {data}");
            }

            void ThreadBlockContinuation(Task task, (int data, string result)? result)
            {
                _log.WriteLine($"Contrived Thread Continuation result: {result.Value.data}, {result.Value.result}");
            }

            // void PLINQContinuation((int data, string result)? result)
            // {
            //     _log.WriteLine($"Contrived Thread Continuation result: {result.Value.data}, {result.Value.result}");
            // }

            void BlockContinuation(ICollection<(int data, string result)?> results)
            {
                _log.WriteLine($"Results count: {results.Count}");
            }

            var dataSet = new int[20];

            var rand = new System.Random();

            for(int i = 0; i < dataSet.Length; ++i)
            {
                dataSet[i] = rand.Next(250, 2500);
            }

            var block = new ThreadBlock<int, string>(
                Processor,
                BlockContinuation);

            block.AddRange(dataSet);

            block.LockList();

            var parallelism = 8;

            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            block.Execute(parallelism, Warmup, ThreadBlockContinuation);

            sw.Stop();
            var blockElapsed = sw.Elapsed;

            _log.WriteLine($"Finished in {blockElapsed.Milliseconds} ms");

            // sw = new System.Diagnostics.Stopwatch();

            // sw.Start();
            
            // var resultSet = dataSet
            //     .Select(data => { Warmup(data); return data; })
            //     .AsParallel()
            //     .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
            //     .WithDegreeOfParallelism(parallelism)
            //     .Select(data => 
            //     {
            //             return new Nullable<(int data, string result)>
            //                 ((data: data, result: Processor(data)));
            //     })
            //     .AsSequential()
            //     .Select(result => {
            //         PLINQContinuation(result);
            //         return result;
            //     }).ToList();

            // BlockContinuation(resultSet.ToArray());

            // sw.Stop();
            // var plinqElapsed = sw.Elapsed;

            // _log.WriteLine(
            //     $"block: {blockElapsed.TotalSeconds}, " + 
            //     $"PLINQ: {plinqElapsed.TotalSeconds}");

            Assert.Equal(dataSet.Length, block.Results.Count + block.Exceptions.Count);
            // Assert.Equal(dataSet.Length, resultSet.Count);

            Assert.NotEqual(0, block.Exceptions.Count);

            block.Exceptions.Values.ToList().ForEach(ex => _log.WriteLine(ex.ToString()));

            // This is here to force the test to fail
            // allowing dotnet test to output the log.
            // Assert.Equal(blockElapsed, plinqElapsed);
        }
    }
}
