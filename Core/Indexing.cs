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
using System.Text;
using System.Collections.Generic;

namespace Saraswati.Core
{
    // The pasteboard is a structure which indexes a document for
    // display and navigation. It allows navigation of the document by
    // lines or by word-fragment indices, and provides a means of
    // converting between the two.
    //
    // It is populated via the usual IDocumentConsumer interface.
    class Pasteboard : IDocumentConsumer
    {
	struct Line
	{
	    public int Word;
	    public int Fragment;
	}

	List<Fragment> fragments = new List<Fragment>();
	List<Line> lines = new List<Line>();

	int curWord = 0;
	int curLine = 0;

	public Pasteboard() { }

	public int LineCount
	{
	    get { return lines.Count; }
	}

	public int WordCount
	{
	    get { return curWord; }
	}

	public int FragmentCount
	{
	    get { return fragments.Count; }
	}

	public void Clear()
	{
	    curWord = 0;
	    curLine = 0;
	    fragments.Clear();
	    lines.Clear();
	}

	public void PushBlock(Block blk) { }

	public void PushFragment(Fragment frg)
	{
	    if (frg.Attr == Fragment.Attributes.NewLine)
		curLine++;

	    if ((frg.Text != null) &&
		((frg.Attr & Fragment.Attributes.Formatter) == 0))
	    {
		while (lines.Count <= curLine)
		    lines.Add(new Line() {
			Word = curWord,
			Fragment = fragments.Count
		    });

		fragments.Add(frg);
		curWord += frg.WordCount;
	    }
	}

	public void Close() { }

	public Fragment GetFragment(int i)
	{
	    return fragments[i];
	}

	public int LineFirstFragment(int line)
	{
	    return lines[line].Fragment;
	}

	public int LineNumFragments(int line)
	{
	    int next = fragments.Count;

	    if (line + 1 < lines.Count)
		next = lines[line + 1].Fragment;

	    return next - lines[line].Fragment;
	}

	public int LineFirstWord(int line)
	{
	    return lines[line].Word;
	}

	public int LineNumWords(int line)
	{
	    int next = curWord;

	    if (line + 1 < lines.Count)
		next = lines[line + 1].Word;

	    return next - lines[line].Word;
	}

	public int WordToLine(int w)
	{
	    int low = 0;
	    int high = lines.Count - 1;

	    while (low <= high)
	    {
		int mid = (low + high) / 2;

		if (lines[mid].Word > w)
		    high = mid - 1;
		else if ((mid + 1 < lines.Count) &&
			 (lines[mid + 1].Word <= w))
		    low = mid + 1;
		else
		    return mid;
	    }

	    return 0;
	}
    }

    // The search index indexes a document for fast text searching
    // and/or finding parts referenced via a # fragment.
    //
    // It references document locations by word/fragment index.
    class SearchIndex : IDocumentConsumer
    {
	StringBuilder text = new StringBuilder();
	List<int> wordOffsets = new List<int>();
	string searchText = null;

	public SearchIndex() { }

	public int WordCount
	{
	    get { return wordOffsets.Count; }
	}

	public void Clear()
	{
	    text.Clear();
	    wordOffsets.Clear();
	}

	public void PushBlock(Block blk) { }

	public void PushFragment(Fragment frg)
	{
	    if ((frg.Text != null) &&
		((frg.Attr & Fragment.Attributes.Formatter) == 0))
	    {
		searchText = null;

		if ((text.Length > 0) &&
		    !Char.IsWhiteSpace(text[text.Length - 1]))
		    text.Append(' ');

		foreach (char c in frg.Text)
		{
		    bool wsTerm = (text.Length <= 0) ||
			Char.IsWhiteSpace(text[text.Length - 1]);

		    if (Char.IsWhiteSpace(c))
		    {
			if (!wsTerm)
			    text.Append(c);
		    }
		    else
		    {
			if (wsTerm)
			    wordOffsets.Add(text.Length);

			text.Append(c);
		    }
		}
	    }
	}

