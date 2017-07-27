# SimpleMVC
## What is it?
SimpleThreading is a set of classes to ease your multithreading.  There are
two thread-safe collections: ThreadSafeList<T> and ThreadSafeDictionary<K,V>.
Additionally, there is a very simple ThreadBlock class to work with that 
allows you to get a result from each delegate, unlike the ActionBlock Microsoft
provides.

## How do you use it?
SimpleThreading is a stand-alone library that you can use very easily.  

### ThreadSafeList<T>
Example:

  //...
  var list = new ThreadSafeList<char>();
  var str = new string('-', 100);
  list.AddRange(str.ToCharArray());

  var plist = list.AsParallel();
  plist.ForEach(s => s = s - 1);

Notice you do not need to wrap the value assignment in a lock()...

### ThreadSafeDictionary<K, V>
Example:

  // ...
  var d = new ThreadSafeDictionary<string, int>();
  for(int i = 0; i < 100; i++) d.Add(i.ToString(), Math.Pow(i, 2));

  var plist = d.Keys.AsParallel();
  Parallel.ForEach(k => d[k] = Math.Sqrt(v));

Again, notice that you don't need a lock on the dictionary.

### ThreadBlock
Example: (from unit test)

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

## Where can I get it?

SimpleThreading is available on Nuget.org for easy inclusion in your .Net application
from within Visual Studio.  Simply open the Package Manager Console and type:

  install-package GPS.SimpleThreading
  
 That's it.  Nuget will go get the dependencies (in this case Newtonsoft.JSON
 and GPS.SimpleDI) as well as the 
 [SimpleMVC](https://www.nuget.org/packages/GPS.SimpleMVC/) package and add 
 them to your solution.
 
 Alternatively, you may download the package from
 [GitHub](https://github.com/gatewayprogrammingschool/SimpleMVC/releases).

## Where can I get help?
You can log and track issues on [GitHub](https://github.com/gatewayprogrammingschool/SimpleMVC/issues)
or you can send an email to ninja@gatewayprogramming.school.

## What's this Gateway Programming School?
Gateway Programming School (or GPS for short) is a programming school in
Clarksville, TN that is funding its startup costs through the creation of
useful applications, frameworks and text books.  GPS is a for profit school
with the express goal of providing a first-class education in business-oriented
computer programming in C#.


