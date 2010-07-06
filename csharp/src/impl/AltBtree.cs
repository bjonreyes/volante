namespace Perst.Impl
{
    using System;
    using System.Collections;
    using System.Diagnostics;
    using Perst;
#if USE_GENERICS
    using System.Collections.Generic;
    using Link=Link<IPersistent>;
#endif

    
#if USE_GENERICS
    class AltBtree<K,V>:PersistentCollection<V>, Index<K,V> where V:class,IPersistent
#else
    class AltBtree:PersistentCollection, Index
#endif
    {
        public Type KeyType
        {
            get
            {
#if USE_GENERICS
                return typeof(K);
#else
                switch (type)
                {                   
                    case ClassDescriptor.FieldType.tpBoolean: 
                        return typeof(bool);
                    
                    case ClassDescriptor.FieldType.tpByte: 
                        return typeof(byte);
                    
                    case ClassDescriptor.FieldType.tpSByte: 
                        return typeof(sbyte);
                    
                    case ClassDescriptor.FieldType.tpChar: 
                        return typeof(char);
                    
                    case ClassDescriptor.FieldType.tpShort: 
                        return typeof(short);
                    
                    case ClassDescriptor.FieldType.tpUShort: 
                        return typeof(ushort);
                    
                    case ClassDescriptor.FieldType.tpInt: 
                    case ClassDescriptor.FieldType.tpOid: 
                        return typeof(int);
                    
                    case ClassDescriptor.FieldType.tpUInt: 
                        return typeof(uint);
                    
                    case ClassDescriptor.FieldType.tpLong: 
                        return typeof(long);
                    
                    case ClassDescriptor.FieldType.tpULong: 
                        return typeof(ulong);
                    
                    case ClassDescriptor.FieldType.tpFloat: 
                        return typeof(float);
                    
                    case ClassDescriptor.FieldType.tpDouble: 
                        return typeof(double);
                    
                    case ClassDescriptor.FieldType.tpString: 
                        return typeof(string);
                    
                    case ClassDescriptor.FieldType.tpDate: 
                        return typeof(DateTime);
                    
                    case ClassDescriptor.FieldType.tpObject: 
                        return typeof(IPersistent);
                    
                    case ClassDescriptor.FieldType.tpRaw: 
                        return typeof(IComparable);
                    
                    case ClassDescriptor.FieldType.tpGuid: 
                        return typeof(Guid);
                    
                    case ClassDescriptor.FieldType.tpDecimal: 
                        return typeof(decimal);
                    
                    case ClassDescriptor.FieldType.tpEnum: 
                        return typeof(Enum);
                    
                    default: 
                        return null;
                    
                }
#endif
            }
            
        }
        internal int height;
        internal ClassDescriptor.FieldType type;
        internal int nElems;
        internal bool unique;
        internal BtreePage root;
        
        [NonSerialized()]
        internal int updateCounter;
        
        internal AltBtree()
        {
        }
        
#if USE_GENERICS
        public override void OnLoad()
        {
             if (type != ClassDescriptor.getTypeCode(typeof(K))) {
                throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_KEY_TYPE, typeof(K));
            }
        }
#endif

        internal class BtreeKey
        {
            internal Key key;
            internal IPersistent node;
            internal IPersistent oldNode;
            
            internal BtreeKey(Key key, IPersistent node)
            {
                this.key = key;
                this.node = node;
            }
        }
        
        internal abstract class BtreePage:Persistent
        {
            internal abstract Array Data{get;}
            internal int nItems;
            internal Link items;
       
            internal const int BTREE_PAGE_SIZE = Page.pageSize - ObjectHeader.Sizeof - 4 * 3;
            
            internal abstract object getKeyValue(int i);
            internal abstract Key getKey(int i);
            internal abstract int compare(Key key, int i);
            internal abstract void  insert(BtreeKey key, int i);
            internal abstract BtreePage clonePage();
            
            internal virtual void  clearKeyValue(int i)
            {
            }
            
            internal virtual bool find(Key firstKey, Key lastKey, int height, ArrayList result)
            {
                int l = 0, n = nItems, r = n;
                height -= 1;
                if (firstKey != null)
                {
                    while (l < r)
                    {
                        int i = (l + r) >> 1;
                        if (compare(firstKey, i) >= firstKey.inclusion)
                        {
                            l = i + 1;
                        }
                        else
                        {
                            r = i;
                        }
                    }
                    Debug.Assert(r == l);
                }
                if (lastKey != null)
                {
                    if (height == 0)
                    {
                        while (l < n)
                        {
                            if (-compare(lastKey, l) >= lastKey.inclusion)
                            {
                                return false;
                            }
                            result.Add(items[l]);
                            l += 1;
                        }
                        return true;
                    }
                    else
                    {
                        do 
                        {
                            if (!((BtreePage) items[l]).find(firstKey, lastKey, height, result))
                            {
                                return false;
                            }
                            if (l == n)
                            {
                                return true;
                            }
                        }
                        while (compare(lastKey, l++) >= 0);
                        return false;
                    }
                }
                if (height == 0)
                {
                    while (l < n)
                    {
                        result.Add(items[l]);
                        l += 1;
                    }
                }
                else
                {
                    do 
                    {
                        if (!((BtreePage) items[l]).find(firstKey, lastKey, height, result))
                        {
                            return false;
                        }
                    }
                    while (++l <= n);
                }
                return true;
            }
            
            internal static void  memcpyData(BtreePage dst_pg, int dst_idx, BtreePage src_pg, int src_idx, int len)
            {
                Array.Copy(src_pg.Data, src_idx, dst_pg.Data, dst_idx, len);
            }
            
            internal static void  memcpyItems(BtreePage dst_pg, int dst_idx, BtreePage src_pg, int src_idx, int len)
            {
                Array.Copy(src_pg.items.ToRawArray(), src_idx, dst_pg.items.ToRawArray(), dst_idx, len);
            }
            
            internal static void  memcpy(BtreePage dst_pg, int dst_idx, BtreePage src_pg, int src_idx, int len)
            {
                memcpyData(dst_pg, dst_idx, src_pg, src_idx, len);
                memcpyItems(dst_pg, dst_idx, src_pg, src_idx, len);
            }
            
            internal virtual void  memset(int i, int len)
            {
                while (--len >= 0)
                {
                    items[i++] = null;
                }
            }
            
            internal virtual OperationResult insert(BtreeKey ins, int height, bool unique, bool overwrite)
            {
                OperationResult result;
                int l = 0, n = nItems, r = n;
                while (l < r)
                {
                    int i = (l + r) >> 1;
                    if (compare(ins.key, i) > 0)
                    {
                        l = i + 1;
                    }
                    else
                    {
                        r = i;
                    }
                }
                Debug.Assert(l == r);
                /* insert before e[r] */
                if (--height != 0)
                {
                    result = ((BtreePage) items[r]).insert(ins, height, unique, overwrite);
                    Debug.Assert(result != OperationResult.NotFound);
                    if (result != OperationResult.Overflow)
                    {
                        return result;
                    }
                    n += 1;
                }
                else if (r < n && compare(ins.key, r) == 0)
                {
                    if (overwrite)
                    {
                        ins.oldNode = items[r];
                        Modify();
                        items[r] = ins.node;
                        return OperationResult.Overwrite;
                    }
                    else if (unique)
                    {
                        ins.oldNode = items[r];
                        return OperationResult.Duplicate;
                    }
                }
                int max = items.Length;
                Modify();
                if (n < max)
                {
                    memcpy(this, r + 1, this, r, n - r);
                    insert(ins, r);
                    nItems += 1;
                    return OperationResult.Done;
                }
                else
                {
                    /* page is full then divide page */
                    BtreePage b = clonePage();
                    Debug.Assert(n == max);
                    int m = max / 2;
                    if (r < m)
                    {
                        memcpy(b, 0, this, 0, r);
                        memcpy(b, r + 1, this, r, m - r - 1);
                        memcpy(this, 0, this, m - 1, max - m + 1);
                        b.insert(ins, r);
                    }
                    else
                    {
                        memcpy(b, 0, this, 0, m);
                        memcpy(this, 0, this, m, r - m);
                        memcpy(this, r - m + 1, this, r, max - r);
                        insert(ins, r - m);
                    }
                    memset(max - m + 1, m - 1);
                    ins.node = b;
                    ins.key = b.getKey(m - 1);
                    if (height == 0)
                    {
                        nItems = max - m + 1;
                        b.nItems = m;
                    }
                    else
                    {
                        b.clearKeyValue(m - 1);
                        nItems = max - m;
                        b.nItems = m - 1;
                    }
                    return OperationResult.Overflow;
                }
            }
            
            internal virtual OperationResult handlePageUnderflow(int r, BtreeKey rem, int height)
            {
                BtreePage a = (BtreePage) items[r];
                a.Modify();
                Modify();
                int an = a.nItems;
                if (r < nItems)
                {
                    // exists greater page
                    BtreePage b = (BtreePage) items[r + 1];
                    int bn = b.nItems;
                    Debug.Assert(bn >= an);
                    if (height != 1)
                    {
                        memcpyData(a, an, this, r, 1);
                        an += 1;
                        bn += 1;
                    }
                    if (an + bn > items.Length)
                    {
                        // reallocation of nodes between pages a and b
                        int i = bn - ((an + bn) >> 1);
                        b.Modify();
                        memcpy(a, an, b, 0, i);
                        memcpy(b, 0, b, i, bn - i);
                        memcpyData(this, r, a, an + i - 1, 1);
                        if (height != 1)
                        {
                            a.clearKeyValue(an + i - 1);
                        }
                        b.memset(bn - i, i);
                        b.nItems -= i;
                        a.nItems += i;
                        return OperationResult.Done;
                    }
                    else
                    {
                        // merge page b to a  
                        memcpy(a, an, b, 0, bn);
                        b.Deallocate();
                        memcpyData(this, r, this, r + 1, nItems - r - 1);
                        memcpyItems(this, r + 1, this, r + 2, nItems - r - 1);
                        items[nItems] = null;
                        a.nItems += bn;
                        nItems -= 1;
                        return nItems < (items.Size() >> 1) ? OperationResult.Underflow : OperationResult.Done;
                    }
                }
                else
                {
                    // page b is before a
                    BtreePage b = (BtreePage) items[r - 1];
                    int bn = b.nItems;
                    Debug.Assert(bn >= an);
                    if (height != 1)
                    {
                        an += 1;
                        bn += 1;
                    }
                    if (an + bn > items.Size())
                    {
                        // reallocation of nodes between pages a and b
                        int i = bn - ((an + bn) >> 1);
                        b.Modify();
                        memcpy(a, i, a, 0, an);
                        memcpy(a, 0, b, bn - i, i);
                        if (height != 1)
                        {
                            memcpyData(a, i - 1, this, r - 1, 1);
                        }
                        memcpyData(this, r - 1, b, bn - i - 1, 1);
                        if (height != 1)
                        {
                            b.clearKeyValue(bn - i - 1);
                        }
                        b.memset(bn - i, i);
                        b.nItems -= i;
                        a.nItems += i;
                        return OperationResult.Done;
                    }
                    else
                    {
                        // merge page b to a
                        memcpy(a, bn, a, 0, an);
                        memcpy(a, 0, b, 0, bn);
                        if (height != 1)
                        {
                            memcpyData(a, bn - 1, this, r - 1, 1);
                        }
                        b.Deallocate();
                        items[r - 1] = a;
                        items[nItems] = null;
                        a.nItems += bn;
                        nItems -= 1;
                        return nItems < (items.Size() >> 1) ? OperationResult.Underflow : OperationResult.Done;
                    }
                }
            }
            
            internal virtual OperationResult remove(BtreeKey rem, int height)
            {
                int i, n = nItems, l = 0, r = n;
                
                while (l < r)
                {
                    i = (l + r) >> 1;
                    if (compare(rem.key, i) > 0)
                    {
                        l = i + 1;
                    }
                    else
                    {
                        r = i;
                    }
                }
                if (--height == 0)
                {
                    IPersistent node = rem.node;
                    while (r < n)
                    {
                        if (compare(rem.key, r) == 0)
                        {
                            if (node == null || items.ContainsElement(r, node))
                            {
                                rem.oldNode = items[r];
                                Modify();
                                memcpy(this, r, this, r + 1, n - r - 1);
                                nItems = --n;
                                memset(n, 1);
                                return n < (items.Size() >> 1) ? OperationResult.Underflow : OperationResult.Done;
                            }
                        }
                        else
                        {
                            break;
                        }
                        r += 1;
                    }
                    return OperationResult.NotFound;
                }
                do 
                {
                    switch (((BtreePage) items[r]).remove(rem, height))
                    {                       
                        case OperationResult.Underflow: 
                            return handlePageUnderflow(r, rem, height);
                        
                        case OperationResult.Done: 
                            return OperationResult.Done;
                    }
                }
                while (++r <= n);
                
                return OperationResult.NotFound;
            }
            
            internal virtual void purge(int height)
            {
                if (--height != 0)
                {
                    int n = nItems;
                    do 
                    {
                        ((BtreePage) items[n]).purge(height);
                    }
                    while (--n >= 0);
                }
                Deallocate();
            }
            
            internal virtual int traverseForward(int height, IPersistent[] result, int pos)
            {
                int i, n = nItems;
                if (--height != 0)
                {
                    for (i = 0; i <= n; i++)
                    {
                        pos = ((BtreePage) items[i]).traverseForward(height, result, pos);
                    }
                }
                else
                {
                    for (i = 0; i < n; i++)
                    {
                        result[pos++] = items[i];
                    }
                }
                return pos;
            }
            
            internal BtreePage(Storage s, int n) : base(s)
            {
                items = s.CreateLink(n);
                items.Length = n;
            }
            
            internal BtreePage()
            {
            }
        }
        
        
        class BtreePageOfByte:BtreePage
        {
            override internal Array Data
            {
                get
                {
                    return data;
                }
                
            }
            
            protected byte[] data;
            
            const int MAX_ITEMS = BTREE_PAGE_SIZE / (4 + 1);
            
            
            internal override object getKeyValue(int i)
            {
                return data[i];
            }
            
            internal override Key getKey(int i)
            {
                return new Key(data[i]);
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfByte(Storage);
            }
            
            internal override int compare(Key key, int i)
            {
                return (byte) key.ival - data[i];
            }
            
            internal override void  insert(BtreeKey key, int i)
            {
                items[i] = key.node;
                data[i] = (byte) key.key.ival;
            }
            
            internal BtreePageOfByte(Storage s):base(s, MAX_ITEMS)
            {
                data = new byte[MAX_ITEMS];
            }
            
            internal BtreePageOfByte()
            {
            }
        }
        
        class BtreePageOfSByte:BtreePage
        {
            override internal Array Data
            {
                get
                {
                    return data;
                }
                
            }
            
            sbyte[] data;
            
            const int MAX_ITEMS = BTREE_PAGE_SIZE / (4 + 1);
            
            
            internal override object getKeyValue(int i)
            {
                return data[i];
            }
            
            internal override Key getKey(int i)
            {
                return new Key(data[i]);
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfSByte(Storage);
            }
            
            internal override int compare(Key key, int i)
            {
                return (sbyte) key.ival - data[i];
            }
            
            internal override void  insert(BtreeKey key, int i)
            {
                items[i] = key.node;
                data[i] = (sbyte) key.key.ival;
            }
            
            internal BtreePageOfSByte(Storage s):base(s, MAX_ITEMS)
            {
                data = new sbyte[MAX_ITEMS];
            }
            
            internal BtreePageOfSByte()
            {
            }
        }
        
        class BtreePageOfBoolean:BtreePageOfByte
        {
            internal override Key getKey(int i)
            {
                return new Key(data[i] != 0);
            }
            
            internal override object getKeyValue(int i)
            {
                return data[i] != 0;
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfBoolean(Storage);
            }
            
            internal BtreePageOfBoolean()
            {
            }
            
            internal BtreePageOfBoolean(Storage s):base(s)
            {
            }
        }
        
        class BtreePageOfShort:BtreePage
        {
            override internal Array Data
            {
                get
                {
                    return data;
                }
                
            }
            internal short[] data;
            
            const int MAX_ITEMS = BTREE_PAGE_SIZE / (4 + 2);
            
            
            internal override Key getKey(int i)
            {
                return new Key(data[i]);
            }
            
            internal override object getKeyValue(int i)
            {
                return data[i];
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfShort(Storage);
            }
            
            internal override int compare(Key key, int i)
            {
                return (short) key.ival - data[i];
            }
            
            internal override void  insert(BtreeKey key, int i)
            {
                items[i] = key.node;
                data[i] = (short) key.key.ival;
            }
            
            internal BtreePageOfShort(Storage s):base(s, MAX_ITEMS)
            {
                data = new short[MAX_ITEMS];
            }
            
            internal BtreePageOfShort()
            {
            }
        }
        
        class BtreePageOfUShort:BtreePage
        {
            override internal Array Data
            {
                get
                {
                    return data;
                }
                
            }
            internal ushort[] data;
            
            const int MAX_ITEMS = BTREE_PAGE_SIZE / (4 + 2);
            
            
            internal override Key getKey(int i)
            {
                return new Key(data[i]);
            }
            
            internal override object getKeyValue(int i)
            {
                return data[i];
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfUShort(Storage);
            }
            
            internal override int compare(Key key, int i)
            {
                return (ushort) key.ival - data[i];
            }
            
            internal override void  insert(BtreeKey key, int i)
            {
                items[i] = key.node;
                data[i] = (ushort) key.key.ival;
            }
            
            internal BtreePageOfUShort(Storage s):base(s, MAX_ITEMS)
            {
                data = new ushort[MAX_ITEMS];
            }
            
            internal BtreePageOfUShort()
            {
            }
        }
        
        class BtreePageOfInt:BtreePage
        {
            override internal Array Data
            {
                get
                {
                    return data;
                }
                
            }
            internal int[] data;
            
            const int MAX_ITEMS = BTREE_PAGE_SIZE / (4 + 4);
            
            
            internal override Key getKey(int i)
            {
                return new Key(data[i]);
            }
            
            internal override object getKeyValue(int i)
            {
                return data[i];
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfInt(Storage);
            }
            
            internal override int compare(Key key, int i)
            {
                return key.ival - data[i];
            }
            
            internal override void  insert(BtreeKey key, int i)
            {
                items[i] = key.node;
                data[i] = key.key.ival;
            }
            
            internal BtreePageOfInt(Storage s):base(s, MAX_ITEMS)
            {
                data = new int[MAX_ITEMS];
            }
            
            internal BtreePageOfInt()
            {
            }
        }
        
        class BtreePageOfUInt:BtreePage
        {
            override internal Array Data
            {
                get
                {
                    return data;
                }
                
            }
            internal uint[] data;
            
            const int MAX_ITEMS = BTREE_PAGE_SIZE / (4 + 4);
            
            
            internal override Key getKey(int i)
            {
                return new Key(data[i]);
            }
            
            internal override object getKeyValue(int i)
            {
                return data[i];
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfUInt(Storage);
            }
            
            internal override int compare(Key key, int i)
            {
                return (uint) key.ival < data[i] ? -1 : (uint) key.ival == data[i] ? 0 : 1;
            }
            
            internal override void  insert(BtreeKey key, int i)
            {
                items[i] = key.node;
                data[i] = (uint) key.key.ival;
            }
            
            internal BtreePageOfUInt(Storage s):base(s, MAX_ITEMS)
            {
                data = new uint[MAX_ITEMS];
            }
            
            internal BtreePageOfUInt()
            {
            }
        }
        
        class BtreePageOfLong:BtreePage
        {
            override internal Array Data
            {
                get
                {
                    return data;
                }
                
            }
            internal long[] data;
            
            const int MAX_ITEMS = BTREE_PAGE_SIZE / (4 + 8);
            
            
            internal override Key getKey(int i)
            {
                return new Key(data[i]);
            }
            
            internal override object getKeyValue(int i)
            {
                return data[i];
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfLong(Storage);
            }
            
            internal override int compare(Key key, int i)
            {
                return key.lval < data[i] ? -1 : key.lval == data[i] ? 0 : 1;
            }
            
            internal override void  insert(BtreeKey key, int i)
            {
                items[i] = key.node;
                data[i] = key.key.lval;
            }
            
            internal BtreePageOfLong(Storage s):base(s, MAX_ITEMS)
            {
                data = new long[MAX_ITEMS];
            }
            
            internal BtreePageOfLong()
            {
            }
        }
        
        class BtreePageOfULong:BtreePage
        {
            override internal Array Data
            {
                get
                {
                    return data;
                }
                
            }
            internal ulong[] data;
            
            const int MAX_ITEMS = BTREE_PAGE_SIZE / (4 + 8);
            
            
            internal override Key getKey(int i)
            {
                return new Key(data[i]);
            }
            
            internal override object getKeyValue(int i)
            {
                return data[i];
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfULong(Storage);
            }
            
            internal override int compare(Key key, int i)
            {
                return (ulong)key.lval < data[i] ? -1 : (ulong)key.lval == data[i] ? 0 : 1;
            }
            
            internal override void  insert(BtreeKey key, int i)
            {
                items[i] = key.node;
                data[i] = (ulong)key.key.lval;
            }
            
            internal BtreePageOfULong(Storage s):base(s, MAX_ITEMS)
            {
                data = new ulong[MAX_ITEMS];
            }
            
            internal BtreePageOfULong()
            {
            }
        }
        
        class BtreePageOfFloat:BtreePage
        {
            override internal Array Data
            {
                get
                {
                    return data;
                }
                
            }
            internal float[] data;
            
            const int MAX_ITEMS = BTREE_PAGE_SIZE / (4 + 4);
            
            
            internal override Key getKey(int i)
            {
                return new Key(data[i]);
            }
            
            internal override object getKeyValue(int i)
            {
                return data[i];
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfFloat(Storage);
            }
            
            internal override int compare(Key key, int i)
            {
                return (float) key.dval < data[i]?- 1:(float) key.dval == data[i]?0:1;
            }
            
            internal override void  insert(BtreeKey key, int i)
            {
                items[i] = key.node;
                data[i] = (float) key.key.dval;
            }
            
            internal BtreePageOfFloat(Storage s):base(s, MAX_ITEMS)
            {
                data = new float[MAX_ITEMS];
            }
            
            internal BtreePageOfFloat()
            {
            }
        }
        
        class BtreePageOfDouble:BtreePage
        {
            override internal Array Data
            {
                get
                {
                    return data;
                }
                
            }
            internal double[] data;
            
            const int MAX_ITEMS = BTREE_PAGE_SIZE / (4 + 8);
            
            
            internal override Key getKey(int i)
            {
                return new Key(data[i]);
            }
            
            internal override object getKeyValue(int i)
            {
                return data[i];
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfDouble(Storage);
            }
            
            internal override int compare(Key key, int i)
            {
                return key.dval < data[i]?- 1:key.dval == data[i]?0:1;
            }
            
            internal override void  insert(BtreeKey key, int i)
            {
                items[i] = key.node;
                data[i] = key.key.dval;
            }
            
            internal BtreePageOfDouble(Storage s):base(s, MAX_ITEMS)
            {
                data = new double[MAX_ITEMS];
            }
            
            internal BtreePageOfDouble()
            {
            }
        }
        
        class BtreePageOfGuid:BtreePage
        {
            override internal Array Data
            {
                get
                {
                    return data;
                }
                
            }
            internal Guid[] data;
            
            const int MAX_ITEMS = BTREE_PAGE_SIZE / (4 + 16);
            
            
            internal override Key getKey(int i)
            {
                return new Key(data[i]);
            }
            
            internal override object getKeyValue(int i)
            {
                return data[i];
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfGuid(Storage);
            }
            
            internal override int compare(Key key, int i)
            {
                return key.guid.CompareTo(data[i]);
            }
            
            internal override void  insert(BtreeKey key, int i)
            {
                items[i] = key.node;
                data[i] = key.key.guid;
            }
            
            internal BtreePageOfGuid(Storage s):base(s, MAX_ITEMS)
            {
                data = new Guid[MAX_ITEMS];
            }
            
            internal BtreePageOfGuid()
            {
            }
        }
        
        class BtreePageOfDecimal:BtreePage
        {
            override internal Array Data
            {
                get
                {
                    return data;
                }
                
            }
            internal decimal[] data;
            
            const int MAX_ITEMS = BTREE_PAGE_SIZE / (4 + 16);
            
            
            internal override Key getKey(int i)
            {
                return new Key(data[i]);
            }
            
            internal override object getKeyValue(int i)
            {
                return data[i];
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfDecimal(Storage);
            }
            
            internal override int compare(Key key, int i)
            {
                return key.dec.CompareTo(data[i]);
            }
            
            internal override void  insert(BtreeKey key, int i)
            {
                items[i] = key.node;
                data[i] = key.key.dec;
            }
            
            internal BtreePageOfDecimal(Storage s):base(s, MAX_ITEMS)
            {
                data = new decimal[MAX_ITEMS];
            }
            
            internal BtreePageOfDecimal()
            {
            }
        }
        
        class BtreePageOfObject:BtreePage
        {
            override internal Array Data
            {
                get
                {
                    return data.ToRawArray();
                }
                
            }
            internal Link data;
            
            const int MAX_ITEMS = BTREE_PAGE_SIZE / (4 + 4);
            
            
            internal override Key getKey(int i)
            {
                return new Key(data.GetRaw(i));
            }
            
            internal override object getKeyValue(int i)
            {
                return data[i];
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfObject(Storage);
            }
            
            internal override int compare(Key key, int i)
            {
                return (int) key.ival - data[i].Oid;
            }
            
            internal override void  insert(BtreeKey key, int i)
            {
                items[i] = key.node;
                data[i] = (IPersistent) key.key.oval;
            }
            
            internal BtreePageOfObject(Storage s):base(s, MAX_ITEMS)
            {
                data = s.CreateLink(MAX_ITEMS);
                data.Length = MAX_ITEMS;
            }
            
            internal BtreePageOfObject()
            {
            }
        }
        
        class BtreePageOfString:BtreePage
        {
            override internal Array Data
            {
                get
                {
                    return data;
                }
                
            }
            internal string[] data;
            
            internal const int MAX_ITEMS = 100;
            
            
            internal override Key getKey(int i)
            {
                return new Key(data[i]);
            }
            
            internal override object getKeyValue(int i)
            {
                return data[i];
            }
            
            internal override void  clearKeyValue(int i)
            {
                data[i] = null;
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfString(Storage);
            }
            
            internal override int compare(Key key, int i)
            {
                return ((string) key.oval).CompareTo(data[i]);
            }
            
            internal override void  insert(BtreeKey key, int i)
            {
                items[i] = key.node;
                data[i] = (string) key.key.oval;
            }
            
            internal override void  memset(int i, int len)
            {
                while (--len >= 0)
                {
                    items[i] = null;
                    data[i] = null;
                    i += 1;
                }
            }
            
            internal virtual bool prefixSearch(string key, int height, ArrayList result)
            {
                int l = 0, n = nItems, r = n;
                height -= 1;
                while (l < r)
                {
                    int i = (l + r) >> 1;
                    if (!key.StartsWith(data[i]) && key.CompareTo(data[i]) > 0)
                    {
                        l = i + 1;
                    }
                    else
                    {
                        r = i;
                    }
                }
                Debug.Assert(r == l);
                if (height == 0)
                {
                    while (l < n)
                    {
                        if (key.CompareTo(data[l]) < 0)
                        {
                            return false;
                        }
                        result.Add(items[l]);
                        l += 1;
                    }
                }
                else
                {
                    do 
                    {
                        if (!((BtreePageOfString) items[l]).prefixSearch(key, height, result))
                        {
                            return false;
                        }
                        if (l == n)
                        {
                            return true;
                        }
                    }
                    while (key.CompareTo(data[l++]) >= 0);
                    return false;
                }
                return true;
            }
            
            
            internal BtreePageOfString(Storage s):base(s, MAX_ITEMS)
            {
                data = new string[MAX_ITEMS];
            }
            
            internal BtreePageOfString()
            {
            }
        }
        
        class BtreePageOfRaw:BtreePage
        {
            override internal Array Data
            {
                get
                {
                    return (Array)data;
                }
                
            }
            internal object data;
            
            internal const int MAX_ITEMS = 100;
            
            
            internal override Key getKey(int i)
            {
                return new Key((IComparable)((object[]) data)[i]);
            }
            
            internal override object getKeyValue(int i)
            {
                return ((object[])data)[i];
            }
            
            internal override void  clearKeyValue(int i)
            {
                ((object[])data)[i] = null;
            }
            
            internal override BtreePage clonePage()
            {
                return new BtreePageOfRaw(Storage);
            }
            
            internal override int compare(Key key, int i)
            {
                return ((IComparable) key.oval).CompareTo(((object[]) data)[i]);
            }
            
            internal override void  insert(BtreeKey key, int i)
            {
                items[i] = key.node;
                ((object[])data)[i] = key.key.oval;
            }
            
            internal BtreePageOfRaw(Storage s):base(s, MAX_ITEMS)
            {
                data = new object[MAX_ITEMS];
            }
            
            internal BtreePageOfRaw()
            {
            }
        }
        
        
        
        internal static ClassDescriptor.FieldType checkType(Type c)
        {
            ClassDescriptor.FieldType elemType = ClassDescriptor.getTypeCode(c);
            if ((int)elemType > (int)ClassDescriptor.FieldType.tpOid
                && elemType != ClassDescriptor.FieldType.tpDecimal
                && elemType != ClassDescriptor.FieldType.tpRaw
                && elemType != ClassDescriptor.FieldType.tpGuid) 
            {
                throw new StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE, c);
            }
            return elemType;
        }
        
