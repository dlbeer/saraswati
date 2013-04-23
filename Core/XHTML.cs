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
using System.Collections.Generic;

namespace Saraswati.Core
{
    // This object takes a reference to a document consumer. It's
    // Parse() method takes a Stream, parses it using an XML SAX parser
    // and emits a flattened document broken into blocks.
    //
    // Lists, paragraphs, blockquotes and <pre> tags are all parsed
    // appropriately, but fragments may not be optimized, and may
    // contain bad whitespace.
    class XHTMLParser
    {
	IDocumentConsumer consumer;
	bool blockChanged = true;

	enum ListState
	{
	    Unordered,
	    Ordered,
	    Definition
	};

	class ListEnv
	{
	    public ListState	State;
	    public int		ItemCount;

	    public ListEnv(ListState st)
	    {
		State = st;
		ItemCount = 1;
	    }
	};

	// Tag counters for context
	int depBody = 0;
	int depStrong = 0;
	int depHeading = 0;
	int depEm = 0;
	int depCode = 0;
	int depPre = 0;
	int depBlockquote = 0;
	int depList = 0;

	Stack<ListEnv> stkList = new Stack<ListEnv>();
	bool bulletStart = false;

	Stack<string> stkHref = new Stack<string>();

	public XHTMLParser(IDocumentConsumer c)
	{
	    consumer = c;
	}

	void Begin(string lwr, XmlReader reader)
	{
	    string name = reader.GetAttribute("id");

	    if (name != null)
		consumer.PushFragment(new Fragment() {
		    Attr = Fragment.Attributes.AnchorName,
		    Text = name
		});

	    if (lwr.Equals("body"))
		depBody++;
	    else if (lwr.Equals("p") || lwr.Equals("div"))
		BlockBoundary();
	    else if ((lwr.Length == 2) && (lwr[0] == 'h') &&
		Char.IsDigit(lwr[1]))
	    {
		BlockBoundary();
		depHeading++;
	    }
	    else if (lwr.Equals("a"))
		stkHref.Push(reader.GetAttribute("href"));
	    else if (lwr.Equals("br"))
		consumer.PushFragment(new Fragment() {
		    Attr = Fragment.Attributes.NewLine });
	    else if (lwr.Equals("blockquote"))
	    {
		BlockBoundary();
		depBlockquote++;
	    }
	    else if (lwr.Equals("em"))
		depEm++;
	    else if (lwr.Equals("strong"))
		depStrong++;
	    else if (lwr.Equals("code"))
		depCode++;
	    else if (lwr.Equals("pre"))
	    {
		BlockBoundary();
		depPre++;
	    }
	    else if (lwr.Equals("ol"))
	    {
		BlockBoundary();
		stkList.Push(new ListEnv(ListState.Ordered));
		depList++;
	    }
	    else if (lwr.Equals("ul"))
	    {
		BlockBoundary();
		stkList.Push(new ListEnv(ListState.Unordered));
		depList++;
	    }
	    else if (lwr.Equals("li"))
	    {
		BlockBoundary();
		bulletStart = true;
	    }
	    else if (lwr.Equals("dl"))
	    {
		BlockBoundary();
		stkList.Push(new ListEnv(ListState.Definition));
		depList++;
	    }
	    else if (lwr.Equals("dt"))
	    {
		BlockBoundary();
	    }
	    else if (lwr.Equals("dd"))
	    {
		BlockBoundary();
		depList++;
		bulletStart = true;
	    }
	}

