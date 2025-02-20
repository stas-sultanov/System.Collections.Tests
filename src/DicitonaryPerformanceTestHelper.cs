namespace System.Collections.Tests;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public static class DictionaryPerformanceTestHelper
{
	#region Methods

	public static Task<CollectionLoadTestResult> CreateDictionaryTest<TKey, TValue>(IReadOnlyList<KeyValuePair<TKey, TValue>> inputItems, Int32 writersCount)
	{
		var test = new CollectionLoadTest<KeyValuePair<TKey, TValue>, KeyValuePair<TKey, ConcurrentQueue<TValue>>, KeyValuePair<TKey, TValue>, Dictionary<TKey, ConcurrentQueue<TValue>>, List<KeyValuePair<TKey, TValue>>>
			(
			$"Dictionary | lock, Clear | AddRange",
			writersCount,
			inputItems,
			// write
			(KeyValuePair<TKey, TValue> inputItem, ref Dictionary<TKey, ConcurrentQueue<TValue>> main) =>
			{

				lock (main)
				{
					// ReSharper disable once InvertIf
					if (!main.TryGetValue(inputItem.Key, out var queue))
					{
						queue = new ConcurrentQueue<TValue>();

						main.Add(inputItem.Key, queue);
					}

					queue.Enqueue(inputItem.Value);
				}
			},
			// read
			(ref Dictionary<TKey, ConcurrentQueue<TValue>> main, ref List<KeyValuePair<TKey, TValue>> output) =>
			{
				lock (main)
				{
					output.AddRange(from pair in main from subPair in pair.Value select new KeyValuePair<TKey, TValue>(pair.Key, subPair));

					main.Clear();
				}
			}
			);

		return test.RunAsync();
	}

	public static Task<CollectionLoadTestResult> CreateConcurrentDictionaryTest<TKey, TValue>(IReadOnlyList<KeyValuePair<TKey, TValue>> inputItems, Int32 writersCount)
	{
		var test = new CollectionLoadTest<KeyValuePair<TKey, TValue>, KeyValuePair<TKey, ConcurrentQueue<TValue>>, KeyValuePair<TKey, TValue>, ConcurrentDictionary<TKey, ConcurrentQueue<TValue>>, List<KeyValuePair<TKey, TValue>>>
			(
			$"ConcurrentDictionary | lock, Clear | AddRange",
			writersCount,
			inputItems,
			// write
			(KeyValuePair<TKey, TValue> inputItem, ref ConcurrentDictionary<TKey, ConcurrentQueue<TValue>> main) =>
			{
				_ = main.AddOrUpdate
					(
						inputItem.Key,
						key =>
						{
							var result = new ConcurrentQueue<TValue>();

							result.Enqueue(inputItem.Value);

							return result;
						},
						(key, queue) =>
						{
							queue.Enqueue(inputItem.Value);

							return queue;
						}
					);
			},
			// read
			(ref ConcurrentDictionary<TKey, ConcurrentQueue<TValue>> main, ref List<KeyValuePair<TKey, TValue>> output) =>
			{
				var keys = main.Keys.ToArray();

				foreach (var key in keys)
				{

					if (!main.TryRemove(key, out var queue))
					{
						continue;
					}

					while (queue.TryDequeue(out var value))
					{
						output.Add(new KeyValuePair<TKey, TValue>(key, value));
					}
				}
			}
			);

		return test.RunAsync();
	}

	public static async Task<CollectionLoadTestResult[]> RunAllAsync<TKey, TValue>(IReadOnlyList<KeyValuePair<TKey, TValue>> inputItems, Int32 concurrentWritersCount)
	{
		var results = new CollectionLoadTestResult[2];

		{
			results[0] = await CreateDictionaryTest(inputItems, concurrentWritersCount);
		}

		{
			results[1] = await CreateConcurrentDictionaryTest(inputItems, concurrentWritersCount);
		}

		return results;
	}

	#endregion
}