#if USE_GENERICS
        internal AltBtree(bool unique)
        {
            type = checkType(typeof(K));
            this.unique = unique;
        }
#else
        internal AltBtree(Type cls, bool unique)
        {
            type = checkType(cls);
            this.unique = unique;
        }
#endif
        
        internal AltBtree(ClassDescriptor.FieldType type, bool unique)
        {
            this.type = type;
            this.unique = unique;
        }
        
        internal enum OperationResult 
        { 
            Done, 
            Overflow,
            Underflow,
            NotFound,
            Duplicate,
            Overwrite
        }
        
        public override int Count 
        { 
            get 
            {
                return nElems;
            }
        }

#if USE_GENERICS
        public V this[K key] 
#else
        public IPersistent this[object key] 
#endif
        {
            get 
            {
                return Get(key);
            }
            set 
            {
                Set(key, value);
            }
        } 
        
#if USE_GENERICS
        public V[] this[K from, K till] 
#else
        public IPersistent[] this[object from, object till] 
#endif
        {
            get 
            {
                return Get(from, till);
            }
        }


        internal virtual Key checkKey(Key key)
        {
            if (key != null)
            {
                if (key.type != type)
                {
                    throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_KEY_TYPE);
                }
                if ((type == ClassDescriptor.FieldType.tpObject 
                     || type == ClassDescriptor.FieldType.tpOid) 
                    && key.ival == 0 && key.oval != null)
                {
                    throw new StorageError(StorageError.ErrorCode.INVALID_OID);
                }
                if (key.oval is char[])
                {
                    key = new Key(new string((char[]) key.oval), key.inclusion != 0);
                }
            }
            return key;
        }
        
