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

namespace Saraswati.UI
{
    class DocumentView : IWidget
    {
	const TerminalColor ColorHeading =
	    TerminalColor.Yellow | TerminalColor.Bold;
	const TerminalColor ColorBold =
	    TerminalColor.White | TerminalColor.Bold;
	const TerminalColor ColorItalic = TerminalColor.Yellow;
	const TerminalColor ColorCode = TerminalColor.Green;
	const TerminalColor ColorCursor =
	    TerminalColor.White | TerminalColor.Bold;
	const TerminalColor ColorLink =
	    TerminalColor.Blue | TerminalColor.Bold;
	const TerminalColor ColorLinkActive =
	    TerminalColor.Yellow | TerminalColor.Bold | TerminalColor.BgBlue;
	const TerminalColor ColorText = TerminalColor.White;

	Config config;

	Core.DocumentStore doc = new Core.DocumentStore();
	Core.Pasteboard pasteBoard = new Core.Pasteboard();
	Core.SearchIndex searchIndex = new Core.SearchIndex();

	StatusQuery searchQuery = new StatusQuery("Find text:");

	int width;
	int height;
	int row;
	int column;

	int scroll;
	int cursor;
	int cursorFragment;

	public DocumentView(Config cfg)
	{
	    config = cfg;
	}

	public string ActiveLink
	{
	    get
	    {
		if (cursorFragment >= 0 &&
		    cursorFragment < pasteBoard.FragmentCount)
		    return pasteBoard.GetFragment(cursorFragment).Linkref;

		return null;
	    }
	}

	public int LinePos
	{
	    get { return cursor; }
	}

	public int LineCount
	{
	    get { return pasteBoard.LineCount; }
	}

	public Core.DocumentStore Document
	{
	    get { return doc; }
	    set
	    {
		doc = value;

		searchIndex.Clear();
		doc.Replay(searchIndex);

		doReflow();

		cursor = 0;
		scroll = 0;
		fixScroll();
	    }
	}

	public int WordPos
	{
	    get
	    {
		if (cursor >= 0 && cursor < pasteBoard.LineCount)
		    return pasteBoard.LineFirstWord(cursor) +
			   pasteBoard.LineNumWords(cursor) / 2;

		return 0;
	    }

	    set
	    {
		cursor = pasteBoard.WordToLine(value);
		scroll = cursor - height / 2;
		fixScroll();
	    }
	}

	void stableReflow()
	{
	    int pos = WordPos;

	    doReflow();

	    WordPos = pos;
	}

	public void SetSize(int h, int w, int r, int c)
	{
	    height = h;
	    width = w;
	    row = r;
	    column = c;

	    stableReflow();
	}

	void drawLine(int line, int w)
	{
	    int fs = pasteBoard.LineFirstFragment(line);
	    int fn = pasteBoard.LineNumFragments(line);

	    for (int i = 0; i < fn; i++)
	    {
		Core.Fragment frg = pasteBoard.GetFragment(fs + i);
		string text = frg.Text;

		if (fs + i == cursorFragment)
		    Terminal.SetColor(ColorLinkActive);
		else if (frg.Linkref != null)
		    Terminal.SetColor(ColorLink);
		else if ((frg.Attr & Core.Fragment.Attributes.Heading) != 0)
		    Terminal.SetColor(ColorHeading);
		else if ((frg.Attr & Core.Fragment.Attributes.Bold) != 0)
		    Terminal.SetColor(ColorBold);
		else if ((frg.Attr & Core.Fragment.Attributes.Italic) != 0)
		    Terminal.SetColor(ColorItalic);
		else if ((frg.Attr & Core.Fragment.Attributes.Code) != 0)
		    Terminal.SetColor(ColorCode);
		else
		    Terminal.SetColor(ColorText);

		if (text.Length > w)
		    text = text.Substring(0, w);

		Terminal.AddString(text);
		w -= text.Length;
	    }

	    Terminal.SetColor(ColorText);
	    Terminal.AddChar(' ', w);
	}

	public void Draw()
	{
	    int i;

	    for (i = 0; i < height; i++)
	    {
		int line = scroll + i;

		Terminal.Goto(row + i, column);
		Terminal.SetColor(ColorCursor);

		if (line == cursor)
		    Terminal.AddString("--> ");
		else
		    Terminal.AddString("    ");

		if (line >= 0 && line < pasteBoard.LineCount)
		    drawLine(line, width - 4);
		else
		    Terminal.AddChar(' ', width - 4);
	    }

	    if (cursor >= 0 && cursor < pasteBoard.LineCount)
		Terminal.Goto(cursor - scroll + row, column);
	    else
		Terminal.Goto(row, column);
	}

