using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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

		private const Int32 itemsCount = Int32.MaxValue / 128;

		private static readonly Int64[] items;

		#endregion

		#region Constructor

		static MultipleWritersAndOneReaderTest()
		{
			var random = new Random(DateTime.UtcNow.Millisecond);

			items = new Int64[itemsCount];

			for (var index = 0; index < itemsCount; index++)
			{
				items[index] = random.Next();
			}
		}

		#endregion

		#region Test methods

		[TestMethod]
		public async Task ConcurrentStackTryPop()
		{
			// Create test
			var test = CreateConcurrentStackTest(items, concurrentWritersCount);

			// Run test
			var testResult = await test.RunAsync();

			// Print results
			PrintResults(testResult);
		}

		[TestMethod]
		public async Task ConcurrentStackTryPopRange()
		{
			// Create test
			var test = CreateConcurrentStackRangeTest(items, concurrentWritersCount);

			// Run test
			var testResult = await test.RunAsync();

			// Print results
			PrintResults(testResult);
		}

		[TestMethod]
		public async Task ConcurrentBagInterlockedExchange()
		{
			// Create test
			var test = CreateConcurrentBagWitInterlockedExchangeTest(items, concurrentWritersCount);

			// Run test
			var testResult = await test.RunAsync();

			// Print results
			PrintResults(testResult);
		}

		[TestMethod]
		public async Task ConcurrentBag()
		{
			// Create test
			var test = CreateConcurrentBagTest(items, concurrentWritersCount);

			// Run test
			var testResult = await test.RunAsync();

			// Print results
			PrintResults(testResult);
		}

		[TestMethod]
		public async Task ListLock()
		{
			// Create test
			var test = CreateListLockTest(items, concurrentWritersCount);

			// Run test
			var testResult = await test.RunAsync();

			// Print results
			PrintResults(testResult);
		}

		[TestMethod]
		public async Task Compare()
		{
			var results = new[]
			{
				await CreateListLockTest(items, concurrentWritersCount).RunAsync(),
				await CreateConcurrentQueueTest(items, concurrentWritersCount).RunAsync(),
				await CreateConcurrentStackTest(items, concurrentWritersCount).RunAsync(),
				await CreateConcurrentStackRangeTest(items, concurrentWritersCount).RunAsync(),
				await CreateConcurrentBagTest(items, concurrentWritersCount).RunAsync(),
				await CreateConcurrentBagWitInterlockedExchangeTest(items, concurrentWritersCount).RunAsync(),
			};

			// Print results
			PrintResults(results);
		}

		#endregion

		#region Private methods

		private static CollectionTestHelper<TItem, List<TItem>, List<TItem>> CreateListLockTest<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
		{
			return CollectionTestHelper.Create
				(
					$"type: List<T> # source: lock, ToArray, Clear # destination AddRange",
					inputItems,
					writersCount,
					(TItem inputItem, ref List<TItem> source) =>
					{
						lock (source)
						{
							source.Add(inputItem);
						}
					},
					(ref List<TItem> source, ref List<TItem> destination) =>
					{
						TItem[] tempItems;

						lock (source)
						{
							tempItems = source.ToArray();

							source.Clear();
						}

						destination.AddRange(tempItems);
					}
				);
		}

		private static CollectionTestHelper<TItem, ConcurrentStack<TItem>, List<TItem>> CreateConcurrentStackTest<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
		{
			return CollectionTestHelper.Create
				(
					$"type: CoucurrentStack<T> # source: while TryPop # destination: Add",
					inputItems,
					writersCount,
					(TItem inputItem, ref ConcurrentStack<TItem> source) => source.Push(inputItem),
					(ref ConcurrentStack<TItem> source, ref List<TItem> destination) =>
					{
						TItem sourceItem;

						while (source.TryPop(out sourceItem))
						{
							destination.Add(sourceItem);
						}
					}
				);
		}

		private static CollectionTestHelper<TItem, ConcurrentStack<TItem>, List<TItem>> CreateConcurrentStackRangeTest<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
		{
			var tempItems = new TItem[1024];

			return CollectionTestHelper.Create
				(
					$"type: CoucurrentStack<T> # source: while TryPopRange # destination: Add",
					inputItems,
					writersCount,
					(TItem inputItem, ref ConcurrentStack<TItem> source) => source.Push(inputItem),
					(ref ConcurrentStack<TItem> source, ref List<TItem> destination) =>
					{
						Int32 popCount;

						while ((popCount = source.TryPopRange(tempItems)) != 0)
						{
							for (var index = 0; index < popCount; index++)
							{
								destination.Add(tempItems[index]);
							}
						}
					}
				);
		}

		private static CollectionTestHelper<TItem, ConcurrentQueue<TItem>, List<TItem>> CreateConcurrentQueueTest<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
		{
			return CollectionTestHelper.Create
				(
					$"type: ConcurrentQueue<T> # source: while TryDequeue # destination: Add",
					inputItems,
					writersCount,
					(TItem inputItem, ref ConcurrentQueue<TItem> source) => source.Enqueue(inputItem),
					(ref ConcurrentQueue<TItem> source, ref List<TItem> destination) =>
					{
						TItem sourceItem;

						while (source.TryDequeue(out sourceItem))
						{
							destination.Add(sourceItem);
						}
					}
				);
		}

		private static CollectionTestHelper<TItem, ConcurrentBag<TItem>, List<TItem>> CreateConcurrentBagWitInterlockedExchangeTest<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
		{
			return CollectionTestHelper.Create
				(
					"type: CoucurrentBage<T> # source: replace # destination: AddRange",
					inputItems,
					writersCount,
					(TItem inputItem, ref ConcurrentBag<TItem> source) => source.Add(inputItem),
					(ref ConcurrentBag<TItem> source, ref List<TItem> destination) =>
					{
						var old = Interlocked.Exchange(ref source, new ConcurrentBag<TItem>());

						destination.AddRange(old);
					}
				);
		}

		private static CollectionTestHelper<TItem, ConcurrentBag<TItem>, List<TItem>> CreateConcurrentBagTest<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
		{
			return CollectionTestHelper.Create
				(
					$"type: CoucurrentBage<T> # source: while try take # destination: Add",
					inputItems,
					writersCount,
					(TItem inputItem, ref ConcurrentBag<TItem> source) => source.Add(inputItem),
					(ref ConcurrentBag<TItem> source, ref List<TItem> destination) =>
					{
						TItem item;

						while (source.TryTake(out item))
						{
							destination.Add(item);
						}
					}
				);
		}

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
		private static void PrintResults(params CollectionTestResult[] testResults)
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