#if USE_GENERICS
        public virtual V Get(Key key)
#else
        public virtual IPersistent Get(Key key)
#endif
        {
            key = checkKey(key);
            if (root != null)
            {
                ArrayList list = new ArrayList();
                root.find(key, key, height, list);
                if (list.Count > 1)
                {
                    throw new StorageError(StorageError.ErrorCode.KEY_NOT_UNIQUE);
                }
                else if (list.Count == 0)
                {
                    return null;
                }
                else
                {
#if USE_GENERICS
                    return (V) list[0];
#else
                    return (IPersistent) list[0];
#endif
                }
            }
            return null;
        }

#if USE_GENERICS
        public virtual V Get(K key) 
#else
        public virtual IPersistent Get(object key) 
#endif
        {
            return Get(KeyBuilder.getKeyFromObject(key));
        }


#if USE_GENERICS
        internal static V[] emptySelection = new V[0];
        
        public virtual V[] PrefixSearch(string key)
#else
        internal static IPersistent[] emptySelection = new IPersistent[0];
        
        public virtual IPersistent[] PrefixSearch(string key)
#endif
        {
            if (ClassDescriptor.FieldType.tpString != type)
            {
                throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_KEY_TYPE);
            }
            if (root != null)
            {
                ArrayList list = new ArrayList();
                ((BtreePageOfString) root).prefixSearch(key, height, list);
                if (list.Count != 0)
                {
#if USE_GENERICS
                    return (V[]) list.ToArray(typeof(V));
#else
                    return (IPersistent[]) list.ToArray(typeof(IPersistent));
#endif
                }
            }
            return emptySelection;
        }
        
