namespace System.Collections.Tests;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public static class CollectionLoadTestHelper
{
	#region Methods

	/// <summary>
	/// Runs all tests.
	/// </summary>
	/// <typeparam name="TItem">Item type.</typeparam>
	/// <param name="inputItems">Collection of input items.</param>
	/// <param name="producersCount">Count of producers.</param>
	/// <returns>Collection of <see cref="CollectionLoadTestResult"/>.</returns>
	public static async Task<CollectionLoadTestResult[]> RunAllAsync<TItem>(IReadOnlyList<TItem> inputItems, Int32 producersCount)
	{
		var results = new CollectionLoadTestResult[8];

		{
			results[0] = await Test_ConcurrentBag_Interlocked_Async(inputItems, producersCount);
		}

		{
			results[1] = await Test_ConcurrentBag_TryTake_Async(inputItems, producersCount);
		}

		{
			results[2] = await Test_ConcurrentQueue_TryDequeue_Async(inputItems, producersCount);
		}

		{
			results[3] = await Test_ConcurrentQueue_Interlocked_Async(inputItems, producersCount);
		}

		{
			results[4] = await Test_ConcurrentStack_Add_Async(inputItems, producersCount);
		}

		{
			results[5] = await Test_ConcurrentStack_AddRange_Async(inputItems, producersCount);
		}

		{
			results[6] = await Test_List_Lock_Async(inputItems, producersCount);
		}

		{
			results[7] = await Test_Queue_Lock_Async(inputItems, producersCount);
		}

		return results;
	}

	public static CollectionLoadTest<ItemType, ItemType, ItemType, MainCollectionType, List<ItemType>> Create<ItemType, MainCollectionType>
	(
		String description,
		Int32 producersCount,
		IReadOnlyList<ItemType> inputItems,
		Produce<ItemType, ItemType, MainCollectionType> produce,
		Consume<ItemType, MainCollectionType, ItemType, List<ItemType>> consume
	)
		where MainCollectionType : IReadOnlyCollection<ItemType>, new()
	{
		return new CollectionLoadTest<ItemType, ItemType, ItemType, MainCollectionType, List<ItemType>>(description, producersCount, inputItems, produce, consume);
	}

	/// <summary>
	/// A test for <see cref="List{T}"/> type.
	/// </summary>
	/// <typeparam name="ItemType">Type of the items.</typeparam>
	/// <param name="inputItems"></param>
	/// <param name="writersCount"></param>
	/// <returns></returns>
	public static Task<CollectionLoadTestResult> Test_List_Lock_Async<ItemType>
	(
		IReadOnlyList<ItemType> inputItems,
		Int32 writersCount
	)
	{
		var test = Create
		(
			$"lock + Add; lock + ToArray + Clear + AddRange",
			writersCount,
			inputItems,
			(ItemType inputItem, ref List<ItemType> main) =>
			{
				lock (main)
				{
					main.Add(inputItem);
				}
			},
			(ref List<ItemType> main, ref List<ItemType> output) =>
			{
				ItemType[] tempItems;

				lock (main)
				{
					tempItems = [.. main];

					main.Clear();
				}

				output.AddRange(tempItems);
			}
		);

		return test.RunAsync();
	}

	public static Task<CollectionLoadTestResult> Test_Queue_Lock_Async<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
	{
		var test = Create
		(
			$"lock + Enqueue; lock + ToArray + Clear + AddRange",
			writersCount,
			inputItems,
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
					tempItems = [.. interim];

					interim.Clear();
				}

				output.AddRange(tempItems);
			}
		);

		return test.RunAsync();
	}

	public static Task<CollectionLoadTestResult> Test_ConcurrentStack_Add_Async<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
	{
		var test = Create
		(
			$"Push; while TryPop > Add",
			writersCount,
			inputItems,
			(TItem inputItem, ref ConcurrentStack<TItem> source) => source.Push(inputItem),
			(ref ConcurrentStack<TItem> source, ref List<TItem> destination) =>
			{
				while (source.TryPop(out var sourceItem))
				{
					destination.Add(sourceItem);
				}
			}
		);

		return test.RunAsync();
	}

	public static Task<CollectionLoadTestResult> Test_ConcurrentStack_AddRange_Async<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
	{
		var tempItems = new TItem[1024];

		var test = Create
		(
			$"Push; while TryPopRange > AddRange",
			writersCount,
			inputItems,
			(TItem inputItem, ref ConcurrentStack<TItem> source) => source.Push(inputItem),
			(ref ConcurrentStack<TItem> source, ref List<TItem> destination) =>
			{
				Int32 popCount;

				while ((popCount = source.TryPopRange(tempItems)) != 0)
				{
					destination.AddRange(tempItems);
				}
			}
		);

		return test.RunAsync();
	}

	public static Task<CollectionLoadTestResult> Test_ConcurrentQueue_TryDequeue_Async<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
	{
		var test = Create
		(
			$"Enque; while TryDequeue > Add",
			writersCount,
			inputItems,
			(TItem inputItem, ref ConcurrentQueue<TItem> main) => main.Enqueue(inputItem),
			(ref ConcurrentQueue<TItem> main, ref List<TItem> output) =>
			{
				while (main.TryDequeue(out var item))
				{
					output.Add(item);
				}
			}
		);

		return test.RunAsync();
	}

	public static Task<CollectionLoadTestResult> Test_ConcurrentQueue_Interlocked_Async<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
	{
		var test = Create
		(
			$"Enqueue; Intelocked.Exchange + AddRange",
			writersCount,
			inputItems,
			(TItem item, ref ConcurrentQueue<TItem> main) => main.Enqueue(item),
			(ref ConcurrentQueue<TItem> main, ref List<TItem> output) =>
			{
				var old = Interlocked.Exchange(ref main, new ConcurrentQueue<TItem>());

				output.AddRange(old);
			}
		);

		return test.RunAsync();
	}

	public static Task<CollectionLoadTestResult> Test_ConcurrentBag_Interlocked_Async<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
	{
		var test = Create
		(
			"Add; InterlockedExchange + AddRange",
			writersCount,
			inputItems,
			(TItem inputItem, ref ConcurrentBag<TItem> source) => source.Add(inputItem),
			(ref ConcurrentBag<TItem> source, ref List<TItem> destination) =>
			{
				var old = Interlocked.Exchange(ref source, []);

				destination.AddRange(old);
			}
		);

		return test.RunAsync();
	}

	public static Task<CollectionLoadTestResult> Test_ConcurrentBag_TryTake_Async<TItem>(IReadOnlyList<TItem> inputItems, Int32 writersCount)
	{
		var test = Create
			(
				$"Add; while TryTake > Add",
				writersCount,
				inputItems,
				(TItem inputItem, ref ConcurrentBag<TItem> source) => source.Add(inputItem),
				(ref ConcurrentBag<TItem> source, ref List<TItem> destination) =>
				{

					while (source.TryTake(out var item))
					{
						destination.Add(item);
					}
				}
			);

		return test.RunAsync();
	}

	#endregion
}