using System;
using System.Collections.Generic;
using System.Diagnostics;
using GPS.SimpleExtensions;
using GPS.SimpleThreading.Blocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GPS.SimpleThreading.Tests
{
    [TestClass]
    public class ThreadBlockTests
    {
        [TestMethod]
        public void ExecuteThreadBlock()
        {
            var block = new ThreadBlock<string, int>(s =>
            {
                var result = 0;
                return int.TryParse(s, out result) ? result : 0;
            });

            block.AddRange(new List<string>{ "1", "2", "3", "four", "5", "six", "7", "8", "nine", "10"});

            block.LockList();

            block.Execute(5, tasks =>
            {
                Assert.AreEqual(10, block.Results.Count);

                block.Results.ForEach(pair =>
                {
                    Debug.WriteLine($"{pair.Key} - {pair.Value}");
                    $"{pair.Key} - {pair.Value}".ToDebug();
                });
            });
        }
    }
}