#if USE_GENERICS
        public virtual V[] Get(Key from, Key till)
#else
        public virtual IPersistent[] Get(Key from, Key till)
#endif
        {
            if (root != null)
            {
                ArrayList list = new ArrayList();
                root.find(checkKey(from), checkKey(till), height, list);
                if (list.Count != 0)
                {
#if USE_GENERICS
                    return (V[]) list.ToArray(typeof(V));
#else
                    return (IPersistent[]) list.ToArray(typeof(IPersistent));
#endif
                }
            }
            return emptySelection;
        }
        
#if USE_GENERICS
        public virtual V[] Get(K from, K till)
#else
        public virtual IPersistent[] Get(object from, object till)
#endif
        {
            return Get(KeyBuilder.getKeyFromObject(from), KeyBuilder.getKeyFromObject(till));
        }
         
#if USE_GENERICS
        public virtual bool Put(Key key, V obj)
#else
        public virtual bool Put(Key key, IPersistent obj)
#endif
        {
            return insert(key, obj, false) == null;
        }
        
#if USE_GENERICS
        public virtual bool Put(K key, V obj)
#else
        public virtual bool Put(object key, IPersistent obj)
#endif
        {
            return Put(KeyBuilder.getKeyFromObject(key), obj);
        }

#if USE_GENERICS
        public virtual V Set(Key key, V obj)
        {
            return (V)insert(key, obj, true);
        }
