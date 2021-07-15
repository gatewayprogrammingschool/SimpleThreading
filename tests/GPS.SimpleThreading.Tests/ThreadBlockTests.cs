using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using FluentAssertions;
using GPS.SimpleThreading.Blocks;
using GPS.SimpleExtensions;
using Xunit;
using Xunit.Abstractions;

namespace GPS.SimpleThreading.Tests
{
    public class ThreadBlockTests
    {
        const int PARALLELISM = 8;
        private const int ITERATIONS = 100;
        private readonly TimeSpan _timeout = TimeSpan.FromMinutes(10);

        ITestOutputHelper _log;

        public ThreadBlockTests(ITestOutputHelper log)
        {
            _log = log;
        }

        string Processor(int data)
        {
            if (data % 5 == 0) throw new ApplicationException("Data was multiple of 5");
            Thread.Sleep(data);
            return $"Waited {data} miliseconds";
        }

        Task<string> ProcessorAsync(int data)
        {
            if (data % 5 == 0) throw new ApplicationException("Data was multiple of 5");
            Thread.Sleep(data);
            return Task.FromResult($"Waiting {data} miliseconds");
        }

        void Warmup(int data)
        {
            if (Debugger.IsAttached) _log.WriteLine($"Contrived Warmup for {data}");
        }

        Task WarmupAsync(int data)
        {
            if (Debugger.IsAttached) _log.WriteLine($"Contrived Warmup for {data}");

            return Task.CompletedTask;
        }

        void ThreadBlockContinuation(Task task, (int data, string result)? result)
        {
            if (Debugger.IsAttached) _log.WriteLine($"Contrived Thread Continuation result: {result.Value.data}, {result.Value.result}");
        }

        Task ThreadBlockContinuationAsync(Task task, (int data, string result)? result)
        {
            if (Debugger.IsAttached) _log.WriteLine($"Contrived Thread Continuation result: {result.Value.data}, {result.Value.result}");

            return Task.CompletedTask;
        }

        void BlockContinuation(ICollection<(int data, string result)?> results)
        {
            if (Debugger.IsAttached) _log.WriteLine($"Results count: {results.Count}");
        }

        Task BlockContinuationAsync(ICollection<(int data, string result)?> results)
        {
            if (Debugger.IsAttached) _log.WriteLine($"Results count: {results.Count}");

            return Task.CompletedTask;
        }

        void PlinqContinuation((int data, string result)? result)
        {
            if (Debugger.IsAttached) _log.WriteLine($"Contrived Thread Continuation result: {result.Value.data}, {result.Value.result}");
        }

        TimeSpan ExecutePlinq(int[] dataSet, int maxParallelism = PARALLELISM, bool assert = true)
        {
            var sw = new System.Diagnostics.Stopwatch();
            var tokenSource = new CancellationTokenSource(_timeout);
            sw.Start();
            ConcurrentDictionary<int?, (int? data, Exception result)?> exceptions = new();

            var resultSet = dataSet
                .Select(data => { Warmup(data); return data; })
                .AsParallel()
                .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
                .WithDegreeOfParallelism(maxParallelism)
                .WithCancellation(tokenSource.Token)
                .Select(data =>
                {
                    try
                    {
                        var result = ((int, string)?)(data, Processor(data));

                        if (result.Value.Item2 is null)
                        {
                            throw new NullReferenceException("result.Value.Item2");
                        }

                        return result;
                    }
                    catch (Exception ex)
                    {
                        exceptions.AddOrUpdate(data, (data, ex), (_, _) => (data, ex));
                    }

                    return default;
                })
                .Where(item => item != default)
                .AsSequential()
                .Select(result =>
                {
                    try
                    {
                        if (result is null) throw new NullReferenceException(nameof(result));
                        PlinqContinuation(result);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        var (data, _) = result ?? default;
                        exceptions.AddOrUpdate(data, (data, ex), (_, _) => (data, ex));
                    }

                    return default;
                })
                .Where(item => item != default)
                .ToArray();

            BlockContinuation(resultSet);

            sw.Stop();

            if (assert)
            {
                tokenSource.IsCancellationRequested.Should().BeFalse();

                var resultCount = resultSet.Count(r => r is not null);
                var count = resultCount + exceptions.Count;
                count.Should().Be(dataSet.Length, $"Results: {resultCount}, Exceptions: {exceptions.Count}");
            }

            if (!Debugger.IsAttached) return sw.Elapsed;

            _log.WriteLine("\n## PLINQ Results\n");

            resultSet.ToList().ForEach(ex => _log.WriteLine(ex.ToString()));
            exceptions.Values.ToList().ForEach(ex => _log.WriteLine(ex.ToString()));


            return sw.Elapsed;
        }

