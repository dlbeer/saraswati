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
using ICSharpCode.SharpZipLib.Zip;

namespace Saraswati.Core
{
    class EPubFile : IDisposable
    {
	Stream stream;
	ZipFile zip;

	public readonly OPFManifest Manifest;
	public readonly OPFSpine Spine;
	public readonly TableOfContents Toc;

	public EPubFile(string filename) :
	    this(File.Open(filename, FileMode.Open)) { }

	public EPubFile(Stream s)
	{
	    try
	    {
		zip = new ZipFile(s);

		string mpath = getRootFromContainer
		    (GetContent("META-INF/container.xml"));
		OPFParser.ParseStream(GetContent(mpath), mpath,
				      out Manifest, out Spine);

		if (Spine.TocId == null)
		{
		    Toc = new TableOfContents();
		}
		else
		{
		    string tocPath = Manifest.GetById(Spine.TocId).Linkref;

		    Toc = NCXParser.ParseStream(GetContent(tocPath), tocPath);
		}

		stream = s;
	    }
	    catch (Exception)
	    {
		s.Dispose();
		throw;
	    }
	}

	public Stream GetContent(string path)
	{
	    try {
		ZipEntry ent = zip.GetEntry(path);

		return zip.GetInputStream(ent);
	    }
	    catch (Exception ex)
	    {
		throw new Exception(
		    string.Format("Can't obtain part {0}: {1}", path, ex));
	    }
	}

	public void ProduceDocument(string path, IDocumentConsumer con)
	{
	    path = EPath.GetPath(path);

	    con = new NormalizeWhitespace(con);
	    con = new ShiftLinks(path, con);

	    using (Stream s = GetContent(path))
		XHTMLParser.ParseStream(s, con);
	}

	public void ProduceBook(IDocumentConsumer con)
	{
	    for (int i = 0; i < Spine.Count; i++)
	    {
		OPFManifest.Item item = Manifest.GetById(Spine[i]);
		IDocumentConsumer filter =
		    new NamespaceAnchors(item.Linkref + "#", con);

		con.PushFragment(new Fragment() {
		    Attr = Fragment.Attributes.AnchorName,
		    Text = item.Linkref
		});

		ProduceDocument(item.Linkref, filter);
	    }
	}

	public void Dispose()
	{
	    stream.Dispose();
	}

	string getRootFromContainer(Stream s)
	{
	    var r = new XmlTextReader(s);

	    r.XmlResolver = null;

	    while (r.Read())
		if ((r.NodeType == XmlNodeType.Element) &&
		    (r.Name.ToLower() == "rootfile"))
		    return r.GetAttribute("full-path");

	    return null;
	}
    }
}
