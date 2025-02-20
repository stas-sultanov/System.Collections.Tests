// Created by Stas Sultanov.
// Copyright © Stas Sultanov.

namespace Tests;

using System;
using System.Threading;

public sealed class ConcurrentList<T>
{
	private Node head;
	private Node tail;
	private Int32 count;

	private class Node
	{
		public T Item;
		public Node Next;
	}

	public ConcurrentList()
	{
		head = null;
		tail = null;
		count = 0;
	}

	public void Add(T item)
	{
		var newNode = new Node { Item = item, Next = null };

		while (true)
		{
			var oldTail = tail;
			_ = Interlocked.CompareExchange(ref tail, newNode, oldTail);

			if (oldTail == null)
			{
				head = newNode;
				break;
			}
			else if (Interlocked.CompareExchange(ref oldTail.Next, newNode, null) == null)
			{
				break;
			}
		}

		_ = Interlocked.Increment(ref count);
	}

	public T[] EvictAll()
	{
		Node oldHead;
		Int32 oldCount;

		do
		{
			oldHead = Interlocked.Exchange(ref head, null);
			oldCount = Interlocked.Exchange(ref count, 0);
		} while (Interlocked.CompareExchange(ref tail, null, oldHead) != oldHead);

		var result = new T[oldCount];
		var current = oldHead;
		var index = 0;
		while (current != null)
		{
			result[index++] = current.Item;
			current = current.Next;
		}

		return result;
	}
}