        int[] GenerateDataSet(int size = ITERATIONS)
        {
            var dataSet = new int[size];

            var rand = new Random();

            for (var i = 0; i < dataSet.Length; ++i)
            {
                dataSet[i] = rand.Next(250, 2500);
            }

            return dataSet;
        }

        [Theory]
        [InlineData(ITERATIONS / 5, PARALLELISM)]
        [InlineData(ITERATIONS / 2, PARALLELISM)]
        [InlineData(ITERATIONS, PARALLELISM)]
        [InlineData(ITERATIONS * 2, PARALLELISM)]
        [InlineData(ITERATIONS * 5, PARALLELISM)]
        [InlineData(ITERATIONS / 5, PARALLELISM / 2)]
        [InlineData(ITERATIONS / 2, PARALLELISM / 2)]
        [InlineData(ITERATIONS, PARALLELISM / 2)]
        [InlineData(ITERATIONS * 2, PARALLELISM / 2)]
        [InlineData(ITERATIONS * 5, PARALLELISM / 2)]
        [InlineData(ITERATIONS / 5, PARALLELISM / 4)]
        [InlineData(ITERATIONS / 2, PARALLELISM / 4)]
        [InlineData(ITERATIONS, PARALLELISM / 4)]
        [InlineData(ITERATIONS * 2, PARALLELISM / 4)]
        [InlineData(ITERATIONS * 5, PARALLELISM / 4)]
        [InlineData(ITERATIONS / 5, PARALLELISM * 2)]
        [InlineData(ITERATIONS / 2, PARALLELISM * 2)]
        [InlineData(ITERATIONS, PARALLELISM * 2)]
        [InlineData(ITERATIONS * 2, PARALLELISM * 2)]
        [InlineData(ITERATIONS * 5, PARALLELISM * 2)]
        [InlineData(ITERATIONS / 5, PARALLELISM * 4)]
        [InlineData(ITERATIONS / 2, PARALLELISM * 4)]
        [InlineData(ITERATIONS, PARALLELISM * 4)]
        [InlineData(ITERATIONS * 2, PARALLELISM * 4)]
        [InlineData(ITERATIONS * 5, PARALLELISM * 4)]
        public void ValidateContrivedTest(int iterations, int maxParallelism)
        {
            var dataSet = GenerateDataSet(iterations);

            var block = new ThreadBlock<int, string>(
                Processor,
                BlockContinuation);

            block.AddRange(dataSet);

            block.LockList();

            var tokenSource = new CancellationTokenSource(_timeout);
            var sw = new Stopwatch();
            sw.Start();

            block.Execute(maxParallelism, Warmup, ThreadBlockContinuation, tokenSource.Token);

            sw.Stop();
            var blockElapsed = sw.Elapsed;

            _log.WriteLine($"Finished in {blockElapsed:t}\n\n---\n\n");

            tokenSource.IsCancellationRequested.Should().BeFalse();

            var count = block.Results.Count + block.Exceptions.Count;
            count.Should().Be(dataSet.Distinct().Count(), $"Results: {block.Results.Count}, Exceptions: {block.Exceptions.Count}");

            block.Results.Values.ToList().ForEach(ex => _log.WriteLine(ex.ToString()));
            block.Exceptions.Values.ToList().ForEach(ex => _log.WriteLine(ex.ToString()));
        }

