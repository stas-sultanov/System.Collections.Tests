using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace System.Collections.Tests
{
	public static class DictionaryPerformanceTestHelper
	{
		#region Methods


		public static Task<CollectionTestResult> CreateDictionaryTest<TKey, TValue>(IReadOnlyList<KeyValuePair<TKey, TValue>> inputItems, Int32 writersCount)
		{
			var test = new CollectionLoadTest<KeyValuePair<TKey, TValue>, KeyValuePair<TKey, ConcurrentQueue<TValue>>, KeyValuePair<TKey, TValue>, Dictionary<TKey, ConcurrentQueue<TValue>>, List<KeyValuePair<TKey, TValue>>>
				(
					$"Dictionary | lock, Clear | AddRange",
					inputItems,
					writersCount,
					(KeyValuePair<TKey, TValue> inputItem, ref Dictionary<TKey, ConcurrentQueue<TValue>> interim) =>
					{
						ConcurrentQueue<TValue> coll;

						lock (interim)
						{
							// ReSharper disable once InvertIf
							if (!interim.TryGetValue(inputItem.Key, out coll))
							{
								coll = new ConcurrentQueue<TValue>();

								interim.Add(inputItem.Key, coll);
							}
						}

						coll.Enqueue(inputItem.Value);
					},
					(ref Dictionary<TKey, ConcurrentQueue<TValue>> interim, ref List<KeyValuePair<TKey, TValue>> output) =>
					{
						lock (interim)
						{
							output.AddRange(from pair in interim from subPair in pair.Value select new KeyValuePair<TKey, TValue>(pair.Key, subPair));

							interim.Clear();
						}
					}
				);

			return test.RunAsync();
		}

		public static Task<CollectionTestResult> CreateConcurrentDictionaryTest<TKey, TValue>(IReadOnlyList<KeyValuePair<TKey, TValue>> inputItems, Int32 writersCount)
		{
			var test = new CollectionLoadTest<KeyValuePair<TKey, TValue>, KeyValuePair<TKey, ConcurrentQueue<TValue>>, KeyValuePair<TKey, TValue>, ConcurrentDictionary<TKey, ConcurrentQueue<TValue>>, List<KeyValuePair<TKey, TValue>>>
			(
				$"ConcurrentDictionary | lock, Clear | AddRange",
				inputItems,
				writersCount,
					(KeyValuePair<TKey, TValue> inputItem, ref ConcurrentDictionary<TKey, ConcurrentQueue<TValue>> interim) =>
					{
						interim.AddOrUpdate
							(
								inputItem.Key,
								key =>
								{
									var result = new ConcurrentQueue<TValue>();

									result.Enqueue(inputItem.Value);

									return result;
								},
								(key, values) =>
								{
									values.Enqueue(inputItem.Value);

									return values;
								}
							);
					},
					(ref ConcurrentDictionary<TKey, ConcurrentQueue<TValue>> interim, ref List<KeyValuePair<TKey, TValue>> output) =>
					{
						var keys = interim.Keys.ToArray();

						foreach (var key in keys)
						{
							ConcurrentQueue<TValue> col;

							if (!interim.TryRemove(key, out col))
							{
								continue;
							}

							TValue value;

							while (col.TryDequeue(out value))
							{
								output.Add(new KeyValuePair<TKey, TValue>(key, value));
							}
						}
					}
				);

			return test.RunAsync();
		}


		public static async Task<IReadOnlyCollection<CollectionTestResult>> RunAllAsync<TKey, TValue>(IReadOnlyList<KeyValuePair<TKey, TValue>> inputItems, Int32 concurrentWritersCount)
		{
			var results = new CollectionTestResult[2];

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
}