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
using System.Collections.Generic;

namespace Saraswati.UI
{
    // This widget implements an editable entry box with all the usual
    // terminal functions and cursor navigation. It makes use of the
    // clipboard.
    //
    // No events are provided, because it's expected that you'll embed
    // this widget within another composite widget.
    class EntryBox : IWidget
    {
	const TerminalColor ColorText = TerminalColor.White;
	const TerminalColor ColorTab =
		TerminalColor.Bold | TerminalColor.Yellow;

	StringBuilder buffer = new StringBuilder();
	int row;
	int col;
	int width;
	int scroll = 0;
	int cursor = 0;

	public delegate string[] CompletionFunc(string text);

	string[] completionList = null;
	int completionIndex = -1;
	CompletionFunc completer;

	string[] historyList;
	int historyIndex = -1;

	public EntryBox() { }

	public CompletionFunc Completer
	{
	    set { completer = value; }
	}

	public string[] History
	{
	    set
	    {
		historyList = value;
		historyIndex = -1;
	    }
	}

	public void Reset()
	{
	    buffer.Clear();
	    scroll = 0;
	    cursor = 0;
	    fixScroll();

	    completionList = null;
	    completionIndex = -1;

	    historyIndex = -1;
	}

	public string Text
	{
	    get { return buffer.ToString(); }
	    set
	    {
		buffer.Clear();
		buffer.Append(value);
		cursor = buffer.Length;
		fixScroll();

		completionList = null;
		completionIndex = -1;
	    }
	}

	void fixScroll()
	{
	    int ew = width - 2;

	    if (width < 3)
		return;

	    if (scroll > cursor)
		scroll = cursor;
	    if (scroll + ew <= cursor)
		scroll = cursor - ew + 1;
	    if (scroll < 0)
		scroll = 0;
	}

	public void SetSize(int h, int w, int r, int c)
	{
	    row = r;
	    col = c;
	    width = w;

	    fixScroll();
	}

	public bool SendKey(TerminalKey key)
	{
	    if (key != TerminalKey.Tab)
	    {
		completionList = null;
		completionIndex = -1;
	    }

	    switch (key)
	    {
	    case TerminalKey.Tab:
		if (completionList == null)
		{
		    completionList = completer(buffer.ToString());
		    completionIndex = -1;
		}

		if ((completionList != null) && (completionList.Length > 0))
		{
		    completionIndex = (completionIndex + 1) %
			completionList.Length;

		    buffer.Clear();
		    buffer.Append(completionList[completionIndex]);

		    cursor = buffer.Length;
		    fixScroll();
		}
		return true;

	    case TerminalKey.Up:
		if ((historyList != null) && historyList.Length > 0)
		{
		    if (historyIndex >= 0)
			historyIndex--;
		    else
			historyIndex = historyList.Length - 1;

		    buffer.Clear();

		    if (historyIndex >= 0)
			buffer.Append(historyList[historyIndex]);

		    cursor = buffer.Length;
		    fixScroll();
		}
		return true;

	    case TerminalKey.Down:
		if ((historyList != null) && historyList.Length > 0)
		{
		    if (historyIndex >= 0)
			historyIndex++;
		    if (historyIndex >= historyList.Length)
			historyIndex = -1;

		    buffer.Clear();

		    if (historyIndex >= 0)
			buffer.Append(historyList[historyIndex]);

		    cursor = buffer.Length;
		    fixScroll();
		}
		return true;

	    case TerminalKey.Left:
		if (cursor > 0)
		{
		    cursor--;
		    fixScroll();
		}
		return true;

	    case TerminalKey.Right:
		if (cursor < buffer.Length)
		{
		    cursor++;
		    fixScroll();
		}
		return true;

	    case TerminalKey.CtrlH:
	    case TerminalKey.Backspace:
	    case TerminalKey.Delete:
		if (cursor > 0)
		{
		    buffer.Remove(cursor - 1, 1);
		    cursor--;
		    fixScroll();
		}
		return true;

	    case TerminalKey.CtrlD:
		if (cursor < buffer.Length)
		    buffer.Remove(cursor, 1);
		return true;

	   case TerminalKey.Home:
	   case TerminalKey.CtrlA:
		cursor = 0;
		fixScroll();
		return true;

	   case TerminalKey.End:
	   case TerminalKey.CtrlE:
		cursor = buffer.Length;
		fixScroll();
		return true;

	    case TerminalKey.CtrlK:
		Clipboard.Content =
		    buffer.ToString(cursor, buffer.Length - cursor);
		buffer.Remove(cursor, buffer.Length - cursor);
		return true;

	    case TerminalKey.CtrlU:
		Clipboard.Content = buffer.ToString(0, cursor);
		buffer.Remove(0, cursor);
		cursor = 0;
		fixScroll();
		return true;

	    case TerminalKey.CtrlY:
		if (Clipboard.Content != null)
		{
		    string text = Clipboard.Content;

		    buffer.Insert(cursor, text);
		    cursor += text.Length;
		    fixScroll();
		}
		return true;

	    default:
		if ((int)key >= 32 && (int)key < 127)
		{
		    char ch = (char)key;

		    buffer.Insert(cursor, ch);
		    cursor++;
		    fixScroll();

		    return true;
		}
		break;
	    }

	    return false;
	}

	public void Draw()
	{
	    int ew = width - 2;
	    int dw = ew;

	    if (width < 3)
		return;

	    if (scroll + dw > buffer.Length)
		dw = buffer.Length - scroll;

	    Terminal.SetColor(ColorText);
	    Terminal.Goto(row, col);
	    Terminal.AddChar(' ', 1);
	    Terminal.AddString(buffer.ToString(scroll, dw));
	    Terminal.AddChar(' ', ew - dw + 1);

	    Terminal.SetColor(ColorTab);

	    if (scroll > 0)
	    {
		Terminal.Goto(row, col);
		Terminal.AddACS(TerminalACS.LeftArrow, 1);
	    }

	    if (scroll + ew < buffer.Length)
	    {
		Terminal.Goto(row, col + width - 1);
		Terminal.AddACS(TerminalACS.RightArrow, 1);
	    }

	    Terminal.Goto(row, col + 1 + cursor - scroll);
	}
    }

    class FileCompleter
    {
	public readonly string Pattern;

	public FileCompleter(string pat) { Pattern = pat; }

	public FileCompleter() : this("*") { }

	public string[] Match(string text)
	{
	    List<string> results = new List<string>();
	    string[] full = null;

	    try
	    {
		string dirName = Path.GetDirectoryName(text);
		string prefix = Path.GetFileName(text);
		DirectoryInfo info = new DirectoryInfo(dirName);

		foreach (DirectoryInfo dir in info.EnumerateDirectories())
		    if (MatchName(prefix, dir.Name))
			results.Add(dir.Name);

		foreach (FileInfo file in info.EnumerateFiles(Pattern))
		    if (MatchName(prefix, file.Name))
			results.Add(file.Name);

		full = new string[results.Count];

		for (int i = 0; i < results.Count; i++)
		    full[i] = Path.Combine(dirName, results[i]);
	    }
	    catch { }

	    if (results.Count == 0)
		return null;

	    return full;
	}

	static bool MatchName(string prefix, string name)
	{
	    return ((name.Length >= prefix.Length) &&
		(name.Substring(0, prefix.Length) == prefix));
	}
    }
}
