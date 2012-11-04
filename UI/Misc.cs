// Saraswati -- command-line EPUB reader
// Copyright (C) 2012 Daniel Beer <dlbeer@gmail.com>
//
// Permission to use, copy, modify, and/or distribute this software for
// any purpose with or without fee is hereby granted, provided that the
// above copyright notice and this permission notice appear in all
// copies.
//
// THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL
// WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE
// AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL
// DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR
// PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER
// TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR
// PERFORMANCE OF THIS SOFTWARE.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Saraswati.UI
{
    // LRU cache structure.
    class LRUSet<ValueType> : IEnumerable<ValueType>
    {
	LinkedList<ValueType> list = new LinkedList<ValueType>();
	Dictionary<ValueType, LinkedListNode<ValueType>> map =
	    new Dictionary<ValueType, LinkedListNode<ValueType>>();
	public readonly int Limit;

	public LRUSet(int l)
	{
	    Limit = l;
	}

	public int Count
	{
	    get { return list.Count; }
	}

	public void Clear()
	{
	    list.Clear();
	    map.Clear();
	}

	public void Remove(ValueType v)
	{
	    LinkedListNode<ValueType> node;

	    if (!map.TryGetValue(v, out node))
		return;

	    list.Remove(node);
	    map.Remove(v);
	}

	public void Add(ValueType v)
	{
	    Remove(v);

	    if (list.Count >= Limit)
	    {
		LinkedListNode<ValueType> oldest = list.First;

		map.Remove(oldest.Value);
		list.Remove(oldest);
	    }

	    list.AddLast(v);
	    map[v] = list.Last;
	}

	public bool Contains(ValueType v)
	{
	    return map.ContainsKey(v);
	}

	public ValueType[] ToArray()
	{
	    ValueType[] result = new ValueType[list.Count];
	    int i = 0;

	    foreach (ValueType v in this)
		result[i++] = v;

	    return result;
	}

	public IEnumerator<ValueType> GetEnumerator()
	{
	    return list.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
	    return ((IEnumerable)list).GetEnumerator();
	}
    }

    // LRU cache structure, with attached data.
    class LRUDictionary<KeyType, ValueType> :
	IEnumerable<LRUDictionary<KeyType, ValueType>.Pair>
	where ValueType : new()
    {
	public struct Pair
	{
	    public readonly KeyType	Key;
	    public readonly ValueType	Value;

	    public Pair(KeyType k, ValueType v)
	    {
		Key = k;
		Value = v;
	    }
	}

	LinkedList<Pair> list = new LinkedList<Pair>();
	Dictionary<KeyType, LinkedListNode<Pair>> map =
	    new Dictionary<KeyType, LinkedListNode<Pair>>();
	public readonly int Limit;

	public LRUDictionary(int l)
	{
	    Limit = l;
	}

	public int Count
	{
	    get { return list.Count; }
	}

	public void Clear()
	{
	    list.Clear();
	    map.Clear();
	}

	public void Remove(KeyType k)
	{
	    LinkedListNode<Pair> node;

	    if (!map.TryGetValue(k, out node))
		return;

	    list.Remove(node);
	    map.Remove(k);
	}

	public bool Contains(KeyType k)
	{
	    return map.ContainsKey(k);
	}

	public bool TryGetValue(KeyType k, out ValueType v)
	{
	    LinkedListNode<Pair> node;

	    if (map.TryGetValue(k, out node))
	    {
		v = node.Value.Value;
		return true;
	    }

	    v = new ValueType();
	    return false;
	}

	public void Put(KeyType k, ValueType v)
	{
	    Remove(k);

	    if (list.Count >= Limit)
	    {
		LinkedListNode<Pair> node = list.First;

		list.Remove(node);
		map.Remove(node.Value.Key);
	    }

	    list.AddLast(new Pair(k, v));
	    map[k] = list.Last;
	}

	public ValueType this[KeyType k]
	{
	    get
	    {
		LinkedListNode<Pair> v;

		if (!map.TryGetValue(k, out v))
		    throw new Exception("Key not found");

		return v.Value.Value;
	    }

	    set { Put(k, value); }
	}

	public IEnumerator<Pair> GetEnumerator()
	{
	    return list.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
	    return ((IEnumerable)list).GetEnumerator();
	}

	public Pair[] ToArray()
	{
	    Pair[] result = new Pair[list.Count];
	    int i = 0;

	    foreach (Pair it in this)
		result[i++] = it;

	    return result;
	}

	public IEnumerator<KeyType> GetKeysEnumerator()
	{
	    foreach (Pair it in this)
		yield return it.Key;
	}

	public KeyType[] ToKeysArray()
	{
	    KeyType[] result = new KeyType[list.Count];
	    int i = 0;

	    foreach (Pair it in this)
		result[i++] = it.Key;

	    return result;
	}
    }

    // LRU queue.
    class LRUDeque<ValueType> : IEnumerable<ValueType> where ValueType : new()
    {
	ValueType[] values;
	int front = 0;
	int len = 0;

	public LRUDeque(int limit)
	{
	    values = new ValueType[limit];
	}

	public int Count
	{
	    get { return len; }
	}

	public void Clear()
	{
	    front = 0;
	    len = 0;
	}

	public bool TryPopFront(out ValueType v)
	{
	    if (len <= 0)
	    {
		v = new ValueType();
		return false;
	    }

	    v = values[front];
	    front = (front + 1) % values.Length;
	    len--;
	    return true;
	}

	public bool TryPopBack(out ValueType v)
	{
	    if (len <= 0)
	    {
		v = new ValueType();
		return false;
	    }

	    v = values[(front + values.Length + len - 1) % values.Length];
	    len--;
	    return true;
	}

	public ValueType PopFront()
	{
	    ValueType v;

	    if (!TryPopFront(out v))
		throw new Exception("Empty queue");

	    return v;
	}

	public ValueType PopBack()
	{
	    ValueType v;

	    if (!TryPopBack(out v))
		throw new Exception("Empty queue");

	    return v;
	}

	public void PushFront(ValueType v)
	{
	    front = (front + values.Length - 1) % values.Length;
	    values[front] = v;

	    if (len < values.Length)
		len++;
	}

	public void PushBack(ValueType v)
	{
	    values[(front + len) % values.Length] = v;

	    if (len < values.Length)
		len++;
	    else
		front = (front + 1) % values.Length;
	}

	public IEnumerator<ValueType> GetEnumerator()
	{
	    for (int i = 0; i < len; i++)
		yield return values[(front + i) % values.Length];
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
	    for (int i = 0; i < len; i++)
		yield return values[(front + i) % values.Length];
	}

	public ValueType[] ToArray()
	{
	    ValueType[] result = new ValueType[len];

	    for (int i = 0; i < len; i++)
		result[i] = values[(i + front) % values.Length];

	    return result;
	}
    }
}
