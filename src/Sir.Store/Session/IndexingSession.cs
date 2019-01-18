﻿using Sir.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Indexing session targeting a single collection.
    /// </summary>
    public class IndexingSession : CollectionSession, IDisposable, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly ITokenizer _tokenizer;
        private bool _validate;
        private readonly IDictionary<long, VectorNode> _dirty;
        private readonly Stream _vectorStream;
        private bool _flushed;
        private bool _flushing;
        private readonly ProducerConsumerQueue<(ulong docId, long keyId, AnalyzedString tokens)> _modelBuilder;
        private readonly RemotePostingsWriter _postingsWriter;

        public IndexingSession(
            string collectionId, 
            SessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationProvider config) : base(collectionId, sessionFactory)
        {
            _config = config;
            _tokenizer = tokenizer;
            _validate = config.Get("create_index_validation_files") == "true";
            _dirty = new ConcurrentDictionary<long, VectorNode>();
            _vectorStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, string.Format("{0}.vec", CollectionId.ToHash())));
            _postingsWriter = new RemotePostingsWriter(_config);

            var numThreads = int.Parse(_config.Get("index_thread_count"));

            _modelBuilder = new ProducerConsumerQueue<(ulong docId, long keyId, AnalyzedString tokens)>(AddDocumentToModel, numThreads);
        }

        public void Write(IDictionary document)
        {
            Analyze(document);
        }

        public void Flush()
        {
            if (_flushing || _flushed)
                return;

            _flushing = true;

            this.Log("waiting for model builder");

            using (_modelBuilder)
            {
                _modelBuilder.Join();
            }

            var tasks = new Task[_dirty.Count];
            var taskId = 0;

            foreach (var column in _dirty)
            {
                tasks[taskId++] = SerializeColumn(column.Key, column.Value);
            }

            using (_vectorStream)
            {
                _vectorStream.Flush();
                _vectorStream.Close();
            }

            Task.WaitAll(tasks);

            _flushed = true;
            _flushing = false;

            this.Log(string.Format("***FLUSHED***"));
        }

        private static readonly object _indexFileSync = new object();


        private async Task SerializeColumn(long keyId, VectorNode column)
        {
            var time = Stopwatch.StartNew();

            (int depth, int width, int avgDepth) size;

            var collectionId = CollectionId.ToHash();

            await _postingsWriter.Write(collectionId, column);

            var pixFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ixp", collectionId, keyId));
            var ixFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", collectionId, keyId));

            lock (_indexFileSync)
            {
                using (var pageIndexWriter = new PageIndexWriter(SessionFactory.CreateAppendStream(pixFileName)))
                using (var ixStream = SessionFactory.CreateAppendStream(ixFileName))
                {
                    var page = column.SerializeTree(ixStream);

                    pageIndexWriter.Write(page.offset, page.length);

                    size = column.Size();
                }
            }

            this.Log("serialized column {0} in {1} with size {2},{3} (avg depth {4})",
                keyId, time.Elapsed, size.depth, size.width, size.avgDepth);
        }

        private void Analyze(IDictionary document)
        {
            var docId = (ulong)document["__docid"];

            foreach (var obj in document.Keys)
            {
                var key = (string)obj;
                AnalyzedString tokens = null;

                if (!key.StartsWith("__"))
                {
                    var keyHash = key.ToHash();
                    var keyId = SessionFactory.GetKeyId(keyHash);
                    var val = (IComparable)document[key];
                    var str = val as string;

                    if (str == null || key[0] == '_')
                    {
                        var v = val.ToString();

                        if (!string.IsNullOrWhiteSpace(v))
                        {
                            tokens = new AnalyzedString { Source = v.ToCharArray(), Tokens = new List<(int, int)> { (0, v.Length) } };
                        }
                    }
                    else
                    {
                        tokens = _tokenizer.Tokenize(str);
                    }

                    if (tokens != null)
                    {
                        _modelBuilder.Enqueue((docId, keyId, tokens));
                    }
                }
            }
        }

        private void AddDocumentToModel((ulong docId, long keyId, AnalyzedString tokens) item)
        {
            var time = Stopwatch.StartNew();

            var ix = GetOrCreateIndex(item.keyId);

            foreach (var token in item.tokens.Tokens)
            {
                var termVector = item.tokens.ToCharVector(token.offset, token.length);

                ix.Add(new VectorNode(termVector, item.docId), _vectorStream);
            }

            this.Log("added document ID {0} key {1} to model in {2}", item.docId, item.keyId, time.Elapsed);
        }

        private static readonly object _syncIndexAccess = new object();

        private VectorNode GetOrCreateIndex(long keyId)
        {
            VectorNode root;

            if (!_dirty.TryGetValue(keyId, out root))
            {
                lock (_syncIndexAccess)
                {
                    if (!_dirty.TryGetValue(keyId, out root))
                    {
                        root = new VectorNode();
                        _dirty.Add(keyId, root);
                    }
                }
            }

            return root;
        }

        public void Dispose()
        {
            Flush();
        }
    }
}