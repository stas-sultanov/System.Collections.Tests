// Created by Stas Sultanov.
// Copyright © Stas Sultanov.

namespace Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

/// <summary>
/// Represents a load test for collections, allowing concurrent writing and moving of items between collections.
/// </summary>
/// <typeparam name="ItemType">The type of items.</typeparam>
/// <typeparam name="CollectionType">The type of the main collection.</typeparam>
/// <param name="description">The description of the test.</param>
/// <param name="producersCount">The count of concurrent writers.</param>
/// <param name="consumerDelay">Amount of time for consumer to sleep before next attempt.</param>
/// <param name="input">The input collection.</param>
/// <param name="produce">A delegate to the method that produces items by taking from <paramref name="input"/> collection and putting into the main collection.</param>
/// <param name="consume">The delegate to move items from the main collection to the output collection.</param>
/// <exception cref="ArgumentNullException">if <paramref name="description"/>, <paramref name="input"/>, <paramref name="produce"/>, or <paramref name="consume"/> is null. </exception>
public sealed class CollectionTest<ItemType, CollectionType>
(
	String description,
	Int32 producersCount,
	TimeSpan consumerDelay,
	IReadOnlyList<ItemType> input,
	Func<CollectionType, Boolean> hasItems,
	Action<ItemType, CollectionType> produce,
	Action<CollectionType, List<ItemType>> consume
)
	where CollectionType : new()
{
	#region Fields

	private readonly Action<CollectionType, List<ItemType>> consume = consume ?? throw new ArgumentNullException(nameof(consume));
	private readonly String description = description ?? throw new ArgumentNullException(nameof(description));
	private readonly IReadOnlyList<ItemType> input = input ?? throw new ArgumentNullException(nameof(input));
	private readonly Func<CollectionType, Boolean> hasItems = hasItems;
	private readonly CollectionType main = new();
	private readonly List<ItemType> output = new(input.Count);
	private readonly Action<ItemType, CollectionType> produce = produce ?? throw new ArgumentNullException(nameof(produce));
	private readonly Int32 producersCount = producersCount;
	private readonly TimeSpan consumerDelay = consumerDelay;
	private volatile Boolean isTestInProgres;

	#endregion

	#region Private methods

	/// <summary>
	/// Asynchronously produces items from the <see cref="input" /> collection to the <see cref="main" /> collection.
	/// </summary>
	/// <param name="from">The begin of the range.</param>
	/// <param name="to">The end of the range.</param>
	/// <returns>A <see cref="Task" /> object that represents the asynchronous operation.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private async Task ProduceItemsAsync(Int32 from, Int32 to)
	{
		for (var index = from; index < to; index++)
		{
			// pass execution to other thread
			await Task.Yield();

			// get item
			var item = input[index];

			// produce item
			produce(item, main);
		}
	}

	/// <summary>
	/// Initiates an asynchronous operation to move items from the <see cref="main" /> collection to the <see cref="output" /> collection.
	/// </summary>
	/// <returns>A <see cref="Task" /> object that represents the asynchronous operation.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private async Task ConsumeAsync()
	{
		// it may happen that consumer will take items fastre then producers
		// this is why we need to keep going till test is in progress
		while (isTestInProgres || hasItems(main))
		{
			// pass execution to other thread
			await Task.Delay(consumerDelay);

			// consume items
			consume(main, output);
		}
	}

	#endregion

	#region Methods

	/// <summary>
	/// Executes the collection load test asynchronously.
	/// </summary>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the 
	/// <see cref="CollectionTestResult"/> which includes details about the test execution.
	/// </returns>
	/// <remarks>
	/// This method initializes and starts multiple producer tasks to populate the collection and a consumer task to process the collection.
	/// It measures the elapsed time for the entire operation and returns the results.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public async Task<CollectionTestResult> RunAsync()
	{
		var producers = new Task[producersCount];

		isTestInProgres = true;

		// start stopwatch
		var stopWatch = Stopwatch.StartNew();

		// initiate consume operation
		var consumer = ConsumeAsync();

		// start producers
		var increment = Math.DivRem(input.Count, producersCount, out var rest);

		for (var writeTaskIndex = 0; writeTaskIndex < producersCount; writeTaskIndex++)
		{
			// calc from position
			var from = writeTaskIndex * increment;

			// calc to position
			var to = (writeTaskIndex * increment) + increment + (writeTaskIndex == producersCount - 1 ? rest : 0);

			// initiate produce operation
			producers[writeTaskIndex] = ProduceItemsAsync(from, to);
		}

		// await producers to complete
		await Task.WhenAll(producers);

		// set writers flag
		isTestInProgres = false;

		// await consumer to complete
		await consumer;

		// stop stopwatch
		stopWatch.Stop();

		// compose and return test result
		return new CollectionTestResult
		{
			CollectionType = typeof(CollectionType),
			Description = description,
			ElapsedTime = stopWatch.Elapsed,
			InputCount = input.Count,
			OutputCount = output.Count
		};
	}

	#endregion
}
