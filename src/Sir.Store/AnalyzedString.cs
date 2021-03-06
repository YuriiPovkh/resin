﻿using System.Collections.Generic;

namespace Sir
{
    /// <summary>
    /// An analyzed (tokenized) string.
    /// </summary>
    public class AnalyzedString
    {
        public IList<(int offset, int length)> Tokens { get; private set; }
        public IList<SortedList<long, int>> Embeddings { get; private set; }
        public string Original { get; private set; }

        public AnalyzedString(IList<(int offset, int length)> tokens, IList<SortedList<long, int>> embeddings, string original)
        {
            Tokens = tokens;
            Embeddings = embeddings;
            Original = original;
        }

        public override string ToString()
        {
            return Original;
        }
    }
}