#else
        public virtual IPersistent Set(Key key, IPersistent obj)
        {
            return insert(key, obj, true);
        }
#endif
        
#if USE_GENERICS
        public virtual V Set(K key, V obj)
#else
        public virtual IPersistent Set(object key, IPersistent obj)
#endif
        {
            return Set(KeyBuilder.getKeyFromObject(key), obj);
        }

        internal void  allocateRootPage(BtreeKey ins)
        {
            Storage s = Storage;
            BtreePage newRoot = null;
            switch (type)
            {               
                case ClassDescriptor.FieldType.tpByte: 
                    newRoot = new BtreePageOfByte(s);
                    break;
                
                case ClassDescriptor.FieldType.tpSByte: 
                    newRoot = new BtreePageOfSByte(s);
                    break;
                
                case ClassDescriptor.FieldType.tpShort: 
                    newRoot = new BtreePageOfShort(s);
                    break;
                
                case ClassDescriptor.FieldType.tpUShort: 
                    newRoot = new BtreePageOfUShort(s);
                    break;
                
                case ClassDescriptor.FieldType.tpBoolean: 
                    newRoot = new BtreePageOfBoolean(s);
                    break;
                
                case ClassDescriptor.FieldType.tpInt: 
                case ClassDescriptor.FieldType.tpOid: 
                    newRoot = new BtreePageOfInt(s);
                    break;
                
                case ClassDescriptor.FieldType.tpUInt: 
                    newRoot = new BtreePageOfInt(s);
                    break;
                
                case ClassDescriptor.FieldType.tpLong: 
                    newRoot = new BtreePageOfLong(s);
                    break;
                
                case ClassDescriptor.FieldType.tpULong: 
                    newRoot = new BtreePageOfLong(s);
                    break;
                
                case ClassDescriptor.FieldType.tpFloat: 
                    newRoot = new BtreePageOfFloat(s);
                    break;
                
                case ClassDescriptor.FieldType.tpDouble: 
                    newRoot = new BtreePageOfDouble(s);
                    break;
                
                case ClassDescriptor.FieldType.tpObject: 
                    newRoot = new BtreePageOfObject(s);
                    break;
                
                case ClassDescriptor.FieldType.tpString: 
                    newRoot = new BtreePageOfString(s);
                    break;
                
                case ClassDescriptor.FieldType.tpRaw: 
                    newRoot = new BtreePageOfRaw(s);
                    break;
                
                case ClassDescriptor.FieldType.tpDecimal: 
                    newRoot = new BtreePageOfDecimal(s);
                    break;
                
                case ClassDescriptor.FieldType.tpGuid: 
                    newRoot = new BtreePageOfGuid(s);
                    break;
                
                default: 
                    Debug.Assert(false, "Invalid type");
                    break;
                
            }
            newRoot.insert(ins, 0);
            newRoot.items[1] = root;
            newRoot.nItems = 1;
            root = newRoot;
        }
        
        internal IPersistent insert(Key key, IPersistent obj, bool overwrite)
        {
            BtreeKey ins = new BtreeKey(checkKey(key), obj);
            if (root == null)
            {
                allocateRootPage(ins);
                height = 1;
            }
            else
            {
                OperationResult result = root.insert(ins, height, unique, overwrite);
                if (result == OperationResult.Overflow)
                {
                    allocateRootPage(ins);
                    height += 1;
                }
                else if (result == OperationResult.Duplicate || result == OperationResult.Overwrite)
                {
                    return ins.oldNode;
                }
            }
            updateCounter += 1;
            nElems += 1;
            Modify();
            return null;
        }
        
