# Resin
Resin is a document database and a search engine with querying support for term, fuzzy, prefix, phrase and range. Analyzers, tokenizers and scoring schemes are customizable.

## A smarter index
Resin's index is a disk-based left-child-right-sibling character trie. Resin indices are fast to write to and read from and support near (as in "almost match") and prefix.

Apart from offering fast lookups Resin also scores documents based on their relevance. Relevance in turn is based on the distance from a document and a query in [vector space](https://en.wikipedia.org/wiki/Vector_space_model).

## Supported .net version
Resin is built for dotnet Core 1.1.

## Download
Clone the source or [download the latest source as a zip file](https://github.com/kreeben/resin/archive/master.zip), build and run the CLI or look at the code in the CLI Program.cs to see how querying and writing was implemented.

## Demo
[searchpanels.com](http://searchpanels.com)  

## Help out?
Awesome! Start [here](https://github.com/kreeben/resin/issues).

## Documentation
### A document (serialized).

	{
		"id": "Q1",
		"label":  "universe",
		"description": "totality of planets, stars, galaxies, intergalactic space, or all matter or all energy",
		"aliases": "cosmos The Universe existence space outerspace"
	}

_Download Wikipedia as JSON [here](https://dumps.wikimedia.org/wikidatawiki/entities/)._

### Store many of those on disk (compression is optional).

	var docs = GetWikipedia();
	var dir = @"C:\wikipedia";
	
	using (var documents = new InMemoryDocumentSource(docs))
	{
		new UpsertOperation(dir, new Analyzer(), Compression.Lz, "id", documents)
		    .Write();
	}
	
### Store JSON documents encoded in a stream

	using (var documents = new JsonDocumentStream(fileName, skip, take))
	{
		new UpsertOperation(dir, new Analyzer(), Compression.NoCompression, "id", documents)
		    .Write();
	}

	// Implement the base class DocumentSource to use whatever source you need.

It's perfectly fine to mix compressed and non-compressed batches inside a document store (directory).
	
### Query the index.
<a name="inproc" id="inproc"></a>

	var result = new Searcher(dir).Search("label:good bad~ description:leone", page:0, size:15);

	// Document fields and scores, i.e. the aggregated tf-idf weights a document recieve from a simple 
	// or compound query, are included in the result:

	var scoreOfFirstDoc = result.Docs[0].Fields["__score"];
	var label = result.Docs[0].Fields["label"];

[More documentation here](https://github.com/kreeben/resin/wiki). 
