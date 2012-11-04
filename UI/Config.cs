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
using System.Xml;
using System.IO;

namespace Saraswati.UI
{
    class Config
    {
	const int MaxHistory = 64;
	string folder;
	string path;

	public LRUSet<string> searchHistory =
		new LRUSet<string>(MaxHistory);
	public LRUDictionary<string, int> bookmarks =
		new LRUDictionary<string, int>(MaxHistory);

	public bool HalfWidth = false;

	public Config()
	{
	    folder = Environment.GetFolderPath
		(Environment.SpecialFolder.ApplicationData);
	    path = Path.Combine(folder, "saraswati.xml");
	}

	public LRUSet<string> SearchHistory
	{
	    get { return searchHistory; }
	}

	public LRUDictionary<string, int> Bookmarks
	{
	    get { return bookmarks; }
	}

	public void Load()
	{
	    try
	    {
		doLoad();
	    }
	    catch { }
	}

	void doLoad()
	{
	    using (XmlReader r = XmlReader.Create(path))
		while (r.Read())
		{
		    if (r.IsStartElement("SearchHistory"))
			loadHistory(r, searchHistory);
		    else if (r.IsStartElement("HalfWidth"))
			HalfWidth = r.ReadElementContentAsBoolean();
		    else if (r.IsStartElement("Bookmarks"))
			loadBookmarks(r, bookmarks);
		}
	}

	static void loadBookmarks(XmlReader r,
				  LRUDictionary<string, int> bookmarks)
	{
	    bookmarks.Clear();

	    using (XmlReader sub = r.ReadSubtree())
		while (sub.Read())
		    if (sub.IsStartElement("item"))
		    {
			string key = sub.GetAttribute("file");
			int pos = sub.ReadElementContentAsInt();

			bookmarks.Put(key, pos);
		    }
	}

	static void loadHistory(XmlReader r, LRUSet<string> history)
	{
	    history.Clear();

	    using (XmlReader sub = r.ReadSubtree())
		while (sub.Read())
		    if (sub.IsStartElement("item"))
			history.Add(r.ReadString());
	}

	public void Save()
	{
	    var set = new XmlWriterSettings();

	    set.Indent = true;
	    Directory.CreateDirectory(folder);

	    using (XmlWriter w = XmlWriter.Create(path, set))
	    {
		w.WriteStartDocument();
		w.WriteStartElement("Saraswati");

		w.WriteStartElement("HalfWidth");
		w.WriteValue(HalfWidth);
		w.WriteEndElement();

		w.WriteStartElement("SearchHistory");
		foreach (string item in searchHistory)
		    w.WriteElementString("item", item);
		w.WriteEndElement();

		w.WriteStartElement("Bookmarks");
		foreach (LRUDictionary<string, int>.Pair p in bookmarks)
		{
		    w.WriteStartElement("item");
		    w.WriteAttributeString("file", p.Key);
		    w.WriteValue(p.Value);
		    w.WriteEndElement();
		}
		w.WriteEndElement();

		w.WriteEndElement();
		w.WriteEndDocument();
	    }
	}
    }
}
