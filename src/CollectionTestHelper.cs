using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace System.Collections.Tests
{
	public static class CollectionTestHelper
	{
		#region Methods

		public static CollectionTestHelper<TItem, TInterimCollection, TOutputCollection> Create<TItem, TInterimCollection, TOutputCollection>(String name, IReadOnlyList<TItem> auxCollection, Int32 writersCount, CollectionTestHelper<TItem, TInterimCollection, TOutputCollection>.Add add, CollectionTestHelper<TItem, TInterimCollection, TOutputCollection>.Move move)
			where TInterimCollection : IReadOnlyCollection<TItem>, new()
			where TOutputCollection : ICollection<TItem>, new()
		{
			return new CollectionTestHelper<TItem, TInterimCollection, TOutputCollection>(name, auxCollection, writersCount, add, move);
		}

		#endregion
	}

	public sealed class CollectionTestHelper<TItem, TInterimCollection, TOutputCollection>
		where TInterimCollection : IReadOnlyCollection<TItem>, new()
		where TOutputCollection : ICollection<TItem>, new()
	{
		#region Nested Types

		public delegate void Add(TItem inputItem, ref TInterimCollection source);

		public delegate void Move(ref TInterimCollection source, ref TOutputCollection output);

		#endregion

		#region Fields

		private readonly Add add;

		private readonly Move move;

		private TInterimCollection interim;

		private TOutputCollection output;

		private volatile Boolean writersAreActive;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the class.
		/// </summary>
		public CollectionTestHelper(String description, IReadOnlyList<TItem> input, Int32 writersCount, Add add, Move move)
		{
			if (description == null)
			{
				throw new ArgumentNullException(nameof(description));
			}

			if (description.Length == 0)
			{
				throw new ArgumentException();
			}

			if (input == null)
			{
				throw new ArgumentNullException(nameof(input));
			}

			Description = description;

			Input = input;

			output = new TOutputCollection();

			WritersCount = writersCount;

			interim = new TInterimCollection();

			this.add = add;

			this.move = move;
		}

		#endregion

		#region Properties

		/// <summary>
		/// The description of the test.
		/// </summary>
		public String Description
		{
			get;
		}

		/// <summary>
		/// The total elapsed time.
		/// </summary>
		public TimeSpan ElapsedTime
		{
			get;

			private set;
		}

		/// <summary>
		/// The input collection.
		/// </summary>
		public IReadOnlyList<TItem> Input
		{
			get;
		}

		/// <summary>
		/// The intermediate collection.
		/// </summary>
		public TInterimCollection Interim => interim;

		/// <summary>
		/// The output collection.
		/// </summary>
		public TOutputCollection Output => output;

		/// <summary>
		/// The count of concurrent writers.
		/// </summary>
		public Int32 WritersCount
		{
			get;
		}

		#endregion

		#region Private methods

		/// <summary>
		/// Initiates an asynchronous operation to move specified range of items from the auxCollection source into source one.
		/// </summary>
		/// <param name="from">The begin of the range.</param>
		/// <param name="to">The end of the range.</param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private async Task WriteRangeAsync(Int32 from, Int32 to)
		{
			for (var index = from; index < to; index++)
			{
				// Pass execution to other thread
				await Task.Yield();

				// Get item
				var item = Input[index];

				// Add item
				add(item, ref interim);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private async Task GetAsync()
		{
			while (writersAreActive || interim.Count != 0)
			{
				await Task.Yield();

				move(ref interim, ref output);
			}
		}

		#endregion

		#region Methods

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public async Task<CollectionTestResult> RunAsync()
		{
			// Start stopwatch
			var stopWatch = Stopwatch.StartNew();

			var writeTasksCollection = new Task[WritersCount];

			writersAreActive = true;

			// Start reader
			var readTask = GetAsync();

			// Start writers
			Int32 rest;

			var increment = Math.DivRem(Input.Count, WritersCount, out rest);

			for (var writeTaskIndex = 0; writeTaskIndex < WritersCount; writeTaskIndex++)
			{
				// calc from position
				var from = writeTaskIndex * increment;

				// calc to position
				var to = writeTaskIndex * increment + increment + (writeTaskIndex == (WritersCount - 1) ? rest : 0);

				// Initiate write task
				writeTasksCollection[writeTaskIndex] = WriteRangeAsync(from, to);
			}

			// Await tasks to complete
			await Task.WhenAll(writeTasksCollection);

			writersAreActive = false;

			// Await read task
			await readTask;

			// Stop stopwatch
			stopWatch.Stop();

			// Compose and return test result
			return new CollectionTestResult
			{
				Description = Description,
				ElapsedTime = stopWatch.Elapsed,
				InputCount = Input.Count,
				InterimCount = Interim.Count,
				OutputCount = Output.Count,
				WritersCount = WritersCount
			};
		}

		#endregion
	}
}