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
using System.Runtime.InteropServices;

namespace Saraswati.UI
{
    // Constants representing key events. ASCII keys are represented by
    // their ASCII values.
    enum TerminalKey
    {
	CtrlA = 1,
	CtrlB = 2,
	CtrlC = 3,
	CtrlD = 4,
	CtrlE = 5,
	CtrlF = 6,
	CtrlG = 7,
	CtrlH = 8,
	CtrlI = 9,
	CtrlJ = 10,
	CtrlK = 11,
	CtrlL = 12,
	CtrlM = 13,
	CtrlN = 14,
	CtrlO = 15,
	CtrlP = 16,
	CtrlQ = 17,
	CtrlR = 18,
	CtrlS = 19,
	CtrlT = 20,
	CtrlU = 21,
	CtrlV = 22,
	CtrlW = 23,
	CtrlX = 24,
	CtrlY = 25,
	CtrlZ = 26,
	Tab = 9,
	Enter = 10,
	Delete = 127,
	Down = 258,
	Up = 259,
	Left = 260,
	Right = 261,
	Home = 262,
	Backspace = 263,
	PageDown = 338,
	PageUp = 339,
	End = 360,
	Resize = 410
    }

    // Console color constants. You can combine background, foreground
    // and bold colors by or'ing together flags.
    [Flags]
    enum TerminalColor
    {
	Black = 0x00,
	Red = 0x01,
	Green = 0x02,
	Yellow = 0x03,
	Blue = 0x04,
	Magenta = 0x05,
	Cyan = 0x06,
	White = 0x07,
	Bold = 0x08,
	BgBlack = 0x00,
	BgRed = 0x10,
	BgGreen = 0x20,
	BgYellow = 0x30,
	BgBlue = 0x40,
	BgMagenta = 0x50,
	BgCyan = 0x60,
	BgWhite = 0x70
    }

    // Alternate character set constants. These constants are used only
    // for AddACS().
    enum TerminalACS
    {
	LeftArrow = ',',
	RightArrow = '+'
    }

    // Low-level terminal IO. All functions are P/Invoke wrappers for
    // the terminal driver.
    class Terminal
    {
	[DllImport("libterminal.so")]
	public static extern void Init();

	[DllImport("libterminal.so")]
	public static extern void Erase();

	[DllImport("libterminal.so")]
	public static extern void Clear();

	[DllImport("libterminal.so")]
	public static extern void Refresh();

	[DllImport("libterminal.so")]
	public static extern void Exit();

	[DllImport("libterminal.so")]
	public static extern TerminalKey Getch();

	[DllImport("libterminal.so")]
	public static extern void GetSize(out int r, out int c);

	[DllImport("libterminal.so")]
	public static extern void Goto(int r, int c);

	[DllImport("libterminal.so")]
	public static extern void SetColor(TerminalColor color);

	[DllImport("libterminal.so")]
	public static extern void AddChar(int ch, int count);

	[DllImport("libterminal.so")]
	public static extern void AddACS(TerminalACS ch, int count);

	[DllImport("libterminal.so")]
	public static extern void AddString(string text);
    }
}
