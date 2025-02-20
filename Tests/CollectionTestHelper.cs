// Created by Stas Sultanov.
// Copyright © Stas Sultanov.

namespace Tests;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

public static class CollectionTestHelper
{
	#region Methods

	public static CollectionTest<ItemType, CollectionType> Create<ItemType, CollectionType>
	(
		String description,
		Int32 producersCount,
		TimeSpan consumerDelay,
		IReadOnlyList<ItemType> inputItems,
		Func<CollectionType, Boolean> hasItems,
		Action<ItemType, CollectionType> produce,
		Action<CollectionType, List<ItemType>> consume
	)
		where CollectionType : class, new()
	{
		return new CollectionTest<ItemType, CollectionType>(description, producersCount, consumerDelay, inputItems, hasItems, produce, consume);
	}

	/// <summary>
	/// Runs all tests.
	/// </summary>
	/// <typeparam name="ItemType">Item type.</typeparam>
	/// <param name="inputItems">Collection of input items.</param>
	/// <param name="producersCount">Count of producers.</param>
	/// <returns>Collection of <see cref="CollectionTestResult"/>.</returns>
	public static async Task<CollectionTestResult[]> RunAllCollectionTestsAsync<ItemType>
	(
		IReadOnlyList<ItemType> inputItems,
		Int32 producersCount,
		TimeSpan consumerDelay
	)
	{
		var results = new CollectionTestResult[8];

		{
			results[0] = await Test_BlockingCollection_Async(inputItems, producersCount, consumerDelay);
		}

		{
			results[1] = await Test_ConcurrentBag_Async(inputItems, producersCount, consumerDelay);
		}

		{
			results[2] = await Test_ConcurrentQueue_Async(inputItems, producersCount, consumerDelay);
		}

		{
			results[3] = await Test_ConcurrentStack_Add_Async(inputItems, producersCount, consumerDelay);
		}

		{
			results[4] = await Test_ConcurrentStack_AddRange_Async(inputItems, producersCount, consumerDelay);
		}

		{
			results[5] = await Test_List_Async(inputItems, producersCount, consumerDelay);
		}

		{
			results[6] = await Test_Queue_Async(inputItems, producersCount, consumerDelay);
		}

		{
			results[7] = await Test_SupperQueue_Async(inputItems, producersCount, consumerDelay);
		}

		return results;
	}

	public static Task<CollectionTestResult> Test_BlockingCollection_Async<ItemType>
	(
		IReadOnlyList<ItemType> inputItems,
		Int32 producersCount,
		TimeSpan consumerDelay
	)
	{
		var test = Create<ItemType, BlockingCollection<ItemType>>
		(
			$"Add; while TryTake > Add",
			producersCount,
			consumerDelay,
			inputItems,
			(main) => main.Count != 0,
			(inputItem, main) => main.Add(inputItem),
			(main, output) =>
			{
				while (main.TryTake(out var item))
				{
					output.Add(item);
				}
			}
		);

		return test.RunAsync();
	}

	public static Task<CollectionTestResult> Test_ConcurrentBag_Async<ItemType>
	(
		IReadOnlyList<ItemType> inputItems,
		Int32 producersCount,
		TimeSpan consumerDelay
	)
	{
		var test = Create<ItemType, ConcurrentBag<ItemType>>
		(
			$"Add; while TryTake > Add",
			producersCount,
			consumerDelay,
			inputItems,
			(main) => !main.IsEmpty,
			(inputItem, main) => main.Add(inputItem),
			(main, output) =>
			{
				while (main.TryTake(out var item))
				{
					output.Add(item);
				}
			}
		);

		return test.RunAsync();
	}

	public static Task<CollectionTestResult> Test_ConcurrentQueue_Async<ItemType>
	(
		IReadOnlyList<ItemType> inputItems,
		Int32 producersCount,
		TimeSpan consumerDelay
	)
	{
		var test = Create<ItemType, ConcurrentQueue<ItemType>>
		(
			$"Enque; while TryDequeue > Add",
			producersCount,
			consumerDelay,
			inputItems,
			(main) => !main.IsEmpty,
			(inputItem, main) => main.Enqueue(inputItem),
			(main, output) =>
			{
				while (main.TryDequeue(out var item))
				{
					output.Add(item);
				}
			}
		);

		return test.RunAsync();
	}

