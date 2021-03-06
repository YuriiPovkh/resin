﻿using System.IO;

namespace Sir.Store
{
    public abstract class DocumentSession : KeyValueSession
    {
        protected Stream DocStream { get; set; }
        protected Stream DocIndexStream { get; set; }

        protected DocumentSession(string collectionName, ulong collectionId, SessionFactory sessionFactory) 
            : base(collectionName, collectionId, sessionFactory)
        {
        }

        public override void Dispose()
        {
            base.Dispose();

            if (DocStream != null) DocStream.Dispose();
            if (DocIndexStream != null) DocIndexStream.Dispose();
        }
    }
}