        [Theory]
        [InlineData(ITERATIONS / 5, PARALLELISM)]
        [InlineData(ITERATIONS / 2, PARALLELISM)]
        [InlineData(ITERATIONS, PARALLELISM)]
        [InlineData(ITERATIONS * 2, PARALLELISM)]
        [InlineData(ITERATIONS * 5, PARALLELISM)]
        [InlineData(ITERATIONS / 5, PARALLELISM / 2)]
        [InlineData(ITERATIONS / 2, PARALLELISM / 2)]
        [InlineData(ITERATIONS, PARALLELISM / 2)]
        [InlineData(ITERATIONS * 2, PARALLELISM / 2)]
        [InlineData(ITERATIONS * 5, PARALLELISM / 2)]
        [InlineData(ITERATIONS / 5, PARALLELISM / 4)]
        [InlineData(ITERATIONS / 2, PARALLELISM / 4)]
        [InlineData(ITERATIONS, PARALLELISM / 4)]
        [InlineData(ITERATIONS * 2, PARALLELISM / 4)]
        [InlineData(ITERATIONS * 5, PARALLELISM / 4)]
        [InlineData(ITERATIONS / 5, PARALLELISM * 2)]
        [InlineData(ITERATIONS / 2, PARALLELISM * 2)]
        [InlineData(ITERATIONS, PARALLELISM * 2)]
        [InlineData(ITERATIONS * 2, PARALLELISM * 2)]
        [InlineData(ITERATIONS * 5, PARALLELISM * 2)]
        [InlineData(ITERATIONS / 5, PARALLELISM * 4)]
        [InlineData(ITERATIONS / 2, PARALLELISM * 4)]
        [InlineData(ITERATIONS, PARALLELISM * 4)]
        [InlineData(ITERATIONS * 2, PARALLELISM * 4)]
        [InlineData(ITERATIONS * 5, PARALLELISM * 4)]
        public async Task ValidateContrivedTestAsync(int iterations, int maxParallelism)
        {
            var dataSet = GenerateDataSet(iterations);

            var block = new ThreadBlock<int, string>(
                ProcessorAsync,
                BlockContinuationAsync);

            block.AddRange(dataSet);

            block.LockList();

            TimeSpan blockElapsed = default;
            var sw = new Stopwatch();
            var tokenSource = new CancellationTokenSource(_timeout);

            try
            {
                async Task Executor()
                {
                    sw.Start();

                    var list = await block.ExecuteAsync(maxParallelism, WarmupAsync, ThreadBlockContinuationAsync,
                        tokenSource.Token);

                    sw.Stop();
                    blockElapsed = sw.Elapsed;

                    list.Should().NotBeNullOrEmpty();

                    throw new AggregateException("No errors thrown.");
                }

                await Assert.ThrowsAsync<AggregateException>(async () => await Executor());
            }
            catch (Xunit.Sdk.ThrowsException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                Assert.True(false, ex.ToString());
            }
            finally
            {
                if (blockElapsed == default)
                {
                    sw.Stop();
                    blockElapsed = sw.Elapsed;
                }

                _log.WriteLine($"Finished in {blockElapsed:t}\n\n---\n\n");
            }

            tokenSource.IsCancellationRequested.Should().BeFalse();

            //var plinqElapsed = ExecutePlinq(dataSet);

            var count = block.Results.Count + block.Exceptions.Count;
            count.Should().Be(dataSet.Distinct().Count(), $"Results: {block.Results.Count}, Exceptions: {block.Exceptions.Count}");

            block.Results.Values.ToList().ForEach(ex => _log.WriteLine(ex.ToString()));
            block.Exceptions.Values.ToList().ForEach(ex => _log.WriteLine(ex.ToString()));
        }