#if USE_GENERICS
        public virtual void  Remove(Key key, V obj)
#else
        public virtual void  Remove(Key key, IPersistent obj)
#endif
        {
            Remove(new BtreeKey(checkKey(key), obj));
        }
        
#if USE_GENERICS
        public virtual void  Remove(K key, V obj)
#else
        public virtual void  Remove(object key, IPersistent obj)
#endif
        {
            Remove(new BtreeKey(KeyBuilder.getKeyFromObject(key), obj));    
        }
        
        
        internal virtual void Remove(BtreeKey rem)
        {
            if (root == null)
            {
                throw new StorageError(StorageError.ErrorCode.KEY_NOT_FOUND);
            }
            OperationResult result = root.remove(rem, height);
            if (result == OperationResult.NotFound)
            {
                throw new StorageError(StorageError.ErrorCode.KEY_NOT_FOUND);
            }
            nElems -= 1;
            if (result == OperationResult.Underflow)
            {
                if (root.nItems == 0)
                {
                    BtreePage newRoot = null;
                    if (height != 1)
                    {
                        newRoot = (BtreePage) root.items[0];
                    }
                    root.Deallocate();
                    root = newRoot;
                    height -= 1;
                }
            }
            updateCounter += 1;
            Modify();
        }
        
#if USE_GENERICS
        public virtual V Remove(Key key)
#else
        public virtual IPersistent Remove(Key key)
#endif
        {
            if (!unique)
            {
                throw new StorageError(StorageError.ErrorCode.KEY_NOT_UNIQUE);
            }
            BtreeKey rk = new BtreeKey(checkKey(key), null);
            Remove(rk);
#if USE_GENERICS
            return (V)rk.oldNode;
#else
            return rk.oldNode;
#endif
        }
        
#if USE_GENERICS
        public virtual V RemoveKey(K key)
#else
        public virtual IPersistent Remove(object key)
#endif
        {
            return Remove(KeyBuilder.getKeyFromObject(key));
        }
        
#if USE_GENERICS
        public virtual V[] GetPrefix(string prefix)
#else
        public virtual IPersistent[] GetPrefix(string prefix)
#endif
        {
            return Get(new Key(prefix, true), new Key(prefix + Char.MaxValue, false));
        }
        
        
        public virtual int Size()
        {
            return nElems;
        }
        
#if USE_GENERICS
        public override void Clear() 
#else
        public void Clear() 
#endif
        {
            if (root != null)
            {
                root.purge(height);
                root = null;
                nElems = 0;
                height = 0;
                updateCounter += 1;
                Modify();
            }
        }
        
#if USE_GENERICS
        public virtual V[] ToArray()
        {
            V[] arr = new V[nElems];
            if (root != null)
            {
                root.traverseForward(height, arr, 0);
            }
            return (V[])arr;
        }
#else
        public virtual IPersistent[] ToArray()
        {
            IPersistent[] arr = new IPersistent[nElems];
            if (root != null)
            {
                root.traverseForward(height, arr, 0);
            }
            return arr;
        }
#endif
        
        public virtual Array ToArray(Type elemType)
        {
            Array arr = Array.CreateInstance(elemType, nElems);
            if (root != null)
            {
                root.traverseForward(height, (IPersistent[])arr, 0);
            }
            return arr;
        }
        
        public override void Deallocate()
        {
            if (root != null)
            {
                root.purge(height);
            }
            base.Deallocate();
        }
        
#if USE_GENERICS
        class BtreeEnumerator : IEnumerator<V>
#else
        class BtreeEnumerator : IEnumerator 
