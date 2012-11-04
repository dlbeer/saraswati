# Saraswati -- command-line EPUB reader
# Copyright (C) 2012 Daniel Beer <dlbeer@gmail.com>
#
# Permission to use, copy, modify, and/or distribute this software for
# any purpose with or without fee is hereby granted, provided that the
# above copyright notice and this permission notice appear in all
# copies.
#
# THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL
# WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED
# WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE
# AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL
# DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR
# PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER
# TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR
# PERFORMANCE OF THIS SOFTWARE.

CSC ?= dmcs
TARGETS = saraswati.exe libterminal.so
SOURCE = $(wildcard Core/*.cs UI/*.cs)
DESTDIR ?=
PREFIX ?= /usr/local
LIBDIR = $(PREFIX)/lib/saraswati

all: $(TARGETS)

saraswati.exe: $(SOURCE) help.html
	$(CSC) -r:ICSharpCode.SharpZipLib -resource:help.html \
	    -out:$@ $(SOURCE)

libterminal.so: terminal.c
	$(CC) -O1 -Wall -fPIC -shared -Wl,-soname,$@ -o $@ $^ -lncursesw

clean:
	rm -f $(TARGETS)

install: $(TARGETS)
	mkdir -p $(DESTDIR)$(LIBDIR)
	install -o root -g root saraswati.exe $(DESTDIR)$(LIBDIR)
	install -o root -g root libterminal.so $(DESTDIR)$(LIBDIR)
	ln -sf $(LIBDIR)/saraswati.exe $(DESTDIR)$(PREFIX)/bin/saraswati

uninstall:
	rm -f $(DESTDIR)$(PREFIX)/bin/saraswati
	rm -f $(DESTDIR)$(LIBDIR)/saraswati.exe
	rm -f $(DESTDIR)$(LIBDIR)/libterminal.so
	rmdir $(DESTDIR)$(LIBDIR) || true