        TimeSpan PerformBlock(int[] dataSet, int maxParallelism)
        {
            var block = new ThreadBlock<int, string>(
                ProcessorAsync,
                BlockContinuationAsync);

            block.AddRange(dataSet);

            block.LockList();

            var sw = new Stopwatch();
            var tokenSource = new CancellationTokenSource(_timeout);

            try
            {
                sw.Start();

                block.Execute(maxParallelism, Warmup, ThreadBlockContinuation,
                    tokenSource.Token);

                sw.Stop();
            }
            catch (Exception ex)
            {
                // ignore
            }

            return sw.Elapsed;
        }
        async Task<TimeSpan> PerformBlockAsync(int[] dataSet, int maxParallelism)
        {
            var block = new ThreadBlock<int, string>(
                ProcessorAsync,
                BlockContinuationAsync);

            block.AddRange(dataSet);

            block.LockList();

            var sw = new Stopwatch();
            var tokenSource = new CancellationTokenSource(_timeout);

            try
            {
                sw.Start();

                _ = await block.ExecuteAsync(maxParallelism, WarmupAsync, ThreadBlockContinuationAsync,
                    tokenSource.Token);

                sw.Stop();
            }
            catch (Exception ex)
            {
                // ignore
            }

            return sw.Elapsed;
        }

        static TimeSpan blockElapsedTotal = TimeSpan.Zero;
        static TimeSpan blockAsyncElapsedTotal = TimeSpan.Zero;
        static TimeSpan plinqElapsedTotal = TimeSpan.Zero;

        [Theory]
        [InlineData(ITERATIONS / 5, PARALLELISM)]
        [InlineData(ITERATIONS / 2, PARALLELISM)]
        [InlineData(ITERATIONS, PARALLELISM)]
        [InlineData(ITERATIONS * 2, PARALLELISM)]
        [InlineData(ITERATIONS * 5, PARALLELISM)]
        [InlineData(ITERATIONS / 5, PARALLELISM / 2)]
        [InlineData(ITERATIONS / 2, PARALLELISM / 2)]
        [InlineData(ITERATIONS, PARALLELISM / 2)]
        [InlineData(ITERATIONS * 2, PARALLELISM / 2)]
        [InlineData(ITERATIONS * 5, PARALLELISM / 2)]
        [InlineData(ITERATIONS / 5, PARALLELISM / 4)]
        [InlineData(ITERATIONS / 2, PARALLELISM / 4)]
        [InlineData(ITERATIONS, PARALLELISM / 4)]
        [InlineData(ITERATIONS * 2, PARALLELISM / 4)]
        [InlineData(ITERATIONS * 5, PARALLELISM / 4)]
        [InlineData(ITERATIONS / 5, PARALLELISM * 2)]
        [InlineData(ITERATIONS / 2, PARALLELISM * 2)]
        [InlineData(ITERATIONS, PARALLELISM * 2)]
        [InlineData(ITERATIONS * 2, PARALLELISM * 2)]
        [InlineData(ITERATIONS * 5, PARALLELISM * 2)]
        [InlineData(ITERATIONS / 5, PARALLELISM * 4)]
        [InlineData(ITERATIONS / 2, PARALLELISM * 4)]
        [InlineData(ITERATIONS, PARALLELISM * 4)]
        [InlineData(ITERATIONS * 2, PARALLELISM * 4)]
        [InlineData(ITERATIONS * 5, PARALLELISM * 4)]
        public async Task ComparisonTest(int iterations, int maxParallelism)
        {
            var dataSet = GenerateDataSet(iterations);

            var blockElapsed = PerformBlock(dataSet, maxParallelism);
            var blockAsyncElapsed = await PerformBlockAsync(dataSet, maxParallelism);
            var plinqElapsed = ExecutePlinq(dataSet, maxParallelism, false);

            blockElapsedTotal += blockElapsed;
            blockAsyncElapsedTotal += blockAsyncElapsed;
            plinqElapsedTotal += plinqElapsed;

            _log.WriteLine(
                $"\n# Iterations: {iterations}, maxParallelism: {maxParallelism}\n" +
                $"\n\t* block: {blockElapsed:t} " +
                $"\n\t* async: {blockAsyncElapsed:t} " +
                $"\n\t* PLINQ: {plinqElapsed:t}\n");

            _log.WriteLine(
                $"\n# Totals:\n" +
                $"\n\t* block: {blockElapsedTotal:t} " +
                $"\n\t* async: {blockAsyncElapsedTotal:t} " +
                $"\n\t* PLINQ: {plinqElapsedTotal:t}\n");
        }
    }
}