	public void Close() { }

	public int SearchForwards(string query, int start = -1)
	{
	    query = normalizeQuery(query);

	    if (searchText == null)
		searchText = text.ToString();

	    if (start >= 0)
		start = wordOffsets[start];
	    else
		start = 0;

	    int idx = searchText.IndexOf(query, start,
		StringComparison.CurrentCultureIgnoreCase);

	    if (idx < 0)
		return -1;

	    return charToWord(idx);
	}

	public int SearchBackwards(string query, int end = -1)
	{
	    query = normalizeQuery(query);

	    if (searchText == null)
		searchText = text.ToString();

	    if (end >= 0)
		end = wordOffsets[end];
	    else
		end = searchText.Length - 1;

	    int idx = searchText.LastIndexOf(query, end,
		StringComparison.CurrentCultureIgnoreCase);

	    if (idx < 0)
		return -1;

	    return charToWord(idx);
	}

	static string normalizeQuery(string q)
	{
	    StringBuilder res = new StringBuilder();
	    bool wantSpace = false;

	    foreach (char c in q)
	    {
		if (Char.IsWhiteSpace(c))
		    wantSpace = true;
		else
		{
		    if ((res.Length > 0) && wantSpace)
			res.Append(' ');

		    res.Append(c);
		    wantSpace = false;
		}
	    }

	    return res.ToString();
	}

	int charToWord(int pos)
	{
	    int low = 0;
	    int high = wordOffsets.Count - 1;

	    while (low <= high)
	    {
		int mid = (low + high) / 2;

		if (wordOffsets[mid] > pos)
		    high = mid - 1;
		else if ((mid + 1 < wordOffsets.Count) &&
			 (wordOffsets[mid + 1] <= pos))
		    low = mid + 1;
		else
		    return mid;
	    }

	    return 0;
	}
    }

    // This index keeps track of the word positions of each anchor
    // id/name.
    class AnchorIndex : IDocumentConsumer
    {
	Dictionary<string, int> anchors = new Dictionary<string, int>();
	int wordOffset = 0;

	public AnchorIndex() { }

	public void Clear()
	{
	    anchors.Clear();
	    wordOffset = 0;
	}

	public void PushBlock(Block blk) { }

	public void PushFragment(Fragment frg)
	{
	    if (frg.Attr == Fragment.Attributes.AnchorName)
		anchors[frg.Text] = wordOffset;

	    wordOffset += frg.WordCount;
	}

	public void Close() { }

	public int FindAnchor(string name)
	{
	    int res;

	    if (anchors.TryGetValue(name, out res))
		return res;

	    return -1;
	}
    }

    // The section index maps word indices in a full document to table
    // of contents labels.
    class SectionIndex
    {
	struct Item
	{
	    public readonly string	Name;
	    public readonly int		Word;

	    public Item(string n, int w)
	    {
		Name = n;
		Word = w;
	    }
	}

	List<Item> items = new List<Item>();

	public SectionIndex(AnchorIndex anchor, TableOfContents toc)
	{
	    for (int i = 0; i < toc.Count; i++)
	    {
		int word = anchor.FindAnchor(toc.GetLinkref(i));

		if (word >= 0)
		    items.Add(new Item(toc.GetName(i), word));
	    }

	    items.Sort(cmpWord);
	}

	static int cmpWord(Item a, Item b)
	{
	    if (a.Word < b.Word)
		return -1;

	    if (a.Word > b.Word)
		return 1;

	    return 0;
	}

	public string WordToName(int pos)
	{
	    int low = 0;
	    int high = items.Count - 1;

	    while (low <= high)
	    {
		int mid = (low + high) / 2;

		if (items[mid].Word > pos)
		    high = mid - 1;
		else if ((mid + 1 < items.Count) &&
			 items[mid + 1].Word <= pos)
		    low = mid + 1;
		else
		    return items[mid].Name;
	    }

	    return null;
	}
    }
}