	void End(string lwr)
	{
	    if (lwr.Equals("body"))
		depBody--;
	    else if (lwr.Equals("a"))
		stkHref.Pop();
	    else if (lwr.Equals("p") || lwr.Equals("div"))
		BlockBoundary();
	    else if ((lwr.Length == 2) && (lwr[0] == 'h') &&
		Char.IsDigit(lwr[1]))
	    {
		BlockBoundary();
		depHeading--;
	    }
	    else if (lwr.Equals("em"))
		depEm--;
	    else if (lwr.Equals("strong"))
		depStrong--;
	    else if (lwr.Equals("code"))
		depCode--;
	    else if (lwr.Equals("pre"))
	    {
		BlockBoundary();
		depPre--;
	    }
	    else if (lwr.Equals("blockquote"))
	    {
		BlockBoundary();
		depBlockquote--;
	    }
	    else if (lwr.Equals("ol"))
	    {
		BlockBoundary();
		stkList.Pop();
		depList--;
	    }
	    else if (lwr.Equals("ul"))
	    {
		BlockBoundary();
		stkList.Pop();
		depList--;
	    }
	    else if (lwr.Equals("li"))
	    {
		BlockBoundary();
		stkList.Peek().ItemCount++;
	    }
	    else if (lwr.Equals("dl"))
	    {
		BlockBoundary();
		stkList.Push(new ListEnv(ListState.Definition));
		depList--;
	    }
	    else if (lwr.Equals("dt"))
	    {
		BlockBoundary();
	    }
	    else if (lwr.Equals("dd"))
	    {
		BlockBoundary();
		depList--;
	    }
	}

	void Text(string text)
	{
	    if (depBody <= 0)
		return;

	    if (blockChanged)
	    {
		EmitBlock();
		blockChanged = false;
	    }

	    EmitFragment(text);
	}

	void BlockBoundary()
	{
	    blockChanged = true;
	}

	void EmitBlock()
	{
	    var blk = new Block();

	    blk.BlockType = depPre > 0 ?
		Block.Type.Preformatted : Block.Type.Paragraph;
	    blk.Indent = depBlockquote + depPre + depList;

	    if (bulletStart)
	    {
		ListEnv env = new ListEnv(ListState.Unordered);

		if (stkList.Count > 0)
		    env = stkList.Peek();

		bulletStart = false;

		switch (env.State)
		{
		case ListState.Unordered:
		    blk.Prefix = "*";
		    break;

		case ListState.Ordered:
		    blk.Prefix = XmlConvert.ToString(env.ItemCount) + ".";
		    break;

		case ListState.Definition:
		    blk.Prefix = "~";
		    break;
		}
	    }

	    consumer.PushBlock(blk);
	}

	void EmitFragment(string text)
	{
	    var frg = new Fragment();

	    frg.Attr = 0;

	    if (depEm > 0)
		frg.Attr |= Fragment.Attributes.Italic;
	    if (depStrong > 0)
		frg.Attr |= Fragment.Attributes.Bold;
	    if (depCode > 0)
		frg.Attr |= Fragment.Attributes.Code;
	    if (depHeading > 0)
		frg.Attr |= Fragment.Attributes.Heading;

	    frg.Text = text;

	    if (stkHref.Count > 0)
		frg.Linkref = stkHref.Peek();

	    consumer.PushFragment(frg);
	}

	public void Parse(Stream s)
	{
	    using (XmlTextReader r = new XmlTextReader(s))
	    {
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

	    consumer.Close();
	}

	public static void ParseStream(Stream s, IDocumentConsumer con)
	{
	    var parser = new XHTMLParser(con);

	    parser.Parse(s);
	}
    }

    // This function scans an XHTML document and produces a table of
    // contents based on identifiable headings.
    class HeadingScan
    {
	public static TableOfContents Scan(Stream s)
	{
	    var result = new TableOfContents();
	    var levelPath = new Stack<int>();
	    int ent = -1;

	    using (XmlTextReader r = new XmlTextReader(s))
	    {
		r.XmlResolver = null;

		while (r.Read())
		    if (r.NodeType == XmlNodeType.Element)
		    {
			if ((r.Name.Length == 2) &&
			    (char.ToLower(r.Name[0]) == 'h') &&
			    char.IsDigit(r.Name[1]))
			{
			    int level = r.Name[1] - '0';
			    string id = r.GetAttribute("id");
			    string text = r.ReadElementContentAsString();

			    if ((id != null) && (text != null))
			    {
				while ((levelPath.Count > 0) &&
				       (levelPath.Peek() >= level))
				{
				    levelPath.Pop();
				    ent = result.Parent(ent);
				}

				levelPath.Push(level);
				ent = result.AddEntry(ent);

				result.SetName(ent, text);
				result.SetLinkref(ent, id);
			    }
			}
			else if (r.Name.ToLower() == "title")
			{
			    result.Title = r.ReadElementContentAsString();
			}
		    }
	    }

	    return result;
	}
    }
}
