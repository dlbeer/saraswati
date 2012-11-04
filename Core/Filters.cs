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
    // This document filter captures a document stream and can replay it
    // on demand.
    class DocumentStore : IDocumentConsumer
    {
	struct BlockRecord
	{
	    public readonly int		FragmentIndex;
	    public readonly Block	Content;

	    public BlockRecord(int fi, Block c)
	    {
		FragmentIndex = fi;
		Content = c;
	    }
	}

	List<BlockRecord> blocks = new List<BlockRecord>();
	List<Fragment> fragments = new List<Fragment>();

	public DocumentStore() { }

	public void PushFragment(Fragment frg)
	{
	    fragments.Add(frg);
	}

	public void PushBlock(Block blk)
	{
	    blocks.Add(new BlockRecord(fragments.Count, blk));
	}

	public void Close() { }

	public void Clear()
	{
	    blocks.Clear();
	    fragments.Clear();
	}

	public void Replay(IDocumentConsumer con)
	{
	    int f = 0;

	    for (int b = 0; b < blocks.Count; b++)
	    {
		while (f < blocks[b].FragmentIndex)
		    con.PushFragment(fragments[f++]);

		con.PushBlock(blocks[b].Content);
	    }

	    while (f < fragments.Count)
		con.PushFragment(fragments[f++]);

	    con.Close();
	}
    }

    // This document filter edits all linkrefs to be relative to the
    // given path.
    class ShiftLinks : IDocumentConsumer
    {
	IDocumentConsumer next;
	string basePath;

	public ShiftLinks(string p, IDocumentConsumer con)
	{
	    basePath = p;
	    next = con;
	}

	public void PushBlock(Block blk)
	{
	    next.PushBlock(blk);
	}

	public void PushFragment(Fragment frg)
	{
	    if (frg.Linkref != null)
		frg.Linkref = EPath.RelPath(basePath, frg.Linkref);

	    next.PushFragment(frg);
	}

	public void Close()
	{
	    next.Close();
	}
    }

    // This document filter edits all anchors and assigns them a prefix.
    class NamespaceAnchors : IDocumentConsumer
    {
	IDocumentConsumer next;
	string basePath;

	public NamespaceAnchors(string p, IDocumentConsumer con)
	{
	    basePath = p;
	    next = con;
	}

	public void PushBlock(Block blk)
	{
	    next.PushBlock(blk);
	}

	public void PushFragment(Fragment frg)
	{
	    if (frg.Attr == Fragment.Attributes.AnchorName)
		frg.Text = basePath + frg.Text;

	    next.PushFragment(frg);
	}

	public void Close()
	{
	    next.Close();
	}
    }

    // This is a document filter which optimizes whitespace fragments
    // and removes inline newline characters. All inline newlines in
    // preformatted blocks are replaced with line-break fragments.
    class NormalizeWhitespace : IDocumentConsumer
    {
	bool blockEmpty = true;
	bool wantSpace = false;
	bool isPreformatted = false;
	IDocumentConsumer consumer;

	Fragment.Attributes lastAttr;
	string lastLinkref;
	StringBuilder sb = new StringBuilder();

	public NormalizeWhitespace(IDocumentConsumer next)
	{
	    consumer = next;
	}

	public void PushBlock(Block blk)
	{
	    FlushFragment();

	    isPreformatted = (blk.BlockType == Block.Type.Preformatted);
	    wantSpace = false;
	    blockEmpty = true;

	    consumer.PushBlock(blk);
	}

	public void PushFragment(Fragment frg)
	{
	    if ((frg.Attr & Fragment.Attributes.Formatter) != 0)
	    {
		FlushFragment();
		consumer.PushFragment(frg);
	    }
	    else
	    {
		if ((frg.Linkref != lastLinkref) || (frg.Attr != lastAttr))
		    FlushFragment();

		lastAttr = frg.Attr;
		lastLinkref = frg.Linkref;

		if (isPreformatted)
		    PreText(frg.Text);
		else
		    NormalText(frg.Text);
	    }
	}

	public void Close()
	{
	    FlushFragment();
	    consumer.Close();
	}

	void PreText(string text)
	{
	    foreach (char c in text)
	    {
		if (c == '\n')
		{
		    FlushFragment();
		    consumer.PushFragment(new Fragment() {
			Attr = Fragment.Attributes.NewLine
		    });
		}
		else
		    sb.Append(c);
	    }
	}

	void NormalText(string text)
	{
	    foreach (char c in text)
	    {
		if (Char.IsWhiteSpace(c))
		{
		    wantSpace = true;
		}
		else
		{
		    if (wantSpace && !blockEmpty)
			sb.Append(' ');

		    sb.Append(c);

		    blockEmpty = false;
		    wantSpace = false;
		}
	    }
	}

	void FlushFragment()
	{
	    if (sb.Length <= 0)
		return;

	    consumer.PushFragment(new Fragment() {
		Attr = lastAttr,
		Text = sb.ToString(),
		Linkref = lastLinkref
	    });

	    sb.Clear();
	}
    }

    // This document filter wraps reflowable paragraphs to fit the given
    // width and allow for a specified indent per level. This filter
    // preserves the word-fragment count.
    class ReflowParagraphs : IDocumentConsumer
    {
	IDocumentConsumer consumer;
	int targetWidth;
	int indentPerLevel;

	// Current state
	int paraWidth;
	int paraPos;

	public ReflowParagraphs(IDocumentConsumer next,
				int tw = 79, int indent = 4)
	{
	    consumer = next;
	    targetWidth = tw;
	    indentPerLevel = indent;
	}

	public void PushBlock(Block blk)
	{
	    paraPos = 0;
	    paraWidth = targetWidth - blk.Indent * indentPerLevel;

	    if (blk.BlockType == Block.Type.Preformatted)
		paraWidth = -1;

	    consumer.PushBlock(blk);
	}

	public void PushFragment(Fragment frg)
	{
	    if (((frg.Attr & Fragment.Attributes.Formatter) != 0) ||
		(paraWidth <= 0))
	    {
		if (frg.Attr == Fragment.Attributes.NewLine)
		    paraPos = 0;

		consumer.PushFragment(frg);
	    } else if ((frg.Text != null) && (frg.Text.Length > 0)) {
		SendChunks(frg);
	    }
	}

	public void Close()
	{
	    consumer.Close();
	}

	void SendChunks(Fragment frg)
	{
	    int i = 0;

	    while (i < frg.Text.Length)
	    {
		bool hardBreak = false;

		// Skip leading whitespace if at start of line
		if ((paraPos == 0) && Char.IsWhiteSpace(frg.Text[i]))
		{
		    while (Char.IsWhiteSpace(frg.Text[i]))
			i++;
		    continue;
		}

		// Figure out how long the remainder is
		int len = frg.Text.Length - i;

		if (paraPos + len > paraWidth)
		{
		    // First, look for a whitespace break
		    len = paraWidth - paraPos;
		    while ((len > 0) && !Char.IsWhiteSpace(frg.Text[i + len]))
			len--;

		    // If there is a whitespace break that *could* fit
		    // within paraWidth, do a line break and try again.
		    if (len <= 0)
		    {
			int t = 0;

			while ((t < paraWidth) && (i + t < frg.Text.Length) &&
				!Char.IsWhiteSpace(frg.Text[i + t]))
			    t++;

			if (t < paraWidth)
			{
			    consumer.PushFragment(new Fragment() {
				Attr = Fragment.Attributes.NewLine
			    });

			    paraPos = 0;
			    continue;
			}

			// If that didn't work, just do a hard break.
			len = paraWidth - paraPos;
			hardBreak = true;
		    }
		}

		// Send this part of the fragment
		Fragment part;

		part.Attr = frg.Attr |
			(hardBreak ? Fragment.Attributes.DoNotCount : 0);
		part.Linkref = frg.Linkref;
		part.Text = frg.Text.Substring(i, len);
		consumer.PushFragment(part);

		paraPos += len;
		i += len;

		if (hardBreak)
		{
		    consumer.PushFragment(new Fragment() {
			Attr = Fragment.Attributes.NewLine
		    });
		    paraPos = 0;
		}
	    }
	}
    }

    // This document filter adds prefixes, indenting and inter-paragraph
    // spacing. This filter preserves the word-fragment count.
    class IndentText : IDocumentConsumer
    {
	IDocumentConsumer consumer;
	int indentPerLevel;

	bool blockEmpty = true;
	bool lineEmpty = true;
	int paraIndent;
	string paraPrefix;

	public IndentText(IDocumentConsumer next, int indent = 4)
	{
	    consumer = next;
	    indentPerLevel = indent;
	}

	public void PushBlock(Block blk)
	{
	    if (!lineEmpty)
		consumer.PushFragment(new Fragment() {
		    Attr = Fragment.Attributes.NewLine
		});

	    if (!blockEmpty)
		consumer.PushFragment(new Fragment() {
		    Attr = Fragment.Attributes.NewLine
		});

	    blockEmpty = true;
	    lineEmpty = true;
	    paraPrefix = blk.Prefix;
	    paraIndent = indentPerLevel * blk.Indent;

	    consumer.PushBlock(blk);
	}

	public void PushFragment(Fragment frg)
	{
	    if ((frg.Attr & Fragment.Attributes.Formatter) != 0)
	    {
		if (frg.Attr == Fragment.Attributes.NewLine)
		    lineEmpty = true;

		consumer.PushFragment(frg);
	    } else {
		if (lineEmpty)
		    SendIndent();

		consumer.PushFragment(frg);
		lineEmpty = false;
	    }

	    blockEmpty = false;
	}

	void SendIndent()
	{
	    if (paraIndent > 0)
	    {
		Fragment id = new Fragment();

		if (paraPrefix != null)
		{
		    id.Text = paraPrefix + ' ';
		    paraPrefix = null;

		    if (id.Text.Length < paraIndent)
			id.Text = new string(' ',
			    paraIndent - id.Text.Length) + id.Text;
		}
		else
		    id.Text = new string(' ', paraIndent);

		id.Attr = Fragment.Attributes.DoNotCount;
		consumer.PushFragment(id);
	    }
	}

	public void Close()
	{
	    if (!lineEmpty)
		consumer.PushFragment(new Fragment() {
		    Attr = Fragment.Attributes.NewLine
		});

	    consumer.Close();
	}
    }
}
