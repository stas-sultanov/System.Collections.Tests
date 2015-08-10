using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace System.Collections.Tests
{
	/// <summary>
	/// Provides a load test of the collection with many writers one reader.
	/// </summary>
	/// <typeparam name="TInputItem">The type of elements in the input collection.</typeparam>
	/// <typeparam name="TMainItem">The type of elements in the main collection.</typeparam>
	/// <typeparam name="TOutputItem">The type of elements in the output collection.</typeparam>
	/// <typeparam name="TMainCollection">The type of the main collection.</typeparam>
	/// <typeparam name="TOutputCollection">The type of the output collection.</typeparam>
	public sealed class CollectionLoadTest<TInputItem, TMainItem, TOutputItem, TMainCollection, TOutputCollection>
		where TMainCollection : IEnumerable<TMainItem>, new()
		where TOutputCollection : IEnumerable<TOutputItem>, new()
	{
		#region Nested Types

		public delegate void Put(TInputItem inputItem, ref TMainCollection main);

		public delegate void Move(ref TMainCollection main, ref TOutputCollection output);

		#endregion

		#region Fields

		/// <summary>
		/// The description of the test.
		/// </summary>
		private readonly String description;

		/// <summary>
		/// The input collection.
		/// </summary>
		private readonly IReadOnlyList<TInputItem> input;

		private readonly Move move;

		private readonly Put put;

		/// <summary>
		/// The count of concurrent writers.
		/// </summary>
		private readonly Int32 writersCount;

		private TMainCollection main;

		private TOutputCollection output;

		private volatile Boolean writersAreActive;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="CollectionLoadTest{TInputItem, TMainItem, TOutputItem, TMainCollection, TOutputCollection}" /> class.
		/// </summary>
		public CollectionLoadTest(String description, IReadOnlyList<TInputItem> input, Int32 writersCount, Put put, Move move)
		{
			// Check arguments
			if (description == null)
			{
				throw new ArgumentNullException(nameof(description));
			}

			if (input == null)
			{
				throw new ArgumentNullException(nameof(input));
			}

			if (put == null)
			{
				throw new ArgumentNullException(nameof(put));
			}

			if (move == null)
			{
				throw new ArgumentNullException(nameof(move));
			}

			// Set fields
			this.description = description;

			this.input = input;

			output = new TOutputCollection();

			this.writersCount = writersCount;

			main = new TMainCollection();

			this.put = put;

			this.move = move;
		}

		#endregion

		#region Private methods

		/// <summary>
		/// Initiates an asynchronous operation to put specified range of items from the <see cref="input" /> collection to the <see cref="main" /> collection.
		/// </summary>
		/// <param name="from">The begin of the range.</param>
		/// <param name="to">The end of the range.</param>
		/// <returns>A <see cref="Task" /> object that represents the asynchronous operation.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private async Task PunItemsAsync(Int32 from, Int32 to)
		{
			for (var index = from; index < to; index++)
			{
				// pass execution to other thread
				await Task.Yield();

				// get item
				var item = input[index];

				// put item
				put(item, ref main);
			}
		}

		/// <summary>
		/// Initiates an asynchronous operation to move items from the <see cref="main" /> collection to the <see cref="output" /> collection.
		/// </summary>
		/// <returns>A <see cref="Task" /> object that represents the asynchronous operation.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private async Task MoveAsync()
		{
			while (writersAreActive || main.Count() != 0)
			{
				// pass execution to other thread
				await Task.Yield();

				// move items
				move(ref main, ref output);
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Initiates an asynchronous operation to execute test.
		/// </summary>
		/// <returns>A <see cref="Task" /> object that represents the asynchronous operation.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public async Task<CollectionTestResult> RunAsync()
		{
			// start stopwatch
			var stopWatch = Stopwatch.StartNew();

			var writeTasksCollection = new Task[writersCount];

			writersAreActive = true;

			// start reader
			var readTask = MoveAsync();

			// start writers
			Int32 rest;

			var increment = Math.DivRem(input.Count, writersCount, out rest);

			for (var writeTaskIndex = 0; writeTaskIndex < writersCount; writeTaskIndex++)
			{
				// calc from position
				var from = writeTaskIndex * increment;

				// calc to position
				var to = writeTaskIndex * increment + increment + (writeTaskIndex == (writersCount - 1) ? rest : 0);

				// initiate write task
				writeTasksCollection[writeTaskIndex] = PunItemsAsync(from, to);
			}

			// await write tasks to complete
			await Task.WhenAll(writeTasksCollection);

			// set writers flag
			writersAreActive = false;

			// await read task
			await readTask;

			// stop stopwatch
			stopWatch.Stop();

			// compose and return test result
			return new CollectionTestResult
			{
				CollectionType = typeof(TMainCollection),
				Description = description,
				ElapsedTime = stopWatch.Elapsed,
				InputCount = input.Count,
				MainCount = main.Count(),
				OutputCount = output.Count(),
				WritersCount = writersCount
			};
		}

		#endregion
	}
}