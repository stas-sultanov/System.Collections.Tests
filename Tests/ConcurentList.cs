// Created by Stas Sultanov.
// Copyright © Stas Sultanov.

namespace Tests;

using System;
using System.Threading;

public sealed class ConcurrentList<T>
{
	#region Nested Types

	private sealed class Segment<ItemType>(Int32 capacity)
	{
		#region Fields

		public Int32 count = 0;

		public readonly ItemType[] items = new ItemType[capacity*2];

		public Segment<ItemType> nextSegment = null;

		#endregion

		#region Methods

		public Boolean TryAdd(ItemType item)
		{
			var itemsLocal = items;

			// loop in case of contention
			while (true)
			{
				// read current count
				var countLocal = Volatile.Read(ref count);

				if (countLocal >= items.Length)
				{
					return false;
				}

				// after CompareExchange, other threads trying to return will end up spinning until subsequent Write.
				if (Interlocked.CompareExchange(ref count, countLocal + 1, countLocal) == countLocal)
				{
					itemsLocal[countLocal] = item;

					Volatile.Write(ref count, countLocal + 1);

					return true;
				}
			}
		}

		#endregion
	}

	#endregion

	#region Fields

	private readonly Int32 segmentCapaciy;
	private Segment<T> head;
	private Segment<T> tail;
	private readonly Object syncRoot;

	#endregion

	public ConcurrentList() : this(10)
	{
	}

	public ConcurrentList(Byte capacityPower)
	{
		segmentCapaciy = capacityPower < 17 ? 1 << capacityPower : throw new ArgumentNullException(nameof(capacityPower));

		head = new Segment<T>(segmentCapaciy);

		syncRoot = new Object();

		tail = head;
	}

	public Boolean IsEmpty { get => head.count == 0; }

	public void Add(T item)
	{
		while (true)
		{
			var tailLocal = tail;

			// try to append to the existing tail
			if (tailLocal.TryAdd(item))
			{
				return;
			}

			// If we were unsuccessful, take the lock so that we can compare and manipulate
			// the tail.  Assuming another enqueuer hasn't already added a new segment,
			// do so, then loop around to try enqueueing again.
			lock (syncRoot)
			{
				if (tailLocal == tail)
				{
					tail.nextSegment = new Segment<T>(segmentCapaciy);

					tail = tail.nextSegment;
				}
			}
		}
	}

	/// <summary>
	/// By design this method must be run by a single thread.
	/// </summary>
	/// <returns></returns>
	public List<T> EvictAll()
	{
		if (IsEmpty)
		{
			return [];
		}

		var newHead = new Segment<T>(segmentCapaciy);

		// after this operation all producers will start to work with new segment
		_ = Interlocked.Exchange(ref tail, newHead);

		// get current head.
		var evictedHead = head;

		// replace head.
		// no need for thread safity here as by design this method must be run by a single thread
		head = tail;

		// count items
		var resultCount = 0;

		{
			var current = evictedHead;

			do
			{
				resultCount += current.count;

				current = current.nextSegment;
			}
			while (current != null);
		}

		var result = new List<T>(resultCount*2);

		// copy items to result
		{
			var current = evictedHead;

			var copyIndex = 0;

			do
			{
				result.AddRange(current.items);

				// Array.Copy(current.items, 0, result, copyIndex, current.count);

				copyIndex += current.count;

				current = current.nextSegment;
			}
			while (current != null);
		}

		return result;
	}
}
