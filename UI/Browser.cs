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
using System.Text;
using System.Reflection;

namespace Saraswati.UI
{
    class BookInfo
    {
	public readonly string			Filename;
	public readonly Core.DocumentStore	BookText;
	public readonly Core.TableOfContents	Contents;
	public readonly Core.AnchorIndex	Anchors;
	public readonly Core.SectionIndex	SectIndex;

	private BookInfo(string f, Core.DocumentStore ds,
			 Core.TableOfContents toc)
	{
	    Filename = f;
	    BookText = ds;
	    Contents = toc;

	    Anchors = new Core.AnchorIndex();
	    BookText.Replay(Anchors);

	    SectIndex = new Core.SectionIndex(Anchors, Contents);
	}

	static Stream helpHTML()
	{
	    return Assembly.GetExecutingAssembly().GetManifestResourceStream
		("help.html");
	}

	public static BookInfo GetHelp()
	{
	    Core.DocumentStore helpDoc = new Core.DocumentStore();
	    Core.TableOfContents toc;

	    using (Stream s = helpHTML())
		toc = Core.HeadingScan.Scan(s);

	    using (Stream s = helpHTML())
		Core.XHTMLParser.ParseStream(s,
		    new Core.NormalizeWhitespace(helpDoc));

	    return new BookInfo(null, helpDoc, toc);
	}

	public static BookInfo TryCreate(string path)
	{
	    try
	    {
		path = Path.GetFullPath(path);

		using (Core.EPubFile file = new Core.EPubFile(path))
		{
		    Core.DocumentStore newDoc = new Core.DocumentStore();

		    file.ProduceBook(newDoc);
		    return new BookInfo(path, newDoc, file.Toc);
		}
	    }
	    catch (Exception ex)
	    {
		StatusMessage.Say(string.Format("Can't open {0}: {1}",
		    path, ex.Message));
		return null;
	    }
	}
    }

    class Browser : IWidget
    {
	const TerminalColor ColorStatus =
	    TerminalColor.Yellow | TerminalColor.Bold | TerminalColor.BgBlue;

	const int MaxHistory = 64;

	class NavMark
	{
	    public readonly string	Filename;
	    public readonly int		Position;

	    public NavMark() { }

	    public NavMark(string f, int pos)
	    {
		Filename = f;
		Position = pos;
	    }
	}

	StatusQuery openQuery = new StatusQuery("Open file:");
	Config config;

	DocumentView docView;
	BookInfo currentBook;

	LRUDeque<NavMark> history = new LRUDeque<NavMark>(MaxHistory);
	LRUDeque<NavMark> forwardHistory = new LRUDeque<NavMark>(MaxHistory);

	int height;
	int width;
	int row;
	int column;

	public Browser(Config cfg)
	{
	    config = cfg;
	    docView = new DocumentView(config);

	    openQuery.Entry.Completer =
		(new FileCompleter("*.epub")).Match;
	}

	public void OpenBook(BookInfo inf)
	{
	    PreNavigate(history);

	    currentBook = inf;
	    docView.Document = currentBook.BookText;

	    int pos;

	    if ((inf.Filename != null) &&
		config.Bookmarks.TryGetValue(inf.Filename, out pos))
		docView.WordPos = pos;
	}

	public bool TryOpenFile(string path)
	{
	    BookInfo inf = BookInfo.TryCreate(path);

	    if (inf == null)
		return false;

	    OpenBook(inf);
	    return true;
	}

	public void SetSize(int h, int w, int r, int c)
	{
	    h--;
	    height = h;
	    width = w;
	    row = r;
	    column = c;

	    docView.SetSize(h - 2, w, r + 1, c);
	}

	public void Save()
	{
	    if ((currentBook != null) && (currentBook.Filename != null) &&
		(docView.Document == currentBook.BookText))
		config.Bookmarks.Put(currentBook.Filename, docView.WordPos);
	}

	void openToc(int depth)
	{
	    if (currentBook == null)
		return;

	    Core.DocumentStore doc = new Core.DocumentStore();
	    Core.TocPrinter.PrintToc(currentBook.Contents, doc, depth);

	    docView.Document = doc;
	}

	void drawStatus(int r, string text)
	{
	    if (text == null)
		text = "";

	    if (text.Length > width)
		text = text.Substring(0, width);

	    Terminal.Goto(r, column);
	    Terminal.SetColor(ColorStatus);
	    Terminal.AddString(text);
	    Terminal.AddChar(' ', width - text.Length);
	}

