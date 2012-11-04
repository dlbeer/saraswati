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

namespace Saraswati.Core
{
    // EPub content path manipulation
    class EPath
    {
	public static string GetFragment(string link)
	{
	    int f = link.IndexOf('#');

	    if (f < 0)
		return null;

	    return link.Substring(f + 1, link.Length - f - 1);
	}

	public static string GetPath(string link)
	{
	    int f = link.IndexOf('#');

	    if (f < 0)
		return link;

	    return link.Substring(0, f);
	}

	public static bool IsAbsolute(string path)
	{
	    return (path.IndexOf("://") >= 0);
	}

	public static string RelPath(string basePath, string relPath)
	{
	    if (IsAbsolute(relPath))
		return relPath;

	    var stk = new Stack<string>(basePath.Split(new char[]{'/'}));

	    if (stk.Count > 0)
		stk.Pop();

	    foreach (string part in relPath.Split(new char[]{'/'}))
	    {
		if (part == "..")
		    stk.Pop();
		else if (part != ".")
		    stk.Push(part);
	    }

	    var rev = new Stack<string>(stk);

	    return string.Join("/", rev);
	}
    }
}
