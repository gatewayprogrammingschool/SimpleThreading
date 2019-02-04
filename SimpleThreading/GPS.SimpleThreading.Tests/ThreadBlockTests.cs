using System;
using System.Collections.Concurrent;
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
        public void ExecuteThreadBlock()
        {
            var block = new ThreadBlock<string, int>(
                s =>
                {
                    var result = 0;
                    if (int.TryParse(s, out result))
                    {
                        return result;
                    }

                    return 0;
                },
                r =>
                {
                    Assert.NotEmpty(r);
                    Assert.Equal(10, r.Length);
                });

            block.AddRange(new string[] { "1", "2", "3", "42", "5", "-6", "7", "8", "11", "10" });

            block.LockList();

            var results = new ConcurrentBag<(string, int)?>();

            block.Execute(5, null,
            (item, result) =>
            {
                result.AssertParameterNotNull("Result should never be null.", nameof(result));

                _log.WriteLine($"{(result.Value.data ?? "null")} - {(result.Value.result)}");
                
                results.Add(result);

                var parsed = 0;
                if (int.TryParse(result.Value.data, out parsed))
                {
                    Xunit.Assert.Equal(result.Value.result, parsed);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"{result.Value.data} does not parse to {result.Value.result}");
                }
            });

            _log.WriteLine($"Results Count: {results.Count}");
            Xunit.Assert.NotEmpty(results);
            Xunit.Assert.Equal(10, results.Count);
        }
    }
}
