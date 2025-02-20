namespace System.Collections.Tests.App;

using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class Program
{
	public static async Task Main(String[] args)
	{
		var itemsCount = 10000000;

		var producersCount = 128;

		var outputFile = "result.md";

		if (args.Length > 0)
		{
			_ = Int32.TryParse(args[0], out itemsCount);

			_ = Int32.TryParse(args[1], out producersCount);
		}

		using var writer = new StreamWriter(outputFile);

		writer.WriteLine("# Test");

		writer.WriteLine();

		writer.WriteLine("## Input Parameters");

		writer.WriteLine("\nItems: **{0}**", itemsCount);

		writer.WriteLine("\nProudcers: **{0}**", producersCount);

		var collectionResult = await TestCollectionAsync(itemsCount, producersCount);

		WriteResults(writer, collectionResult);
	}

	private static async Task<CollectionLoadTestResult[]> TestDictionaryAsync(Int32 itemsCount, Int32 producersCount)
	{
		var random = new Random(DateTime.UtcNow.Millisecond);

		var inputItems = new KeyValuePair<Int64, Int64>[itemsCount];

		for (var index = 0; index < itemsCount; index++)
		{
			var value = random.Next();

			inputItems[index] = new KeyValuePair<Int64, Int64>(index % 4, value);
		}

		var result = await DictionaryPerformanceTestHelper.RunAllAsync(inputItems, producersCount);

		return result;
	}

	private static async Task<CollectionLoadTestResult[]> TestCollectionAsync(Int32 itemsCount, Int32 producersCount)
	{
		var random = new Random(DateTime.UtcNow.Millisecond);

		var inputItems = new Int64[itemsCount];

		for (var index = 0; index < itemsCount; index++)
		{
			inputItems[index] = random.Next();
		}

		var result = await CollectionLoadTestHelper.RunAllAsync(inputItems, producersCount);

		return result;
	}

	#region Methods: Helpers

	private static void WriteResults(StreamWriter writer, IEnumerable<CollectionLoadTestResult> testResults)
	{
		writer.WriteLine("\n## Results");

		writer.WriteLine("|     Time | Pass  | Main |   Output | Type                     | Description");

		writer.WriteLine("| -------: | :---- | ---: | -------: | :----------------------- | :--");

		foreach (var testResult in testResults.OrderBy(x => x.ElapsedTime).ThenBy(x => x.Pass))
		{
			writer.WriteLine
			(
				"| {0, 8} | {1, -5} | {2, 4} | {3, 8} | {4, -24} | {5}",
				(Int32) testResult.ElapsedTime.TotalMilliseconds,
				testResult.Pass,
				testResult.MainCount,
				testResult.OutputCount,
				testResult.CollectionType.GetNormalizedName(@"\<", @">"),
				testResult.Description
			);
		}

		writer.WriteLine();
	}

	public static String GetNormalizedName(this Type type, String open, String close)
	{
		if (!type.IsGenericType)
		{
			return type.Name;
		}

		var builder = new StringBuilder();
		_ = builder.Append(type.Name.AsSpan(0, type.Name.IndexOf('`')));
		_ = builder.Append(open);
		var genericArguments = type.GetGenericArguments();
		for (var i = 0; i < genericArguments.Length; i++)
		{
			if (i > 0)
			{
				_ = builder.Append(", ");
			}

			_ =

			_ = builder.Append(GetNormalizedName(genericArguments[i], open, close));
		}

		_ = builder.Append(close);
		return builder.ToString();
	}

	#endregion
}