using System.Collections.Generic;
using System.Linq;

namespace System.Collections.Tests.App
{
	internal class Program
	{
		#region Constant and Static Fields

		private static Int32 concurrentWritersCount = 1024;

		private static Int32 itemsCount = 2000000;

		#endregion

		#region Private methods

		private static void Main(String[] args)
		{
			if (args.Length > 0)
			{
				Int32.TryParse(args[0], out itemsCount);

				Int32.TryParse(args[1], out concurrentWritersCount);
			}

			TestList();

			Console.WriteLine();

			TestDictionary();
		}

		private static void TestDictionary()
		{
			var random = new Random(DateTime.UtcNow.Millisecond);

			var inputItems = new KeyValuePair<Int64, Int64>[itemsCount];

			for (var index = 0; index < itemsCount; index++)
			{
				var value = random.Next();

				inputItems[index] = new KeyValuePair<Int64, Int64>(index % 4, value);
			}

			var result = DictionaryPerformanceTestHelper.RunAllAsync(inputItems, concurrentWritersCount).Result;

			PrintResults(result);
		}

		private static void TestList()
		{
			var random = new Random(DateTime.UtcNow.Millisecond);

			var inputItems = new Int64[itemsCount];

			for (var index = 0; index < itemsCount; index++)
			{
				inputItems[index] = random.Next();
			}

			var result = CollectionLoadTestHelper.RunAllAsync(inputItems, concurrentWritersCount).Result;

			PrintResults(result);
		}

		private static void PrintResults(IEnumerable<CollectionTestResult> testResults)
		{
			Console.WriteLine("Time\t| Pass\t| Type");

			Console.WriteLine("--------------------------------");

			foreach (var testResult in testResults.OrderBy(x => x.ElapsedTime).ThenBy(x => x.Pass))
			{
				Console.WriteLine($"{(Int32) testResult.ElapsedTime.TotalMilliseconds:D5}\t| {testResult.Pass}\t| {testResult.CollectionType.Name} : {testResult.Description}");
			}
		}

		#endregion
	}
}