namespace System.Collections.Tests;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

/// <summary>
/// Represents a load test for collections, allowing concurrent writing and moving of items between collections.
/// </summary>
/// <typeparam name="InputItemType">The type of the input items.</typeparam>
/// <typeparam name="MainItemType">The type of the main collection items.</typeparam>
/// <typeparam name="OutputItemType">The type of the output collection items.</typeparam>
/// <typeparam name="MainCollectionType">The type of the main collection.</typeparam>
/// <typeparam name="OutputCollectionType">The type of the output collection.</typeparam>
/// <param name="description">The description of the test.</param>
/// <param name="input">The input collection.</param>
/// <param name="producersCount">The count of concurrent writers.</param>
/// <param name="produce">A delegate to the method that produces items by taking from <paramref name="input"/> collection and putting into the main collection.</param>
/// <param name="consume">The delegate to move items from the main collection to the output collection.</param>
/// <exception cref="ArgumentNullException">
/// Thrown if <paramref name="description"/>, <paramref name="input"/>, <paramref name="produce"/>, or <paramref name="consume"/> is null.
/// </exception>
public sealed class CollectionLoadTest<InputItemType, MainItemType, OutputItemType, MainCollectionType, OutputCollectionType>
(
	String description,
	Int32 producersCount,
	IReadOnlyList<InputItemType> input,
	Produce<InputItemType, MainItemType, MainCollectionType> produce,
	Consume<MainItemType, MainCollectionType, OutputItemType, OutputCollectionType> consume
)
	where MainCollectionType : IEnumerable<MainItemType>, new()
	where OutputCollectionType : IEnumerable<OutputItemType>, new()
{
	#region Fields

	/// <summary>
	/// The description of the test.
	/// </summary>
	private readonly String description = description ?? throw new ArgumentNullException(nameof(description));

	/// <summary>
	/// The input collection.
	/// </summary>
	private readonly IReadOnlyList<InputItemType> input = input ?? throw new ArgumentNullException(nameof(input));

	private readonly Consume<MainItemType, MainCollectionType, OutputItemType, OutputCollectionType> consume = consume ?? throw new ArgumentNullException(nameof(consume));

	private readonly Produce<InputItemType, MainItemType, MainCollectionType> produce = produce ?? throw new ArgumentNullException(nameof(produce));

	/// <summary>
	/// The count of concurrent producers.
	/// </summary>
	private readonly Int32 producersCount = producersCount;

	private MainCollectionType main = new();

	private OutputCollectionType output = new();

	private volatile Boolean writersAreActive;

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

			// put item
			produce(item, ref main);
		}
	}

	/// <summary>
	/// Initiates an asynchronous operation to move items from the <see cref="main" /> collection to the <see cref="output" /> collection.
	/// </summary>
	/// <returns>A <see cref="Task" /> object that represents the asynchronous operation.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private async Task ConsumeAsync()
	{
		while (writersAreActive || main.Any())
		{
			// pass execution to other thread
			await Task.Yield();

			// move items
			consume(ref main, ref output);
		}
	}

	#endregion

	#region Methods

	/// <summary>
	/// Executes the collection load test asynchronously.
	/// </summary>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the 
	/// <see cref="CollectionLoadTestResult"/> which includes details about the test execution.
	/// </returns>
	/// <remarks>
	/// This method initializes and starts multiple producer tasks to populate the collection and a consumer task to process the collection.
	/// It measures the elapsed time for the entire operation and returns the results.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public async Task<CollectionLoadTestResult> RunAsync()
	{
		var producers = new Task[producersCount];

		// start stopwatch
		var stopWatch = Stopwatch.StartNew();

		writersAreActive = true;

		// initiate consume operation
		var consumer = ConsumeAsync();

		// start producers
		var increment = Math.DivRem(input.Count, producersCount, out var rest);

		for (var writeTaskIndex = 0; writeTaskIndex < producersCount; writeTaskIndex++)
		{
			// calc from position
			var from = writeTaskIndex * increment;

			// calc to position
			var to = (writeTaskIndex * increment) + increment + (writeTaskIndex == (producersCount - 1) ? rest : 0);

			// initiate produce operation
			producers[writeTaskIndex] = ProduceItemsAsync(from, to);
		}

		// await producers to complete
		await Task.WhenAll(producers);

		// set writers flag
		writersAreActive = false;

		// await consumer to complete
		await consumer;

		// stop stopwatch
		stopWatch.Stop();

		// compose and return test result
		return new CollectionLoadTestResult
		{
			CollectionType = typeof(MainCollectionType),
			Description = description,
			ElapsedTime = stopWatch.Elapsed,
			InputCount = input.Count,
			MainCount = main.Count(),
			OutputCount = output.Count()
		};
	}

	#endregion
}

/// <summary>
/// Represents the produce operation.
/// </summary>
/// <remarks>
/// Produce happens by taking <paramref name="item"/> from the input collection and putting into the main collection.
/// </remarks>
/// <param name="item">Item from the input collection.</param>
/// <param name="main">The reference to the main collection.</param>
/// <typeparam name="InputItemType">The type of the input items.</typeparam>
/// <typeparam name="MainItemType">The type of the main collection items.</typeparam>
/// <typeparam name="MainCollectionType">The type of the main collection.</typeparam>
public delegate void Produce<InputItemType, MainItemType, MainCollectionType>(InputItemType item, ref MainCollectionType main)
	where MainCollectionType : IEnumerable<MainItemType>;

/// <summary>
/// Represents the consume operation.
/// </summary>
/// <remarks>
/// Produce happens by taking items from the main collection and putting into the output collection.
/// </remarks>
/// <param name="main">The main collection to be consumed.</param>
/// <param name="output">The output collection to put the results.</param>
public delegate void Consume<MainItemType, MainCollectionType, OutputItemType, OutputCollectionType>(ref MainCollectionType main, ref OutputCollectionType output)
	where MainCollectionType : IEnumerable<MainItemType>, new()
	where OutputCollectionType : IEnumerable<OutputItemType>, new();