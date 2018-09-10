﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using GPS.SimpleExtensions;
using GPS.SimpleThreading.Blocks;
using Xunit;

namespace GPS.SimpleThreading.Tests
{
    public class ThreadBlockTests
    {
        [Fact]
        public void ExecuteThreadBlock()
        {
            var block = new ThreadBlock<string, int>(s =>
            {
                var result = 0;
                return int.TryParse(s, out result) ? result : 0;
            });

            block.AddRange(new List<string>{ "1", "2", "3", "four", "5", "six", "7", "8", "nine", "10"});

            block.LockList();

            block.Execute(5, null, tasks =>
            {
                Assert.Equal(10, block.Results.Count);

                Parallel.ForEach(block.Results, pair =>
                {
                    Debug.WriteLine($"{pair.Key} - {pair.Value}");
                    $"{pair.Key} - {pair.Value}".ToDebug();
                });
            });
        }
    }
}
