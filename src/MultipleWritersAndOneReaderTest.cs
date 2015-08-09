using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.Collections.Tests
{
	/// <summary>
	/// Provides a set of tests of the <see cref="System.Collections" />.
	/// </summary>
	[TestClass]
	public sealed class MultipleWritersAndOneReaderTest
	{
		#region Constant and Static Fields

		private const Int32 concurrentWritersCount = 512;

		private const Int32 itemsCount = Int32.MaxValue / 1024;

		private static readonly Int64[] inputItems;

		#endregion

		#region Constructor

		static MultipleWritersAndOneReaderTest()
		{
			var random = new Random(DateTime.UtcNow.Millisecond);

			inputItems = new Int64[itemsCount];

			for (var index = 0; index < itemsCount; index++)
			{
				inputItems[index] = random.Next();
			}
		}

		#endregion

		#region Test methods

		[TestMethod]
		public async Task ConcurrentStackTryPop()
		{
			// Create test
			var test = CollectionLoadTestHelper.RunConcurrentStackTestAsync(inputItems, concurrentWritersCount);

			// Run test
			var testResult = await test.RunAsync();

			// Print results
			PrintResults(testResult);
		}

		[TestMethod]
		public async Task ConcurrentStackTryPopRange()
		{
			// Create test
			var test = CollectionLoadTestHelper.CreateConcurrentStackRangeTest(inputItems, concurrentWritersCount);

			// Run test
			var testResult = await test.RunAsync();

			// Print results
			PrintResults(testResult);
		}

		[TestMethod]
		public async Task ConcurrentBagInterlockedExchange()
		{
			// Create test
			var test = CollectionLoadTestHelper.CreateConcurrentBagWitInterlockedExchangeTest(inputItems, concurrentWritersCount);

			// Run test
			var testResult = await test.RunAsync();

			// Print results
			PrintResults(testResult);
		}

		[TestMethod]
		public async Task ConcurrentBag()
		{
			// Create test
			var test = CollectionLoadTestHelper.CreateConcurrentBagTest(inputItems, concurrentWritersCount);

			// Run test
			var testResult = await test.RunAsync();

			// Print results
			PrintResults(testResult);
		}

		[TestMethod]
		public async Task ListLock()
		{
			// Create test
			var test = CollectionLoadTestHelper.CreateListLockTest(inputItems, concurrentWritersCount);

			// Run test
			var testResult = await test.RunAsync();

			// Print results
			PrintResults(testResult);
		}

		[TestMethod]
		public async Task Compare()
		{
			var results = await CollectionLoadTestHelper.RunAllAsync(inputItems, concurrentWritersCount);

			// Print results
			PrintResults(results);
		}

		#endregion

		#region Private methods

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void PrintResults(CollectionTestResult testResult)
		{
			Trace.TraceInformation($"Test {testResult.Description} :");

			Trace.TraceInformation($"\tPass \t\t\t:\t {testResult.Pass}");

			Trace.TraceInformation($"\tTime \t\t\t:\t {testResult.ElapsedTime.TotalMilliseconds}ms");

			Trace.TraceInformation($"\tWriters count \t\t:\t {testResult.WritersCount}");

			Trace.TraceInformation($"\tInput count \t\t:\t {testResult.InputCount}");

			Trace.TraceInformation($"\tSource count \t\t:\t {testResult.InterimCount}");

			Trace.TraceInformation($"\tDestination count \t:\t {testResult.OutputCount}");
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void PrintResults(IReadOnlyCollection<CollectionTestResult> testResults)
		{
			Trace.TraceInformation($"Pass \t| Time \t| Writers count | Input Count | Interim Count | Output Count | Description ");

			foreach (var testResult in testResults.OrderBy(x => x.Pass).ThenBy(x => x.ElapsedTime))
			{
				Trace.TraceInformation($"{testResult.Pass} \t| {(Int32) testResult.ElapsedTime.TotalMilliseconds} \t| {testResult.WritersCount} | {testResult.InputCount} | {testResult.InterimCount} | {testResult.OutputCount} | {testResult.Description} ");
			}
		}

		#endregion
	}
}