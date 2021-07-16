# SimpleMVC

## What is it?

SimpleThreading is a class to ease your multithreading.  
There is a very simple `ThreadBlock` class to work with that 
allows you to get a result from each delegate call, unlike the `ActionBlock` Microsoft
provides.  `ThreadBlock` provides by synchronous and async execution paths.
`ThreadBlock` can be left unlocker in which case it acts as a threaded pipeline.

## How do you use it?

SimpleThreading is a stand-alone library that you can use very easily.  Simply import the
NuGet package `__GPS.SimpleThreading__` into your project.

### ThreadBlock

_**Example**_

```csharp
    // Structure to write to on-the-fly.
    var results = new ConcurrentDictionary<string, int>();

    // Using Synchronous Callbacks
    var block = new ThreadBlock<string, int>(
        // Lambda defining processing action per item
        item => int.TryParse(item, out var result) ? result : 0,
        // Lambda defining processing action for end of batch
        results => 
        {
            Debug.WriteLine($"Sum: {results.Sum(r => r?.result ?? 0)}");
        });
        
    // Using Asynchronous Callbacks
    var block = new ThreadBlock<string, int>(
        // Lambda defining processing action per item
        item => Task.FromResult(int.TryParse(item, out var result) ? result : 0),
        // Lambda defining processing action for end of batch
        results => 
        {
            Debug.WriteLine($"Sum: {results.Sum(r => r?.result ?? 0)}");
            return Task.CompletedTask;
        });
        
    // Add data to be processed.
    block.AddRange(new List<string>
        { "1", "2", "3", "four", "5", "six", "7", "8", "nine", "10"});

    // Lock the list, preventing data from being added during processing.
    // If left out, the Add methods can be called at any time to use the ThredBlock
    // as an Asynchronous Pipeline.
    block.LockList();

    // Execute the batch Synchronously
    block.Execute(
        // Max Degree of Parallelism
        5,  
        // Warmup code for the item.  Executed before the 
        // main lambda.
        (item) => { Debug.WriteLine($"Processing {item}."); },
        // Continuation code per item.
        (item, result) =>
        {
            Debug.WriteLine("Processed {item} to {result}.");
        });

    // Execute the batch Asynchronously
    await block.ExecuteAsync(
        // Max Degree of Parallelism
        5,  
        // Warmup code for the item.  Executed before the 
        // main lambda.
        (item) => { Debug.WriteLine($"Processing {item}."); return Task.CompletedTask; },
        // Continuation code per item.
        (item, result) =>
        {
             Debug.WriteLine("Processed {item} to {result}.");
             return Task.CompletedTask;
        });

    // Display completed results
    results.ToList().ForEach(
        r => Console.Debug($"{r.Key} - {r.Value}"));
```

## Where can I get it?

SimpleThreading is available on [Nuget.org](https://nuget.org/packages/GPS.SimpleThreading) for easy inclusion in your .Net application
from within Visual Studio.  Simply open the Package Manager Console and type:

 `install-package GPS.SimpleThreading`
 
 Alternatively, you may download the package from
 [GitHub](https://github.com/gatewayprogrammingschool/SimpleThreading/releases).

## Where can I get help?

You can log and track issues on [GitHub](https://github.com/gatewayprogrammingschool/SimpleThreading/issues)
or you can send an email to [The Sharp Ninja](mailto:ninja@thesharp.ninja).

## What's this Gateway Programming School?

Gateway Programming School (or GPS for short) is a programming school in
Clarksville, TN that is funding its startup costs through the creation of
useful applications, frameworks and text books.  GPS is a for profit school
with the express goal of providing a first-class education in business-oriented
computer programming in DotNet Core and related technologies.