package org.garret.perst.impl;
import  org.garret.perst.*;
import  java.util.*;

class PersistentSet<T extends IPersistent> extends Btree<T> implements IPersistentSet<T> { 
    PersistentSet() { 
        type = ClassDescriptor.tpObject;
        unique = true;
    }

    public boolean isEmpty() { 
        return nElems == 0;
    }

    public boolean contains(Object o) {
        if (o instanceof IPersistent) { 
            Key key = new Key((IPersistent)o);
            Iterator i = iterator(key, key, ASCENT_ORDER);
            return i.hasNext();
        }
        return false;
    }
    
    public Object[] toArray() { 
        return toPersistentArray();
    }

    public <E> E[] toArray(E[] arr) { 
        return (E[])super.toArray((T[])arr);
    }

    public boolean add(T obj) { 
        if (!obj.isPersistent()) { 
            ((StorageImpl)getStorage()).storeObject(obj);
        }
        return insert(new Key(obj), obj, false);
    }

    public boolean remove(Object o) { 
        T obj = (T)o;
        try { 
            remove(new Key(obj), obj);
        } catch (StorageError x) { 
            if (x.getErrorCode() == StorageError.KEY_NOT_FOUND) { 
                return false;
            }
            throw x;
        }
        return true;
    }
    
    public boolean containsAll(Collection<?> c) { 
        Iterator i = c.iterator();
        while (i.hasNext()) { 
            if (!contains(i.next()))
                return false;
        }
        return true;
    }

    
    public boolean addAll(Collection<? extends T> c) {
        boolean modified = false;
        Iterator i = c.iterator();
        while (i.hasNext()) {
            modified |= add((T)i.next());
        }
        return modified;
    }

    public boolean retainAll(Collection<?> c) {
        ArrayList toBeRemoved = new ArrayList();
        Iterator i = c.iterator();
        while (i.hasNext()) {
            Object o = i.next();
            if (!c.contains(o)) {
                toBeRemoved.add(o);
            }
        }
        int n = toBeRemoved.size();
        for (int j = 0; j < n; j++) { 
            remove(toBeRemoved.get(j));
        }
        return n != 0;         
    }

    public boolean removeAll(Collection<?> c) {
        boolean modified = false;
        Iterator i = c.iterator();
        while (i.hasNext()) {
            modified |= remove(i.next());
        }
        return modified;
    }

    public boolean equals(Object o) {
        if (o == this) {
            return true;
        }
        if (!(o instanceof Set)) {
            return false;
        }
        Collection c = (Collection) o;
        if (c.size() != size()) {
            return false;
        }
        return containsAll(c);
    }

    public int hashCode() {
        int h = 0;
        Iterator i = iterator();
        while (i.hasNext()) {
            h += ((IPersistent)i.next()).getOid();
        }
        return h;
    }
}