	public bool SendKey(TerminalKey key)
	{
	    switch (key)
	    {
	    case ((TerminalKey)'\\'):
		config.HalfWidth = !config.HalfWidth;
		stableReflow();
		return true;

	    case TerminalKey.Home:
	    case ((TerminalKey)'<'):
		cursor = 0;
		fixScroll();
		return true;

	    case TerminalKey.End:
	    case ((TerminalKey)'>'):
		cursor = pasteBoard.LineCount - 1;
		fixScroll();
		return true;

	    case TerminalKey.Up:
	    case ((TerminalKey)'k'):
		if (cursor > 0)
		{
		    cursor--;
		    fixScroll();
		}
		return true;

	    case TerminalKey.Down:
	    case ((TerminalKey)'j'):
		if (cursor + 1 < pasteBoard.LineCount)
		{
		    cursor++;
		    fixScroll();
		}
		return true;

	    case TerminalKey.Left:
		gotoPrevLink();
		return true;

	    case TerminalKey.Right:
		gotoNextLink();
		return true;

	    case TerminalKey.PageUp:
	    case TerminalKey.CtrlP:
		if (cursor > height)
		{
		    cursor -= height;
		    scroll -= height;
		}
		else
		{
		    cursor = 0;
		}
		fixScroll();
		return true;

	    case TerminalKey.PageDown:
	    case TerminalKey.CtrlN:
		if (cursor + height < pasteBoard.LineCount)
		{
		    cursor += height;
		    scroll += height;
		}
		else
		{
		    cursor = pasteBoard.LineCount - 1;
		}
		fixScroll();
		return true;

	    case ((TerminalKey)'/'):
		searchQuery.Entry.Reset();
		searchQuery.Entry.History = config.SearchHistory.ToArray();

		if (searchQuery.Run() != null)
		{
		    config.SearchHistory.Add(searchQuery.Entry.Text);
		    searchForward();
		}
		return true;

	    case ((TerminalKey)'?'):
		searchQuery.Entry.Reset();
		if (searchQuery.Run() != null)
		{
		    config.SearchHistory.Add(searchQuery.Entry.Text);
		    searchBack();
		}
		return true;

	    case ((TerminalKey)'n'):
		searchForward();
		return true;

	    case ((TerminalKey)'p'):
		searchBack();
		return true;
	    }

	    return false;
	}

	bool searchBack()
	{
	    if (cursor <= 0)
	    {
		StatusMessage.Say("Start of document reached");
		return false;
	    }

	    int startPos = pasteBoard.LineFirstWord(cursor - 1) +
		pasteBoard.LineNumWords(cursor - 1) - 1;
	    int result = searchIndex.SearchBackwards(searchQuery.Entry.Text,
		startPos);

	    if (result < 0) {
		StatusMessage.Say("Search string not found",
			StatusMessage.Error);
		return false;
	    }

	    cursor = pasteBoard.WordToLine(result);
	    fixScroll();
	    return true;
	}

	bool searchForward()
	{
	    if (cursor + 1 >= pasteBoard.LineCount)
	    {
		StatusMessage.Say("End of document reached");
		return false;
	    }

	    int startPos = pasteBoard.LineFirstWord(cursor + 1);
	    int result = searchIndex.SearchForwards(searchQuery.Entry.Text,
		startPos);

	    if (result < 0) {
		StatusMessage.Say("Search string not found",
			StatusMessage.Error);
		return false;
	    }

	    cursor = pasteBoard.WordToLine(result);
	    fixScroll();
	    return true;
	}

	void doReflow()
	{
	    pasteBoard.Clear();
	    Core.IDocumentConsumer filter = pasteBoard;

	    int w = width;

	    if (config.HalfWidth)
		w /= 2;

	    filter = new Core.IndentText(filter);
	    filter = new Core.ReflowParagraphs(filter, w - 6);

	    doc.Replay(filter);
	}

	void gotoFirstLink()
	{
	    // Find the first link fragment
	    if (cursor >= 0 && cursor < pasteBoard.LineCount)
	    {
		int fs = pasteBoard.LineFirstFragment(cursor);
		int fn = pasteBoard.LineNumFragments(cursor);

		for (int i = 0; i < fn; i++)
		{
		    if (pasteBoard.GetFragment(fs + i).Linkref != null)
		    {
			cursorFragment = fs + i;
			break;
		    }
		}
	    }
	}

	void gotoPrevLink()
	{
	    if (cursorFragment >= 0)
	    {
		int fs = pasteBoard.LineFirstFragment(cursor);

		for (int i = cursorFragment - 1; i >= fs; i--)
		    if (pasteBoard.GetFragment(i).Linkref != null)
		    {
			cursorFragment = i;
			break;
		    }
	    }
	    else
	    {
		gotoFirstLink();
	    }
	}

	void gotoNextLink()
	{
	    if (cursorFragment >= 0)
	    {
		int fs = pasteBoard.LineFirstFragment(cursor);
		int fn = pasteBoard.LineNumFragments(cursor);

		for (int i = cursorFragment + 1; i < fs + fn; i++)
		    if (pasteBoard.GetFragment(i).Linkref != null)
		    {
			cursorFragment = i;
			break;
		    }
	    }
	    else
	    {
		gotoFirstLink();
	    }
	}

	void fixScroll()
	{
	    cursorFragment = -1;
	    if (cursor < 0 || cursor >= pasteBoard.LineCount)
		cursor = 0;

	    if (scroll > cursor)
		scroll = cursor;

	    if (scroll + height <= cursor)
		scroll = cursor - height + 1;

	    if (scroll + height >= pasteBoard.LineCount)
		scroll = pasteBoard.LineCount - height + 1;

	    if (scroll < 0)
		scroll = 0;

	    gotoFirstLink();
	}
    }
}
