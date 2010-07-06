package org.garret.perst.impl;

import org.garret.perst.*;
import java.util.ArrayList;

public class TtreePage extends Persistent  { 
    static final int maxItems = (Page.pageSize-ObjectHeader.sizeof-4*4)/4;
    static final int minItems = maxItems - 2; // minimal number of items in internal node

    TtreePage   left;
    TtreePage   right;
    int         balance;
    int         nItems;
    IPersistent item[];

    static class PageReference { 
        TtreePage pg;
        
        PageReference(TtreePage p) { pg = p; }
    }

    public boolean recursiveLoading() {
        return false;
    }

    TtreePage() {}

    TtreePage(IPersistent mbr) { 
        nItems = 1;
        item = new IPersistent[maxItems];
        item[0] = mbr;
    }

    final IPersistent loadItem(int i) 
    { 
        IPersistent mbr = item[i];
        mbr.load();
        return mbr;
    }

    final boolean find(PersistentComparator comparator, Object minValue, Object maxValue, ArrayList selection)
    { 
        int l, r, m, n;
        load();
        n = nItems;
        if (minValue != null) { 
            if (comparator.compareMemberWithKey(loadItem(0), minValue) < 0) {       
                if (comparator.compareMemberWithKey(loadItem(n-1), maxValue) < 0) { 
                    if (right != null) { 
                        return right.find(comparator, minValue, maxValue, selection); 
                    } 
                    return true;
                }
                for (l = 0, r = n; l < r;) { 
                    m = (l + r) >> 1;
                    if (comparator.compareMemberWithKey(loadItem(m), minValue) < 0) {
                        l = m+1;
                    } else { 
                        r = m;
                    }
                }
                while (r < n) { 
                    if (maxValue != null
                        && comparator.compareMemberWithKey(loadItem(r), maxValue) > 0)
                    { 
                        return false;
                    }
                    selection.add(loadItem(r));
                    r += 1;
                }
                if (right != null) { 
                    return right.find(comparator, minValue, maxValue, selection); 
                } 
                return true;    
            }
        }       
        if (left != null) { 
            if (!left.find(comparator, minValue, maxValue, selection)) { 
                return false;
            }
        }
        for (l = 0; l < n; l++) { 
            if (maxValue != null && comparator.compareMemberWithKey(loadItem(l), maxValue) > 0) {
                return false;
            }
            selection.add(loadItem(l));
        }
        if (right != null) { 
            return right.find(comparator, minValue, maxValue, selection);
        }         
        return true;
    }
    
    final boolean contains(PersistentComparator comparator, IPersistent mbr)
    { 
        int l, r, m, n;
        load();
        n = nItems;
        if (comparator.compareMembers(loadItem(0), mbr) < 0) {      
            if (comparator.compareMembers(loadItem(n-1), mbr) < 0) { 
                if (right != null) { 
                    return right.contains(comparator, mbr); 
                } 
                return false;
            }
            for (l = 0, r = n; l < r;) { 
                m = (l + r) >> 1;
                if (comparator.compareMembers(loadItem(m), mbr) < 0) {
                    l = m+1;
                } else { 
                    r = m;
                }
            }
            while (r < n) { 
                if (mbr == loadItem(r)) { 
                    return true;
                }
                if (comparator.compareMembers(item[r], mbr) > 0) { 
                    return false;
                }
                r += 1;
            }
            if (right != null) { 
                return right.contains(comparator, mbr); 
            } 
            return false;       
        }
        if (left != null) { 
            if (left.contains(comparator, mbr)) { 
                return true;
            }
        }
        for (l = 0; l < n; l++) { 
            if (mbr == loadItem(l)) { 
                return true;
            }
            if (comparator.compareMembers(item[l], mbr) > 0) {
                return false;
            }
        }
        if (right != null) { 
            return right.contains(comparator, mbr);
        }         
        return false;
    }

    
    static final int OK         = 0;
    static final int NOT_UNIQUE = 1;
    static final int NOT_FOUND  = 2;
    static final int OVERFLOW   = 3;
    static final int UNDERFLOW  = 4;

