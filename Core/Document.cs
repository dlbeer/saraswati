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

namespace Saraswati.Core
{
    // A document consists of a sequence of blocks, which in turn
    // consists of a sequence of fragments. A block is a vertical
    // separable piece of text which can be reflowed independently of
    // other blocks.
    class Block
    {
	public enum Type
	{
	    Paragraph,
	    Preformatted
	}

	public Type		BlockType;
	public int		Indent;
	public string		Prefix;
    }

    // Fragments within a block are of two kinds:
    //
    //   (i)  Stretches of text characters with associated formatting
    //        attributes: (Attr & Formatter) == 0.
    //   (ii) Special formatting characters without associated
    //        attributes: (Attr & Formatter) != 0.
    struct Fragment
    {
	[Flags]
	public enum Attributes
	{
	    Bold	= 0x01,
	    Italic	= 0x02,
	    Code	= 0x04,
	    Heading     = 0x08,

	    // Some filters preserve a property known as the
	    // word-fragment count. In these filters, the word count
	    // (fragment boundaries are also considered word boundaries)
	    // doesn't change, if you consider only fragments without
	    // the DoNotCount attribute set.
	    DoNotCount  = 0x10,

	    Formatter	= 0x10000,
	    NewLine	= 0x10001,
	    AnchorName	= 0x10002
	}

	public int WordCount
	{
	    get
	    {
		bool lastSpace = true;
		int count = 0;

		if ((Text == null) ||
		    ((Attr & (Attributes.DoNotCount |
			      Attributes.Formatter)) != 0))
		    return 0;

		foreach (char c in Text)
		{
		    bool space = Char.IsWhiteSpace(c);

		    if (lastSpace && !space)
			count++;
		    lastSpace = space;
		}

		return count;
	    }
	}

	public Attributes	Attr;
	public string		Text;
	public string		Linkref;
    }

    // Documents are supplied by calling methods of an IDocumentConsumer
    // interface. Blocks and fragments are supplied in sequence,
    // followed by a call to Close(). Fragments are supplied after the
    // blocks which contain them.
    interface IDocumentConsumer
    {
	void PushBlock(Block blk);
	void PushFragment(Fragment frg);
	void Close();
    }
}