	/// <summary>
	/// A test for <see cref="List{T}"/> type.
	/// </summary>
	/// <typeparam name="ItemType">Type of the items.</typeparam>
	/// <param name="inputItems"></param>
	/// <param name="producersCount"></param>
	/// <returns></returns>
	public static Task<CollectionTestResult> Test_List_Async<ItemType>
	(
		IReadOnlyList<ItemType> inputItems,
		Int32 producersCount,
		TimeSpan consumerDelay
	)
	{
		var test = Create<ItemType, List<ItemType>>
		(
			$"lock + Add; lock + ToArray + Clear + AddRange",
			producersCount,
			consumerDelay,
			inputItems,
			(main) => main.Count!=0,
			(inputItem, main) =>
			{
				lock (main)
				{
					main.Add(inputItem);
				}
			},
			(main, output) =>
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

	public static Task<CollectionTestResult> Test_Queue_Async<ItemType>
	(
		IReadOnlyList<ItemType> inputItems,
		Int32 producersCount,
		TimeSpan consumerDelay
	)
	{
		var test = Create<ItemType, Queue<ItemType>>
		(
			$"lock + Enqueue; lock + ToArray + Clear + AddRange",
			producersCount,
			consumerDelay,
			inputItems,
			(main) => main.Count!=0,
			(inputItem, main) =>
			{
				lock (main)
				{
					main.Enqueue(inputItem);
				}
			},
			(main, output) =>
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

	public static Task<CollectionTestResult> Test_ConcurrentStack_Add_Async<ItemType>
	(
		IReadOnlyList<ItemType> inputItems,
		Int32 producersCount,
		TimeSpan consumerDelay
	)
	{
		var test = Create<ItemType, ConcurrentStack<ItemType>>
		(
			$"Push; while TryPop > Add",
			producersCount,
			consumerDelay,
			inputItems,
			(main) => !main.IsEmpty,
			(inputItem, main) => main.Push(inputItem),
			(main, output) =>
			{
				while (main.TryPop(out var mainItem))
				{
					output.Add(mainItem);
				}
			}
		);

		return test.RunAsync();
	}

	public static Task<CollectionTestResult> Test_SupperQueue_Async<ItemType>
(
	IReadOnlyList<ItemType> inputItems,
	Int32 producersCount,
	TimeSpan consumerDelay
)
	{
		var test = Create<ItemType, SupperQueue<ItemType>>
		(
			$"Add; Evict + AddRange",
			producersCount,
			consumerDelay,
			inputItems,
			(main) => !main.IsEmpty,
			(inputItem, main) => main.Add(inputItem),
			(main, output) =>
			{
				var items = main.Evict();

				output.AddRange(items);
			}
		);

		return test.RunAsync();
	}

	public static Task<CollectionTestResult> Test_ConcurrentStack_AddRange_Async<ItemType>
	(
		IReadOnlyList<ItemType> inputItems,
		Int32 producersCount,
		TimeSpan consumerDelay
	)
	{
		var test = Create<ItemType, ConcurrentStack<ItemType>>
		(
			$"Push; while TryPopRange > Add",
			producersCount,
			consumerDelay,
			inputItems,
			(main) => !main.IsEmpty,
			(inputItem, main) => main.Push(inputItem),
			( main, output) =>
			{
				var popItems = new ItemType[1024];

				Int32 popCount;

				while ((popCount = main.TryPopRange(popItems)) != 0)
				{
					for (var popIndex = 0; popIndex < popCount; popIndex++)
					{
						var popItem = popItems[popIndex];

						output.Add(popItem);
					}
				}
			}
		);

		return test.RunAsync();
	}

	#endregion
}