    final int insert(PersistentComparator comparator, IPersistent mbr, boolean unique, PageReference ref) 
    { 
        load();
        int n = nItems;
        TtreePage pg;
        int diff = comparator.compareMembers(mbr, loadItem(0));
        if (diff <= 0) { 
            if (unique && diff == 0) { 
                return NOT_UNIQUE;
            }
            if ((left == null || diff == 0) && n != maxItems) { 
                modify();
                for (int i = n; i > 0; i--) item[i] = item[i-1];
                item[0] = mbr;
                nItems += 1;
                return OK;
            } 
            if (left == null) { 
                modify();
                left = new TtreePage(mbr);
            } else {
                pg = ref.pg;
                ref.pg = left;
                int result = left.insert(comparator, mbr, unique, ref);
                if (result == NOT_UNIQUE) { 
                    return NOT_UNIQUE;
                }
                modify();
                left = ref.pg;
                ref.pg = pg;
                if (result == OK) return OK;
            }
            if (balance > 0) { 
                balance = 0;
                return OK;
            } else if (balance == 0) { 
                balance = -1;
                return OVERFLOW;
            } else { 
                TtreePage left = this.left;
                left.load();
                left.modify();
                if (left.balance < 0) { // single LL turn
                    this.left = left.right;
                    left.right = this;
                    balance = 0;
                    left.balance = 0;
                    ref.pg = left;
                } else { // double LR turn
                    TtreePage right = left.right;
                    right.load();
                    right.modify();
                    left.right = right.left;
                    right.left = left;
                    this.left = right.right;
                    right.right = this;
                    balance = (right.balance < 0) ? 1 : 0;
                    left.balance = (right.balance > 0) ? -1 : 0;
                    right.balance = 0;
                    ref.pg = right;
                }
                return OK;
            }
        } 
        diff = comparator.compareMembers(mbr, loadItem(n-1));
        if (diff >= 0) { 
            if (unique && diff == 0) { 
                return NOT_UNIQUE;
            }
            if ((right == null || diff == 0) && n != maxItems) { 
                modify();
                item[n] = mbr;
                nItems += 1;
                return OK;
            }
            if (right == null) { 
                 modify();
                 right = new TtreePage(mbr);
            } else { 
                pg = ref.pg;
                ref.pg = right;
                int result = right.insert(comparator, mbr, unique, ref);
                if (result == NOT_UNIQUE) { 
                    return NOT_UNIQUE;
                }
                modify();
                right = ref.pg;
                ref.pg = pg;
                if (result == OK) return OK;
            }
            if (balance < 0) { 
                balance = 0;
                return OK;
            } else if (balance == 0) { 
                balance = 1;
                return OVERFLOW;
            } else { 
                TtreePage right = this.right;
                right.load();
                right.modify();
                if (right.balance > 0) { // single RR turn
                    this.right = right.left;
                    right.left = this;
                    balance = 0;
                    right.balance = 0;
                    ref.pg = right;
                } else { // double RL turn
                    TtreePage left = right.left;
                    left.load();
                    left.modify();
                    right.left = left.right;
                    left.right = right;
                    this.right = left.left;
                    left.left = this;
                    balance = (left.balance > 0) ? -1 : 0;
                    right.balance = (left.balance < 0) ? 1 : 0;
                    left.balance = 0;
                    ref.pg = left;
                }
                return OK;
            }
        }
        int l = 1, r = n-1;
        while (l < r)  {
            int i = (l+r) >> 1;
            diff = comparator.compareMembers(mbr, loadItem(i));
            if (diff > 0) { 
                l = i + 1;
            } else { 
                r = i;
                if (diff == 0) { 
                    if (unique) { 
                        return NOT_UNIQUE;
                    }
                    break;
                }
            }
        }
        // Insert before item[r]
        modify();
        if (n != maxItems) {
            for (int i = n; i > r; i--) item[i] = item[i-1]; 
            item[r] = mbr;
            nItems += 1;
            return OK;
        } else { 
            IPersistent reinsertItem;
            if (balance >= 0) { 
                reinsertItem = loadItem(0);
                for (int i = 1; i < r; i++) item[i-1] = item[i]; 
                item[r-1] = mbr;
            } else { 
                reinsertItem = loadItem(n-1);
                for (int i = n-1; i > r; i--) item[i] = item[i-1]; 
                item[r] = mbr;
            }
            return insert(comparator, reinsertItem, unique, ref);
        }
    }
       
    final int balanceLeftBranch(PageReference ref) 
    {
        if (balance < 0) { 
            balance = 0;
            return UNDERFLOW;
        } else if (balance == 0) { 
            balance = 1;
            return OK;
        } else { 
            TtreePage right = this.right;
            right.load();
            right.modify();
            if (right.balance >= 0) { // single RR turn
                this.right = right.left;
                right.left = this;
                if (right.balance == 0) { 
                    this.balance = 1;
                    right.balance = -1;
                    ref.pg = right;
                    return OK;
                } else { 
                    balance = 0;
                    right.balance = 0;
                    ref.pg = right;
                    return UNDERFLOW;
                }
            } else { // double RL turn
                TtreePage left = right.left;
                left.load();
                left.modify();
                right.left = left.right;
                left.right = right;
                this.right = left.left;
                left.left = this;
                balance = left.balance > 0 ? -1 : 0;
                right.balance = left.balance < 0 ? 1 : 0;
                left.balance = 0;
                ref.pg = left;
                return UNDERFLOW;
            }
        }
    }