#endif
        {
#if USE_GENERICS
            internal BtreeEnumerator(AltBtree<K,V> tree) 
#else
            internal BtreeEnumerator(AltBtree tree) 
#endif
            { 
                this.tree = tree;
                Reset();
            }
            
            public void Reset() 
            {
                BtreePage page = tree.root;
                int h = tree.height;
                counter = tree.updateCounter;
                pageStack = new BtreePage[h];
                posStack = new int[h];
                sp = 0;
                if (h > 0)
                {
                    while (--h > 0)
                    {
                        posStack[sp] = 0;
                        pageStack[sp] = page;
                        page = (BtreePage) page.items[0];
                        sp += 1;
                    }
                    posStack[sp] = 0;
                    pageStack[sp] = page;
                    end = page.nItems;
                    sp += 1;
                }
            }
            
            protected virtual void getCurrent(BtreePage pg, int pos)
            {
                curr = pg.items[pos];
            }
            
            public void Dispose() {}

            public bool MoveNext() 
            {
                if (tree.updateCounter != tree.updateCounter) 
                { 
                    throw new InvalidOperationException("B-Tree was modified");
                }
                if (sp > 0 && posStack[sp-1] < end) 
                {
                    int pos = posStack[sp - 1];
                    BtreePage pg = pageStack[sp - 1];
                    getCurrent(pg, pos);
                    hasCurrent = true;
                    if (++pos == end)
                    {
                        while (--sp != 0)
                        {
                            pos = posStack[sp - 1];
                            pg = pageStack[sp - 1];
                            if (++pos <= pg.nItems)
                            {
                                posStack[sp - 1] = pos;
                                do 
                                {
                                    pg = (BtreePage) pg.items[pos];
                                    end = pg.nItems;
                                    pageStack[sp] = pg;
                                    posStack[sp] = pos = 0;
                                }
                                while (++sp < pageStack.Length);
                                break;
                            }
                        }
                    }
                    else
                    {
                        posStack[sp - 1] = pos;
                    }
                    return true;
                }
                hasCurrent = false;
                return false;
            }

#if USE_GENERICS
            public virtual V Current
#else
            public virtual object Current
#endif
            {
                get 
                {
                    if (!hasCurrent) 
                    { 
                        throw new InvalidOperationException();
                    }
#if USE_GENERICS
                    return (V)curr;
#else
                    return curr;
#endif
                }
            }
            
            protected BtreePage[] pageStack;
            protected int[] posStack;
            protected int sp;
            protected int end;
            protected int counter;
            protected IPersistent curr;
            protected bool hasCurrent;
#if USE_GENERICS
            protected AltBtree<K,V> tree;
#else
            protected AltBtree tree;
#endif
        }
        
        class BtreeDictionaryEnumerator : BtreeEnumerator, IDictionaryEnumerator
        {
#if USE_GENERICS
            internal BtreeDictionaryEnumerator(AltBtree<K,V> tree) 
#else
            internal BtreeDictionaryEnumerator(AltBtree tree) 
#endif
                : base(tree) 
            {   
            }

                
            protected override void getCurrent(BtreePage pg, int pos) 
            { 
                base.getCurrent(pg, pos);
                key = pg.getKeyValue(pos);
            }

#if USE_GENERICS
            public new virtual object Current 
#else
            public override object Current 
#endif
            {
                get 
                {
                    return Entry;
                }
            }

            public DictionaryEntry Entry 
            {
                get 
                {
                    if (!hasCurrent) 
                    { 
                        throw new InvalidOperationException();
                    }
                    return new DictionaryEntry(key, curr);
                }
            }

            public object Key 
            {
                get 
                {
                    if (!hasCurrent) 
                    { 
                        throw new InvalidOperationException();
                    }
                    return key;
                }
            }

            public object Value 
            {
                get 
                {
                    if (!hasCurrent) 
                    { 
                        throw new InvalidOperationException();
                    }
                    return curr;
                }
            }

            protected object key;
        }
        
        
#if USE_GENERICS
        public override IEnumerator<V> GetEnumerator() 
#else
        public override IEnumerator GetEnumerator() 
#endif
        {
            return new BtreeEnumerator(this);
        }
        
        public IDictionaryEnumerator GetDictionaryEnumerator() 
        {
            return new BtreeDictionaryEnumerator(this);
        }
        
#if USE_GENERICS        
        class BtreeSelectionIterator : IEnumerator<V>, IEnumerable<V>
#else
        class BtreeSelectionIterator : IEnumerator, IEnumerable 
#endif
        { 
#if USE_GENERICS        
            internal BtreeSelectionIterator(AltBtree<K,V> tree, Key from, Key till, IterationOrder order) 
#else
            internal BtreeSelectionIterator(AltBtree tree, Key from, Key till, IterationOrder order) 
#endif
            { 
                this.from = from;
                this.till = till;
                this.order = order;
                this.tree = tree;
                Reset();
            }

#if USE_GENERICS        
            public IEnumerator<V> GetEnumerator() 
#else
            public IEnumerator GetEnumerator() 
#endif
            {
                return this;
            }

            public void Reset() 
            {
                int i, l, r;
                
                sp = 0;
                counter = tree.updateCounter;
                if (tree.height == 0)
                {
                    return;
                }
                BtreePage page = tree.root;
                int h = tree.height;
                this.from = from;
                this.till = till;
                this.order = order;
                
                pageStack = new BtreePage[h];
                posStack = new int[h];
                
                if (order == IterationOrder.AscentOrder)
                {
                    if (from == null)
                    {
                        while (--h > 0)
                        {
                            posStack[sp] = 0;
                            pageStack[sp] = page;
                            page = (BtreePage) page.items[0];
                            sp += 1;
                        }
                        posStack[sp] = 0;
                        pageStack[sp] = page;
                        end = page.nItems;
                        sp += 1;
                    }
                    else
                    {
                        while (--h > 0)
                        {
                            pageStack[sp] = page;
                            l = 0;
                            r = page.nItems;
                            while (l < r)
                            {
                                i = (l + r) >> 1;
                                if (page.compare(from, i) >= from.inclusion)
                                {
                                    l = i + 1;
                                }
                                else
                                {
                                    r = i;
                                }
                            }
                            Debug.Assert(r == l);
                            posStack[sp] = r;
                            page = (BtreePage) page.items[r];
                            sp += 1;
                        }
                        pageStack[sp] = page;
                        l = 0;
                        r = end = page.nItems;
                        while (l < r)
                        {
                            i = (l + r) >> 1;
                            if (page.compare(from, i) >= from.inclusion)
                            {
                                l = i + 1;
                            }
                            else
                            {
                                r = i;
                            }
                        }
                        Debug.Assert(r == l);
                        if (r == end)
                        {
                            sp += 1;
                            gotoNextItem(page, r - 1);
                        }
                        else
                        {
                            posStack[sp++] = r;
                        }
                    }
                    if (sp != 0 && till != null)
                    {
                        page = pageStack[sp - 1];
                        if (- page.compare(till, posStack[sp - 1]) >= till.inclusion)
                        {
                            sp = 0;
                        }
                    }
                }
                else
                {
                    // descent order
                    if (till == null)
                    {
                        while (--h > 0)
                        {
                            pageStack[sp] = page;
                            posStack[sp] = page.nItems;
                            page = (BtreePage) page.items[page.nItems];
                            sp += 1;
                        }
                        pageStack[sp] = page;
                        posStack[sp++] = page.nItems - 1;
                    }
                    else
                    {
                        while (--h > 0)
                        {
                            pageStack[sp] = page;
                            l = 0;
                            r = page.nItems;
                            while (l < r)
                            {
                                i = (l + r) >> 1;
                                if (page.compare(till, i) >= 1 - till.inclusion)
                                {
                                    l = i + 1;
                                }
                                else
                                {
                                    r = i;
                                }
                            }
                            Debug.Assert(r == l);
                            posStack[sp] = r;
                            page = (BtreePage) page.items[r];
                            sp += 1;
                        }
                        pageStack[sp] = page;
                        l = 0;
                        r = page.nItems;
                        while (l < r)
                        {
                            i = (l + r) >> 1;
                            if (page.compare(till, i) >= 1 - till.inclusion)
                            {
                                l = i + 1;
                            }
                            else
                            {
                                r = i;
                            }
                        }
                        Debug.Assert(r == l);
                        if (r == 0)
                        {
                            sp += 1;
                            gotoNextItem(page, r);
                        }
                        else
                        {
                            posStack[sp++] = r - 1;
                        }
                    }
                    if (sp != 0 && from != null)
                    {
                        page = pageStack[sp - 1];
                        if (page.compare(from, posStack[sp - 1]) >= from.inclusion)
                        {
                            sp = 0;
                        }
                    }
                }
            }
            
            public void Dispose() {}

            public bool MoveNext() 
            {
                if (tree.updateCounter != tree.updateCounter) 
                { 
                    throw new InvalidOperationException("B-Tree was modified");
                }
                if (sp != 0) 
                {
                    int pos = posStack[sp - 1];
                    BtreePage pg = pageStack[sp - 1];
                    hasCurrent = true;
                    getCurrent(pg, pos);
                    gotoNextItem(pg, pos);
                    return true;
                }
                hasCurrent = false;
                return false;
            }
            
            protected virtual void getCurrent(BtreePage pg, int pos)
            {
                curr = pg.items[pos];
            }
            
#if USE_GENERICS        
            public virtual V Current 
#else
            public virtual object Current 
#endif
            {
                get 
                {
                    if (!hasCurrent) 
                    { 
                        throw new InvalidOperationException();
                    }
#if USE_GENERICS        
                    return (V)curr;
#else
                    return curr;
#endif
                }
            }
            
            protected internal void  gotoNextItem(BtreePage pg, int pos)
            {
                if (order == IterationOrder.AscentOrder)
                {
                    if (++pos == end)
                    {
                        while (--sp != 0)
                        {
                            pos = posStack[sp - 1];
                            pg = pageStack[sp - 1];
                            if (++pos <= pg.nItems)
                            {
                                posStack[sp - 1] = pos;
                                do 
                                {
                                    pg = (BtreePage) pg.items[pos];
                                    end = pg.nItems;
                                    pageStack[sp] = pg;
                                    posStack[sp] = pos = 0;
                                }
                                while (++sp < pageStack.Length);
                                break;
                            }
                        }
                    }
                    else
                    {
                        posStack[sp - 1] = pos;
                    }
                    if (sp != 0 && till != null && - pg.compare(till, pos) >= till.inclusion)
                    {
                        sp = 0;
                    }
                }
                else
                {
                    // descent order
                    if (--pos < 0)
                    {
                        while (--sp != 0)
                        {
                            pos = posStack[sp - 1];
                            pg = pageStack[sp - 1];
                            if (--pos >= 0)
                            {
                                posStack[sp - 1] = pos;
                                do 
                                {
                                    pg = (BtreePage) pg.items[pos];
                                    pageStack[sp] = pg;
                                    posStack[sp] = pos = pg.nItems;
                                }
                                while (++sp < pageStack.Length);
                                posStack[sp - 1] = --pos;
                                break;
                            }
                        }
                    }
                    else
                    {
                        posStack[sp - 1] = pos;
                    }
                    if (sp != 0 && from != null && pg.compare(from, pos) >= from.inclusion)
                    {
                        sp = 0;
                    }
                }
            }
            
            
            protected BtreePage[] pageStack;
            protected int[] posStack;
            protected int sp;
            protected int end;
            protected Key from;
            protected Key till;
            protected IterationOrder order;
            protected int counter;
            protected bool hasCurrent;
            protected IPersistent curr;
#if USE_GENERICS
            protected AltBtree<K,V> tree;
#else
            protected AltBtree tree;
#endif
        }
        
        class BtreeDictionarySelectionIterator : BtreeSelectionIterator, IDictionaryEnumerator 
        { 
#if USE_GENERICS
            internal BtreeDictionarySelectionIterator(AltBtree<K,V> tree, Key from, Key till, IterationOrder order) 
#else
            internal BtreeDictionarySelectionIterator(AltBtree tree, Key from, Key till, IterationOrder order) 
#endif
                : base(tree, from, till, order)
            {}
               
            protected override void getCurrent(BtreePage pg, int pos)
            {
                base.getCurrent(pg, pos);
                key = pg.getKeyValue(pos);
            }
             
#if USE_GENERICS        
            public new virtual object Current 
#else
            public override object Current 
#endif
            {
                get 
                {
                    return Entry;
                }
            }

            public DictionaryEntry Entry 
            {
                get 
                {
                    if (!hasCurrent) 
                    { 
                        throw new InvalidOperationException();
                    }
                    return new DictionaryEntry(key, curr);
                }
            }

            public object Key 
            {
                get 
                {
                    if (!hasCurrent) 
                    { 
                        throw new InvalidOperationException();
                    }
                    return key;
                }
            }

            public object Value 
            {
                get 
                {
                    if (!hasCurrent) 
                    { 
                        throw new InvalidOperationException();
                    }
                    return curr;
                }
            }

            protected object key;
        }
        
