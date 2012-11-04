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
using System.Collections.Generic;

namespace Saraswati.UI
{
    // Shared clipboard. Multiple widgets can cut/yank to and from this
    // clipboard.
    class Clipboard
    {
	public static string Content;
    }

    // Widget interface. All widgets must implement three methods:
    interface IWidget
    {
	// Called when the terminal size changes, used to allocate a
	// rectangular section of the screen to a widget.  Coordinates
	// are relative to the whole screen.
	void SetSize(int h, int w, int r, int c);

	// Called when a key is pressed and this widget currently has
	// the focus. The method should return true if the key event was
	// processed by the widget, false otherwise.
	bool SendKey(TerminalKey key);

	// Called before waiting for a keypress. The widget should draw
	// itself to the screen at its assigned location using the
	// Terminal methods.
	void Draw();
    }

    // Display manager. This set of static methods allows widgets to
    // display new widgets on the screen by placing them on a display
    // stack.
    //
    // To use the display manager, initialize the terminal, Show() the
    // first widget and then call Iterate() until the application is
    // done.
    class Display
    {
	static List<IWidget> widgets = new List<IWidget>();

	public static void Show(IWidget widget)
	{
	    int w, h;

	    Hide(widget);
	    widgets.Add(widget);

	    Terminal.GetSize(out h, out w);
	    widget.SetSize(h, w, 0, 0);
	}

	public static void Hide(IWidget widget)
	{
	    widgets.Remove(widget);
	}

	// Perform one iteration of the event loop:
	//
	//   - draw the current display stack
	//   - wait for a key
	//   - deliver the key to the focused widget
	public static TerminalKey Iterate()
	{
	    Terminal.Erase();
	    foreach (IWidget w in widgets)
		w.Draw();
	    Terminal.Refresh();

	    TerminalKey k = Terminal.Getch();

	    SendKey(k);
	    return k;
	}

	static void SendKey(TerminalKey k)
	{
	    if (k == TerminalKey.CtrlL)
		Terminal.Clear();

	    if (k == TerminalKey.Resize)
	    {
		int w, h;

		Terminal.GetSize(out h, out w);
		foreach (IWidget layer in widgets)
		    layer.SetSize(h, w, 0, 0);

		Terminal.Clear();
	    }

	    if (widgets.Count > 0)
		widgets[widgets.Count - 1].SendKey(k);
	}
    }
}