    final int balanceRightBranch(PageReference ref) 
    {
        if (balance > 0) { 
            balance = 0;
            return UNDERFLOW;
        } else if (balance == 0) { 
            balance = -1;
            return OK;
        } else { 
            TtreePage left = this.left;
            left.load();
            left.modify();
            if (left.balance <= 0) { // single LL turn
                this.left = left.right;
                left.right = this;
                if (left.balance == 0) { 
                    balance = -1;
                    left.balance = 1;
                    ref.pg = left;
                    return OK;
                } else { 
                    balance = 0;
                    left.balance = 0;
                    ref.pg = left;
                    return UNDERFLOW;
                }
            } else { // double LR turn
                TtreePage right = left.right;
                right.load();
                right.modify();
                left.right = right.left;
                right.left = left;
                this.left = right.right;
                right.right = this;
                balance = right.balance < 0 ? 1 : 0;
                left.balance = right.balance > 0 ? -1 : 0;
                right.balance = 0;
                ref.pg = right;
                return UNDERFLOW;
            }
        }
    }
    
    final int remove(PersistentComparator comparator, IPersistent mbr, PageReference ref)
    {
        load();
        TtreePage pg;
        int n = nItems;
        int diff = comparator.compareMembers(mbr, loadItem(0));
        if (diff <= 0) { 
            if (left != null) { 
                modify();
                pg = ref.pg;
                ref.pg = left;
                int h = left.remove(comparator, mbr, ref);
                left = ref.pg;
                ref.pg = pg;
                if (h == UNDERFLOW) { 
                    return balanceLeftBranch(ref);
                } else if (h == OK) { 
                    return OK;
                }
            }
        }
        diff = comparator.compareMembers(mbr, loadItem(n-1));
        if (diff <= 0) {            
            for (int i = 0; i < n; i++) { 
                if (loadItem(i) == mbr) { 
                    if (n == 1) { 
                        if (right == null) { 
                            deallocate();
                            ref.pg = left;
                            return UNDERFLOW;
                        } else if (left == null) { 
                            deallocate();
                            ref.pg = right;
                            return UNDERFLOW;
                        } 
                    }
                    modify();
                    if (n <= minItems) { 
                        if (left != null && balance <= 0) {  
                            TtreePage prev = left;
                            prev.load();
                            while (prev.right != null) {                                 
                                prev = prev.right;
                                prev.load();
                            }
                            while (--i >= 0) { 
                                item[i+1] = item[i];
                            }
                            item[0] = prev.item[prev.nItems-1];
                            pg = ref.pg;
                            ref.pg = left;
                            int h = left.remove(comparator, loadItem(0), ref);
                            left = ref.pg;
                            ref.pg = pg;
                            if (h == UNDERFLOW) {
                                h = balanceLeftBranch(ref);
                            }
                            return h;
                        } else if (right != null) { 
                            TtreePage next = right;
                            next.load();
                            while (next.left != null) { 
                                next = next.left;
                                next.load();
                            }
                            while (++i < n) { 
                                item[i-1] = item[i];
                            }
                            item[n-1] = next.item[0];
                            pg = ref.pg;
                            ref.pg = right;
                            int h = right.remove(comparator, loadItem(n-1), ref);
                            right = ref.pg;
                            ref.pg = pg;
                            if (h == UNDERFLOW) {
                                h = balanceRightBranch(ref);
                            }
                            return h;
                        }
                    }
                    while (++i < n) { 
                        item[i-1] = item[i];
                    }
                    nItems -= 1;
                    return OK;
                }
            }
        }
        if (right != null) { 
            modify();
            pg = ref.pg;
            ref.pg = right;
            int h = right.remove(comparator, mbr, ref);
            right = ref.pg;
            ref.pg = pg;
            if (h == UNDERFLOW) { 
                return balanceRightBranch(ref);
            } else { 
                return h;
            }
        }
        return NOT_FOUND;
    }


    final int toArray(IPersistent[] arr, int index) { 
        load();
        if (left != null) { 
            index = left.toArray(arr, index);
        }
        for (int i = 0, n = nItems; i < n; i++) { 
            arr[index++] = loadItem(i);
        }
        if (right != null) { 
            index = right.toArray(arr, index);
        }
        return index;
    }

    final void prune() { 
        load();
        if (left != null) { 
            left.prune();
        }
        if (right != null) { 
            right.prune();
        }
        deallocate();
    }

}
