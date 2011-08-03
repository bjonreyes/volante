namespace Volante
{
    using System;
    using System.Diagnostics;

    public class TestIndex
    {
        public class Record : Persistent
        {
            public string strKey;
            public long intKey;
        }

        public class Root : Persistent
        {
            public Index<string, Record> strIndex;
            public Index<long, Record> intIndex;
        }

        internal static int pagePoolSize = 32 * 1024 * 1024;

        static public void Run(int nRecords, bool altBtree, bool inmemory, bool serializableTransaction)
        {
            int i;
            string dbName = "testidx.dbs";
            Tests.SafeDeleteFile(dbName);

            Storage db = StorageFactory.CreateStorage();
            if (altBtree || serializableTransaction)
            {
                db.AlternativeBtree = true;
            }
            if (inmemory)
            {
                pagePoolSize = 0;
            }
            db.Open(dbName, pagePoolSize);

            if (serializableTransaction)
            {
                db.BeginThreadTransaction(TransactionMode.Serializable);
            }

            Root root = (Root)db.Root;
            if (root == null)
            {
                root = new Root();
                root.strIndex = db.CreateIndex<string, Record>(true);
                root.intIndex = db.CreateIndex<long, Record>(true);
                db.Root = root;
            }
            Index<string, Record> strIndex = root.strIndex;
            Index<long, Record> intIndex = root.intIndex;
            DateTime start = DateTime.Now;
            long key = 1999;
            for (i = 0; i < nRecords; i++)
            {
                Record rec = new Record();
                key = (3141592621L * key + 2718281829L) % 1000000007L;
                rec.intKey = key;
                rec.strKey = System.Convert.ToString(key);
                intIndex[rec.intKey] = rec;
                strIndex[rec.strKey] = rec;
                if (i % 100000 == 0)
                {
                    db.Commit();
                    Console.Write("Iteration " + i + "\r");
                }
            }

            if (serializableTransaction)
            {
                db.EndThreadTransaction();
                db.BeginThreadTransaction(TransactionMode.Serializable);
            }
            else
            {
                db.Commit();
            }
            System.Console.WriteLine("Elapsed time for inserting " + nRecords + " records: " + (DateTime.Now - start));

            start = System.DateTime.Now;
            key = 1999;
            for (i = 0; i < nRecords; i++)
            {
                key = (3141592621L * key + 2718281829L) % 1000000007L;
                Record rec1 = intIndex[key];
                Record rec2 = strIndex[Convert.ToString(key)];
                Debug.Assert(rec1 != null && rec1 == rec2);
            }
            System.Console.WriteLine("Elapsed time for performing " + nRecords * 2 + " index searches: " + (DateTime.Now - start));

            start = System.DateTime.Now;
            key = Int64.MinValue;
            i = 0;
            foreach (Record rec in intIndex)
            {
                Debug.Assert(rec.intKey >= key);
                key = rec.intKey;
                i += 1;
            }
            Debug.Assert(i == nRecords);
            i = 0;
            String strKey = "";
            foreach (Record rec in strIndex)
            {
                Debug.Assert(rec.strKey.CompareTo(strKey) >= 0);
                strKey = rec.strKey;
                i += 1;
            }
            Debug.Assert(i == nRecords);
            System.Console.WriteLine("Elapsed time for iteration through " + (nRecords * 2) + " records: " + (DateTime.Now - start));

            Console.WriteLine("Memory usage");
            start = DateTime.Now;
            foreach (MemoryUsage usage in db.GetMemoryDump().Values)
            {
                Console.WriteLine(" " + usage.type.Name + ": instances=" + usage.nInstances + ", total size=" + usage.totalSize + ", allocated size=" + usage.allocatedSize);
            }
            Console.WriteLine("Elapsed time for memory dump: " + (DateTime.Now - start));

            start = System.DateTime.Now;
            key = 1999;
            for (i = 0; i < nRecords; i++)
            {
                key = (3141592621L * key + 2718281829L) % 1000000007L;
                Record rec = intIndex.Get(key);
                Record removed = intIndex.RemoveKey(key);
                Debug.Assert(removed == rec);
                strIndex.Remove(new Key(System.Convert.ToString(key)), rec);
                rec.Deallocate();
            }
            System.Console.WriteLine("Elapsed time for deleting " + nRecords + " records: " + (DateTime.Now - start));
            db.Close();
        }
    }


}