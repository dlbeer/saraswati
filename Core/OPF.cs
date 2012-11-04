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
using System.Xml;
using System.Collections.Generic;
using System.Net.Mime;

namespace Saraswati.Core
{
    // The OPF manifest, typicalled called content.obf, contains a list
    // of all media files, mime types and IDs. It also specifies the ID
    // of a table of contents.
    //
    // Link references are relative to the manifest path.
    class OPFManifest
    {
	public class Item
	{
	    public readonly string Id;
	    public readonly string Linkref;
	    public readonly ContentType MediaType;

	    public Item(string i, string l, ContentType m)
	    {
		Id = i;
		Linkref = l;
		MediaType = m;
	    }
	}

	Dictionary<string, Item> byId = new Dictionary<string, Item>();
	Dictionary<string, Item> byLinkref = new Dictionary<string, Item>();
	List<Item> allItems = new List<Item>();

	public OPFManifest() { }

	public int Count
	{
	    get { return allItems.Count; }
	}

	public void Add(Item it)
	{
	    byId[it.Id] = it;
	    byLinkref[it.Linkref] = it;
	    allItems.Add(it);
	}

	public IEnumerator<Item> AllItems()
	{
	    foreach (Item it in allItems)
		yield return it;
	}

	public Item GetById(string id)
	{
	    Item ret;

	    if (byId.TryGetValue(id, out ret))
		return ret;

	    return null;
	}

	public Item GetByLinkref(string lref)
	{
	    Item ret;

	    if (byLinkref.TryGetValue(lref, out ret))
		return ret;

	    return null;
	}
    }

    // The OPF spine contains a list of text files contained in the
    // EPUB, in a linear order.
    class OPFSpine
    {
	string tocId;
	List<string> posToId = new List<string>();
	Dictionary<string, int> idToPos = new Dictionary<string, int>();

	public OPFSpine() { }

	public string TocId
	{
	    get { return tocId; }
	    set { tocId = value; }
	}

	public int Count
	{
	    get { return posToId.Count; }
	}

	public string this[int pos]
	{
	    get { return posToId[pos]; }
	}

	public int FindId(string id)
	{
	    int res;

	    if (idToPos.TryGetValue(id, out res))
		return res;

	    return -1;
	}

	public int Add(string id)
	{
	    int pos = posToId.Count;

	    idToPos[id] = pos;
	    posToId.Add(id);

	    return pos;
	}
    }

    // Parser for OPF files. A utility method is provided which takes a
    // stream and returns a parsed table of contents object.
    class OPFParser
    {
	OPFManifest manf;
	OPFSpine spine;
	string basePath;

	int depSpine = 0;

	public OPFParser(OPFManifest m, OPFSpine s, string b)
	{
	    manf = m;
	    spine = s;
	    basePath = b;
	}

	void Begin(string text, XmlReader r)
	{
	    if (text.Equals("spine"))
	    {
		spine.TocId = r.GetAttribute("toc");
		depSpine++;
	    }
	    else if (text.Equals("item"))
		manf.Add(new OPFManifest.Item(
		    r.GetAttribute("id"),
		    EPath.RelPath(basePath, r.GetAttribute("href")),
		    new ContentType(r.GetAttribute("media-type"))));
	    else if (text.Equals("itemref") && (depSpine > 0))
		spine.Add(r.GetAttribute("idref"));
	}

	void End(string text)
	{
	    if (text.Equals("spine"))
		depSpine--;
	}

	public void Parse(Stream s)
	{
	    var r = new XmlTextReader(s);

	    r.XmlResolver = null;

	    while (r.Read())
		switch (r.NodeType)
		{
		case XmlNodeType.Element:
		    Begin(r.Name.ToLower(), r);
		    break;

		case XmlNodeType.EndElement:
		    End(r.Name.ToLower());
		    break;
		}
	}

	public static void ParseStream(Stream s, string basePath,
				       out OPFManifest mret,
				       out OPFSpine spret)
	{
	    var obf = new OPFManifest();
	    var sp = new OPFSpine();
	    var parse = new OPFParser(obf, sp, basePath);

	    parse.Parse(s);
	    mret = obf;
	    spret = sp;
	}
    }
}
