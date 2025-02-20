// Created by Stas Sultanov.
// Copyright © Stas Sultanov.

namespace Tests;

using System;
using System.Collections.Concurrent;

public sealed class SupperQueue<Type>
{
	private ConcurrentQueue<Type> innerQueue = new ();

	public void Add(Type item)
	{
		innerQueue.Enqueue(item);
	}

	public Boolean IsEmpty
	{
		get => innerQueue.IsEmpty;
	}

	public Type[] Evict()
	{
		var queue = Interlocked.Exchange(ref innerQueue, new ConcurrentQueue<Type>());

		var result = queue.ToArray();

		return result;
	}
}
