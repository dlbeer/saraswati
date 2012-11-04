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
using System.IO;
using System.Xml;
using System.Text;
using System.Collections.Generic;

namespace Saraswati.Core
{
    // This represents a document overview with a named section
    // hierarchy and a title. Each entry in the table of contents is
    // referenced by an int, and iterator methods are provided to
    // navigate the tree.
    //
    // Link references are relative to the table of contents' path.
    class TableOfContents
    {
	class Entry
	{
	    public int		Parent;
	    public string	Name;
	    public string	Linkref;
	};

	string title;
	List<Entry> entries = new List<Entry>();

	public TableOfContents() { }

	public string Title
	{
	    get { return title; }
	    set { title = value; }
	}

	public int Count
	{
	    get { return entries.Count; }
	}

	public void Clear()
	{
	    entries.Clear();
	    title = null;
	}

	public int AddEntry(int parent)
	{
	    int r = entries.Count;

	    entries.Add(new Entry() { Parent = parent });
	    return r;
	}

	public string GetName(int ent)
	{
	    return entries[ent].Name;
	}

	public string GetLinkref(int ent)
	{
	    return entries[ent].Linkref;
	}

	public void SetName(int ent, string name)
	{
	    entries[ent].Name = name;
	}

	public void SetLinkref(int ent, string r)
	{
	    entries[ent].Linkref = r;
	}

	public int FirstRoot()
	{
	    if (entries.Count > 0)
		return 0;

	    return -1;
	}

	public int LastRoot()
	{
	    int ent = entries.Count - 1;

	    while ((ent >= 0) && (entries[ent].Parent >= 0))
		ent--;

	    return ent;
	}

	public int NextSibling(int ent)
	{
	    if (ent < 0)
		return -1;

	    int p = entries[ent].Parent;

	    ent++;
	    while ((ent < entries.Count) && (entries[ent].Parent != p))
		ent++;

	    if (ent >= entries.Count)
		return -1;

	    return ent;
	}

	public int PrevSibling(int ent)
	{
	    if (ent < 0)
		return -1;

	    int p = entries[ent].Parent;

	    ent--;
	    while ((ent < entries.Count) && (entries[ent].Parent != p))
		ent--;

	    if (ent >= entries.Count)
		return -1;

	    return ent;
	}

	public int Parent(int ent)
	{
	    if (ent < 0)
		return -1;

	    return entries[ent].Parent;
	}

	public int FirstChild(int ent)
	{
	    if (ent < 0)
		return -1;

	    if ((ent + 1 < entries.Count) &&
		(entries[ent + 1].Parent == ent))
		return ent + 1;

	    return -1;
	}

	public int LastChild(int ent)
	{
	    if (ent < 0)
		return -1;

	    int c = FirstChild(ent);

	    if (c < 0)
		return -1;

	    for (;;)
	    {
		int next = NextSibling(c);

		if (next < 0)
		    break;

		c = next;
	    }

	    return c;
	}
    }

    // Parser for NCX files. A utility method is provided which takes a
    // stream and returns a parsed table of contents object.
    class NCXParser
    {
	TableOfContents toc;
	StringBuilder sb = new StringBuilder();
	string relPath;

	int depDocTitle = 0;
	int curEnt = -1;

	public NCXParser(TableOfContents t, string rel)
	{
	    toc = t;
	    relPath = rel;
	}

	void Begin(string name, XmlReader r)
	{
	    if (name.Equals("text"))
		sb.Clear();
	    else if (name.Equals("doctitle"))
		depDocTitle++;
	    else if (name.Equals("navpoint"))
		curEnt = toc.AddEntry(curEnt);
	    else if (name.Equals("content"))
	    {
		string src = r.GetAttribute("src");

		if ((src != null) && (curEnt >= 0))
		    toc.SetLinkref(curEnt, EPath.RelPath(relPath, src));
	    }
	}

	void End(string name)
	{
	    if (name.Equals("doctitle"))
		depDocTitle--;
	    else if (name.Equals("navpoint"))
		curEnt = toc.Parent(curEnt);
	    else if (name.Equals("text"))
	    {
		if (depDocTitle > 0)
		    toc.Title = sb.ToString();
		else if (curEnt >= 0)
		    toc.SetName(curEnt, sb.ToString());
	    }
	}

	void Text(string text)
	{
	    sb.Append(text);
	}

	public void Parse(Stream s)
	{
	    var r = new XmlTextReader(s);

	    r.XmlResolver = null;

	    while (r.Read())
		switch (r.NodeType)
		{
		case XmlNodeType.Element:
		    Begin(r.Name.ToLower(), r);
		    break;

		case XmlNodeType.Text:
		    Text(r.Value);
		    break;

		case XmlNodeType.EndElement:
		    End(r.Name.ToLower());
		    break;
		}
	}

	public static TableOfContents ParseStream(Stream s, string rel)
	{
	    TableOfContents toc = new TableOfContents();
	    var parser = new NCXParser(toc, rel);

	    parser.Parse(s);
	    return toc;
	}
    }

    // Pretty-printer for table of contents. A depth limit can be given,
    // so that a depth limit of 0 gives a flat overview, whereas no
    // limit (-1) means that we print every detailed entry in the table
    // of contents.
    class TocPrinter
    {
	public static void PrintToc(TableOfContents toc,
				    IDocumentConsumer doc,
				    int depthLimit = -1)
	{
	    doc.PushBlock(new Block());
	    doc.PushFragment(new Fragment() {
		Attr = Fragment.Attributes.Heading,
		Text = toc.Title
	    });

	    for (int i = toc.FirstRoot(); i >= 0; i = toc.NextSibling(i))
		PrintEntry(toc, doc, i, 0, depthLimit);

	    doc.Close();
	}

	static void PrintEntry(TableOfContents toc,
			       IDocumentConsumer doc,
			       int ent, int depth, int depthLimit)
	{
	    if ((depthLimit >= 0) && (depth > depthLimit))
		return;

	    doc.PushBlock(new Block() {
		Indent = depth
	    });

	    doc.PushFragment(new Fragment() {
		Attr = Fragment.Attributes.Heading,
		Text = toc.GetName(ent),
		Linkref = toc.GetLinkref(ent)
	    });

	    for (int i = toc.FirstChild(ent); i >= 0; i = toc.NextSibling(i))
		PrintEntry(toc, doc, i, depth + 1, depthLimit);
	}
    }
}
