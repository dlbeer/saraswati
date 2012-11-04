/* Saraswati -- command-line EPUB reader
 * Copyright (C) 2012 Daniel Beer <dlbeer@gmail.com>
 *
 * Permission to use, copy, modify, and/or distribute this software for
 * any purpose with or without fee is hereby granted, provided that the
 * above copyright notice and this permission notice appear in all
 * copies.
 *
 * THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL
 * WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE
 * AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL
 * DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR
 * PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER
 * TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR
 * PERFORMANCE OF THIS SOFTWARE.
 */

#include <ncurses.h>
#include <signal.h>

void Init(void)
{
	int i;

	initscr();
	cbreak();
	noecho();
	keypad(stdscr, TRUE);
	start_color();

	for (i = 0; i < 64; i++)
	    init_pair(i + 1, i & 7, i >> 3);

	signal(SIGINT, SIG_IGN);
}

void Erase(void)
{
	erase();
}

void Clear(void)
{
	clear();
}

void Refresh(void)
{
	refresh();
}

void Exit(void)
{
	endwin();
}

int Getch(void)
{
	return getch();
}

void GetSize(int *r, int *c)
{
	*r = LINES;
	*c = COLS;
}

void Goto(int r, int c)
{
	move(r, c);
}

void SetColor(int color)
{
	const int is_bold = color & 8;
	const int fg = color & 7;
	const int bg = (color >> 4) & 7;
	const int pair = ((bg << 3) | fg) + 1;

	attrset((is_bold ? A_BOLD : 0) | COLOR_PAIR(pair));
}

void AddChar(int ch, int count)
{
	while (count > 0) {
		addch(ch);
		count--;
	}
}

void AddACS(int ch, int count)
{
	AddChar(NCURSES_ACS(ch), count);
}

void AddString(const char *text)
{
	addstr(text);
}