	string topStatus()
	{
	    if (currentBook == null)
		return "No document";

	    return currentBook.Contents.Title;
	}

	string bottomStatus()
	{
	    StringBuilder sb = new StringBuilder();
	    int pos = docView.LinePos + 1;
	    int total = docView.LineCount;

	    if (total > 0)
		sb.Append(string.Format("{0}/{1} ({2}%)",
			  pos, total, pos * 100 / total));

	    if ((currentBook != null) &&
		(docView.Document == currentBook.BookText))
	    {
		string name =
		    currentBook.SectIndex.WordToName(docView.WordPos);

		if (name != null)
		{
		    if (sb.Length > 0)
			sb.Append(" :: ");
		    sb.Append(name);
		}
	    }

	    return sb.ToString();
	}

	public void Draw()
	{
	    drawStatus(row, topStatus());
	    drawStatus(row + height - 1, bottomStatus());
	    docView.Draw();
	}

	void PreNavigate(LRUDeque<NavMark> hist)
	{
	    if ((currentBook != null) && (currentBook.Filename != null) &&
		(docView.Document == currentBook.BookText))
		hist.PushBack(new NavMark(currentBook.Filename,
			docView.WordPos));
	}

	void restoreMark(NavMark m)
	{
	    if ((currentBook == null) || (currentBook.Filename != m.Filename))
	    {
		BookInfo inf = BookInfo.TryCreate(m.Filename);

		if (inf == null)
		    return;

		currentBook = inf;
	    }

	    if (docView.Document != currentBook.BookText)
		docView.Document = currentBook.BookText;

	    docView.WordPos = m.Position;
	}

	void GoBack()
	{
	    NavMark m;

	    if (history.TryPopBack(out m))
	    {
		PreNavigate(forwardHistory);
		restoreMark(m);
	    }
	}

	void GoForward()
	{
	    NavMark m;

	    if (forwardHistory.TryPopBack(out m))
	    {
		PreNavigate(history);
		restoreMark(m);
	    }
	}

	void followLink()
	{
	    if (currentBook == null)
		return;

	    string link = docView.ActiveLink;

	    if (link == null)
		return;

	    int pos = currentBook.Anchors.FindAnchor(link);

	    if (pos < 0)
	    {
		StatusMessage.Say(string.Format("No such anchor: {0}", link),
		    StatusMessage.Error);
		return;
	    }

	    PreNavigate(history);

	    if (docView.Document != currentBook.BookText)
		docView.Document = currentBook.BookText;

	    docView.WordPos = pos;
	}

	public bool SendKey(TerminalKey key)
	{
	    Save();

	    switch (key)
	    {
	    case TerminalKey.Enter:
		followLink();
		return true;

	    case TerminalKey.Backspace:
	    case TerminalKey.Delete:
	    case TerminalKey.CtrlH:
		GoBack();
		return true;

	    case TerminalKey.CtrlF:
		GoForward();
		return true;

	    case ((TerminalKey)'#'):
		if (currentBook.BookText != null)
		{
		    string link = docView.ActiveLink;

		    if (link != null)
			StatusMessage.Say(link);
		}
		return true;

	    case ((TerminalKey)'h'):
		OpenBook(BookInfo.GetHelp());
		return true;

	    case ((TerminalKey)'t'):
		PreNavigate(history);
		openToc(-1);
		return true;

	    case ((TerminalKey)'T'):
		PreNavigate(history);
		openToc(0);
		return true;

	    case ((TerminalKey)'b'):
		if ((currentBook != null) &&
		    (docView.Document != currentBook.BookText))
		{
		    int pos;

		    docView.Document = currentBook.BookText;
		    if ((currentBook.Filename != null) &&
			config.Bookmarks.TryGetValue(currentBook.Filename,
			    out pos))
			docView.WordPos = pos;
		}
		return true;

	    case ((TerminalKey)'o'):
		openQuery.Entry.Reset();
		openQuery.Entry.History = config.Bookmarks.ToKeysArray();

		for (;;)
		{
		    if (openQuery.Run() == null)
			break;

		    if (TryOpenFile(openQuery.Entry.Text))
			break;
		}

		return true;
	    }

	    return docView.SendKey(key);
	}
    }
}
