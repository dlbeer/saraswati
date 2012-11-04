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
    // Status query widget. This widget is analogous to a dialog box. It
    // contains a label and an entry box for the user to enter text. It
    // provides a Done event which is invoked when the user presses
    // either Enter (passing entered text) or Ctrl+G (passing null).
    //
    // Also provided is a Run() method for modal use.
    class StatusQuery : IWidget
    {
	const TerminalColor LabelColor =
		TerminalColor.Bold | TerminalColor.White;

	public readonly EntryBox Entry = new EntryBox();
	string label;
	int row;
	int width;
	int column;

	public class DoneEventArgs : EventArgs
	{
	    public readonly string Text;

	    public DoneEventArgs(string t) { Text = t; }
	}

	public delegate void DoneEventHandler(object sender,
		DoneEventArgs args);

	public event DoneEventHandler DoneEvent;

	public StatusQuery(string lbl)
	{
	    label = lbl;
	}

	public string Label
	{
	    get { return label; }
	    set
	    {
		label = value;
		sizeChildren();
	    }
	}

	void sizeChildren()
	{
	    Entry.SetSize(1, width - label.Length - 1,
			  row, column + label.Length + 1);
	}

	public void SetSize(int h, int w, int r, int c)
	{
	    width = w - 1;
	    row = r + h - 1;
	    column = c;
	    sizeChildren();
	}

	public void Draw()
	{
	    Terminal.Goto(row, column);
	    Terminal.SetColor(LabelColor);
	    Terminal.AddString(label);
	    Terminal.AddChar(' ', 1);
	    Entry.Draw();
	}

	public bool SendKey(TerminalKey key)
	{
	    if (key == TerminalKey.CtrlG)
	    {
		if (DoneEvent != null)
		    DoneEvent(this, new DoneEventArgs(null));
		return true;
	    }

	    if (key == TerminalKey.Enter)
	    {
		if (DoneEvent != null)
		    DoneEvent(this, new DoneEventArgs(Entry.Text));
		return true;
	    }

	    return Entry.SendKey(key);
	}

	public string Run()
	{
	    DoneEventArgs[] args = new DoneEventArgs[1];
	    DoneEventHandler handler = (obj, a) => { args[0] = a; };

	    DoneEvent += handler;

	    try
	    {
		Display.Show(this);
		while (args[0] == null)
		    Display.Iterate();
	    }
	    finally
	    {
		DoneEvent -= handler;
	    }

	    Display.Hide(this);
	    return args[0].Text;
	}
    }

    // This is a top-level widget which acts as a simple message pop-up.
    // It provides a Dismiss event, to which the dismissal event is
    // passed. It also provides a modal Run() method, and a static Say()
    // method which can be used to display ad-hoc messages in a modal
    // fashion.
    class StatusMessage : IWidget
    {
	public const TerminalColor Error =
	    TerminalColor.Red | TerminalColor.Bold;
	public const TerminalColor Info =
	    TerminalColor.White;

	public class DismissEventArgs : EventArgs
	{
	    public TerminalKey Key;

	    public DismissEventArgs(TerminalKey k) { Key = k; }
	}

	public delegate void DismissEventHandler(object sender,
		DismissEventArgs args);

	public event DismissEventHandler DismissEvent;

	string text;
	TerminalColor color;

	int row;
	int column;
	int width;

	public StatusMessage(string t, TerminalColor col = Info)
	{
	    color = col;
	    text = t;
	}

	public string Text
	{
	    get { return text; }
	    set { text = value; }
	}

	public TerminalColor Color
	{
	    get { return color; }
	    set { color = value; }
	}

	public void SetSize(int h, int w, int r, int c)
	{
	    width = w - 1;
	    row = r + h - 1;
	    column = c;
	}

	public void Draw()
	{
	    string t = text;

	    if (t.Length > width)
		t = t.Substring(0, width);

	    Terminal.Goto(row, column);
	    Terminal.SetColor(color);
	    Terminal.AddString(t);
	    Terminal.AddChar(' ', width - t.Length);
	    Terminal.Goto(row, column + t.Length);
	}

	public bool SendKey(TerminalKey key)
	{
	    if (DismissEvent != null)
		DismissEvent(this, new DismissEventArgs(key));

	    return true;
	}

	public TerminalKey Run()
	{
	    DismissEventArgs[] args = new DismissEventArgs[1];
	    DismissEventHandler handler = (obj, a) => { args[0] = a; };

	    DismissEvent += handler;

	    try
	    {
		Display.Show(this);
		while (args[0] == null)
		    Display.Iterate();
	    }
	    finally
	    {
		DismissEvent -= handler;
	    }

	    Display.Hide(this);
	    return args[0].Key;
	}

	public static TerminalKey Say(string text, TerminalColor col = Info)
	{
	    StatusMessage msg = new StatusMessage(text);

	    return msg.Run();
	}
    }
}