#if USE_GENERICS        
        public IEnumerator<V> GetEnumerator(Key from, Key till, IterationOrder order) 
#else
        public IEnumerator GetEnumerator(Key from, Key till, IterationOrder order) 
#endif
        {
            return Range(from, till, order).GetEnumerator();
        }

#if USE_GENERICS        
        public IEnumerator<V> GetEnumerator(K from, K till, IterationOrder order) 
#else
        public IEnumerator GetEnumerator(object from, object till, IterationOrder order) 
#endif
        {
            return Range(from, till, order).GetEnumerator();
        }

#if USE_GENERICS        
        public IEnumerator<V> GetEnumerator(Key from, Key till) 
#else
        public IEnumerator GetEnumerator(Key from, Key till) 
#endif
        {
            return Range(from, till).GetEnumerator();
        }

#if USE_GENERICS        
        public IEnumerator<V> GetEnumerator(K from, K till) 
#else
        public IEnumerator GetEnumerator(object from, object till) 
#endif
        {
            return Range(from, till).GetEnumerator();
        }

#if USE_GENERICS        
        public IEnumerator<V> GetEnumerator(string prefix) 
#else
        public IEnumerator GetEnumerator(string prefix) 
#endif
        {
            return StartsWith(prefix).GetEnumerator();
        }

#if USE_GENERICS        
        public virtual IEnumerable<V> Range(Key from, Key till, IterationOrder order) 
        { 
            return new BtreeSelectionIterator(this, checkKey(from), checkKey(till), order);
        }
#else
        public virtual IEnumerable Range(Key from, Key till, IterationOrder order) 
        { 
            return new BtreeSelectionIterator(this, checkKey(from), checkKey(till), order);
        }
#endif

#if USE_GENERICS        
        public virtual IEnumerable<V> Range(Key from, Key till) 
#else
        public virtual IEnumerable Range(Key from, Key till) 
#endif
        { 
            return Range(from, till, IterationOrder.AscentOrder);
        }
            
#if USE_GENERICS        
        public IEnumerable<V> Range(K from, K till, IterationOrder order) 
#else
        public IEnumerable Range(object from, object till, IterationOrder order) 
#endif
        { 
            return Range(KeyBuilder.getKeyFromObject(from), KeyBuilder.getKeyFromObject(till), order);
        }

#if USE_GENERICS        
        public IEnumerable<V> Range(K from, K till) 
#else
        public IEnumerable Range(object from, object till) 
#endif
        { 
            return Range(KeyBuilder.getKeyFromObject(from), KeyBuilder.getKeyFromObject(till), IterationOrder.AscentOrder);
        }
 
#if USE_GENERICS
        public IEnumerable<V> Reverse()
#else
        public IEnumerable Reverse()
#endif
        { 
            return new BtreeSelectionIterator(this, null, null, IterationOrder.DescentOrder);
        }

#if USE_GENERICS        
        public IEnumerable<V> StartsWith(string prefix) 
#else
        public IEnumerable StartsWith(string prefix) 
#endif
        { 
            return Range(new Key(prefix), new Key(prefix + Char.MaxValue, false), IterationOrder.AscentOrder);
        }
 
        public virtual IDictionaryEnumerator GetDictionaryEnumerator(Key from, Key till, IterationOrder order) 
        { 
            return new BtreeDictionarySelectionIterator(this, checkKey(from), checkKey(till), order);
        }        
    }
}