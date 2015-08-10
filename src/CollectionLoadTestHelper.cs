using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace System.Collections.Tests
{
	public static class CollectionLoadTestHelper
	{
		#region Methods

		public static CollectionLoadTest<TItem, TItem, TItem, TInterimCollection, TOutputCollection>
			Create<TItem, TInterimCollection, TOutputCollection>
			(
			String name,
			IReadOnlyList<TItem> inputItems,
			Int32 writersCount,
			CollectionLoadTest<TItem, TItem, TItem, TInterimCollection, TOutputCollection>.Put put,
			CollectionLoadTest<TItem, TItem, TItem, TInterimCollection, TOutputCollection>.Move move
			)
			where TInterimCollection : IReadOnlyCollection<TItem>, new()
			where TOutputCollection : ICollection<TItem>, new()
		{
			return new CollectionLoadTest<TItem, TItem, TItem, TInterimCollection, TOutputCollection>(name, inputItems, writersCount, put, move);
		}

		public static Task<CollectionTestResult> RunListLockTestAsync<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
		{
			var test = Create
				(
					$"source: lock, ToArray, Clear # destination: AddRange",
					inputItems,
					writersCount,
					(TItem inputItem, ref List<TItem> interim) =>
					{
						lock (interim)
						{
							interim.Add(inputItem);
						}
					},
					(ref List<TItem> interim, ref List<TItem> output) =>
					{
						TItem[] tempItems;

						lock (interim)
						{
							tempItems = interim.ToArray();

							interim.Clear();
						}

						output.AddRange(tempItems);
					}
				);

			return test.RunAsync();
		}

		public static Task<CollectionTestResult> RunQueueLockTestAsync<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
		{
			var test = Create
				(
					$"source: lock, ToArray, Clear # destination: AddRange",
					inputItems,
					writersCount,
					(TItem inputItem, ref Queue<TItem> interim) =>
					{
						lock (interim)
						{
							interim.Enqueue(inputItem);
						}
					},
					(ref Queue<TItem> interim, ref List<TItem> output) =>
					{
						TItem[] tempItems;

						lock (interim)
						{
							tempItems = interim.ToArray();

							interim.Clear();
						}

						output.AddRange(tempItems);
					}
				);

			return test.RunAsync();
		}

		public static Task<CollectionTestResult> RunConcurrentStackTestAsync<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
		{
			var test = Create
				(
					$"source: while TryPop # destination: Add",
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

			return test.RunAsync();
		}

		public static Task<CollectionTestResult> RunConcurrentStackRangeTestAsync<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
		{
			var tempItems = new TItem[1024];

			var test = Create
				(
					$"source: while TryPopRange # destination: Add",
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

			return test.RunAsync();
		}

		public static Task<CollectionTestResult> RunConcurrentQueueTestAsync<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
		{
			var test = Create
				(
					$"source: while TryDequeue # destination: Add",
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

			return test.RunAsync();
		}

		public static Task<CollectionTestResult> RunConcurrentQueueInterlockedTestAsync<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
		{
			var test = Create
				(
					$"source: Intelocked.Exchange # destination: AddRange",
					inputItems,
					writersCount,
					(TItem inputItem, ref ConcurrentQueue<TItem> source) => source.Enqueue(inputItem),
					(ref ConcurrentQueue<TItem> source, ref List<TItem> destination) =>
					{
						var old = Interlocked.Exchange(ref source, new ConcurrentQueue<TItem>());

						destination.AddRange(old);
					}
				);

			return test.RunAsync();
		}

		public static Task<CollectionTestResult> RunConcurrentBagInterlockedTestAsync<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
		{
			var test = Create
				(
					"source: replace # destination: AddRange",
					inputItems,
					writersCount,
					(TItem inputItem, ref ConcurrentBag<TItem> source) => source.Add(inputItem),
					(ref ConcurrentBag<TItem> source, ref List<TItem> destination) =>
					{
						var old = Interlocked.Exchange(ref source, new ConcurrentBag<TItem>());

						destination.AddRange(old);
					}
				);

			return test.RunAsync();
		}

		public static Task<CollectionTestResult> RunConcurrentBagTestAsync<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
		{
			var test = Create
				(
					$"source: while try take # destination: Add",
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

			return test.RunAsync();
		}

		public static async Task<IReadOnlyCollection<CollectionTestResult>> RunAllAsync<TItem>(IReadOnlyList<TItem> inputItems, Int32 concurrentWritersCount)
		{
			var results = new CollectionTestResult[8];

			{
				results[0] = await RunListLockTestAsync(inputItems, concurrentWritersCount);
			}

			{
				results[1] = await RunConcurrentQueueTestAsync(inputItems, concurrentWritersCount);
			}

			{
				results[2] = await RunConcurrentQueueInterlockedTestAsync(inputItems, concurrentWritersCount);
			}

			{
				results[3] = await RunConcurrentBagTestAsync(inputItems, concurrentWritersCount);
			}

			{
				results[4] = await RunConcurrentBagInterlockedTestAsync(inputItems, concurrentWritersCount);
			}

			{
				results[5] = await RunConcurrentStackRangeTestAsync(inputItems, concurrentWritersCount);
			}

			{
				results[6] = await RunConcurrentStackTestAsync(inputItems, concurrentWritersCount);
			}

			{
				results[7] = await RunQueueLockTestAsync(inputItems, concurrentWritersCount);
			}

			return results;
		}

		#endregion
	}
}