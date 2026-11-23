namespace DataStructures;

/// <summary>
/// Heap implementation
/// </summary>
public struct Heap<T, C> where C : struct, IComparer<T>
{
    /// <summary>
    /// comparer to use when comparing two instances of type T
    /// </summary>
    private readonly C comparer;

    /// <summary>
    /// Size of the heap = number of elements in the heap
    /// (may be different from heap.Length)
    /// </summary>
    public int heap_size;

    /// <summary>
    /// Collection of elements in the heap
    /// </summary>
    public T[] heap;

    /// <summary>
    /// Peek the root of the heap
    /// </summary>
    public T peek => heap[0];
#if TEST
    private bool check;
#endif
    public Heap(int size, int capacity, C comparer
#if TEST
        , bool check
#endif
        )
    {
        this.heap_size = size;
        this.heap = new T[capacity];
        this.comparer = comparer;
#if TEST
        this.check = check;
#endif
    }

    public Heap(int size, C comparer
#if TEST
        , bool check
#endif
        ) : this(size, size, comparer
#if TEST
                , check
#endif
                )
    {
    }

    /// <summary>
    /// construct a heap from the first heap_size elements in heap[0..heap_size) 
    /// </summary>
    public void heapify()
    {
        // start is assigned the index of the last parent node
        int start = (heap_size - 2) / 2;

        while (start >= 0)
        {
            // sift down the node at index start to the proper place
            // such that all nodes below the start index are in heap order
            sift_down(start);

            // decrement to the next lowest parent node
            start--;
        }
        // after sifting down the root all nodes/elements are in heap order
#if TEST
        if (check)
        {
            for (int i = heap_size - 1; i > 0; --i)
            {
                if (comparer.Compare(heap[(i - 1) / 2], heap[i]) > 0) { PrintFailedCheck($"Cost of parent {heap[(i - 1) / 2]} is bigger than cost of child {heap[i]} in heap"); }
            }
        }
#endif
    }

    /// <summary>
    /// returns the smallest element out of the heap
    /// and removes it from the heap
    /// </summary>
    /// <returns>the smallest element out of the heap</returns>
    public T dequeue()
    {
        // get the root of the heap
        T root = heap[0];

        // swap the root of the heap with the last element in the heap            
        heap[0] = heap[--heap_size];
        heap[heap_size] = default;

        // restore the heap (push the root element down until the heap is restored)
        sift_down(0);
#if TEST
        if (check)
        {
            for (int i = heap_size - 1; i > 0; --i)
            {
                if (comparer.Compare(heap[(i - 1) / 2], heap[i]) > 0) { PrintFailedCheck($"Cost of parent {heap[(i - 1) / 2]} is bigger than cost of child {heap[i]} in heap"); }
            }
        }
#endif
        return root;
    }

    /// <summary>
    /// adds an element to the heap
    /// </summary>
    /// <param name="add">element to add to the heap</param>
    public void enqueue(T add)
    {
        // place the element at the end of the heap
        heap[heap_size++] = add;

        // restore the heap (pull the added element up towards the root until the heap is restored)
        sift_up(heap_size - 1);
#if TEST
        if (check)
        {
            for (int i = heap_size - 1; i > 0; --i)
            {
                if (comparer.Compare(heap[(i - 1) / 2], heap[i]) > 0) { PrintFailedCheck($"Cost of parent {heap[(i - 1) / 2]} is bigger than cost of child {heap[i]} in heap"); }
            }
        }
#endif
    }

    /// <summary>
    /// restores the heap when only heap[root] is out-of min-heap order
    /// heap[root] is larger than its parent, but might not be smaller than its children
    /// </summary>
    /// <param name="root">the node in the heap tree to sift_down (i.e. swap with children as long as cost of child is smaller than cost of root)</param>
    public void sift_down(int root)
    {
        // while the root has at least one child
        while ((2 * root + 1) < heap_size)
        {
            // root * 2 + 1 points to the left child
            int child = 2 * root + 1;

            // take the smallest of the left or right child
            if (child + 1 < heap_size && comparer.Compare(heap[child + 1], heap[child]) < 0)
            {
                // then point to the right child instead
                child += 1;
            }

            // out of min-heap order
            // swap the child with root if child is smaller
            if (comparer.Compare(heap[child], heap[root]) < 0)
            {
                // swap items in the heap
                (heap[child], heap[root]) = (heap[root], heap[child]);

                // recurse
                root = child;
            }
            else
            {
                return;
            }
        }
    }

    /// <summary>
    /// restores the heap when only heap[child] is out-of min-heap order
    /// heap[child] is smaller than its children, but might not be larger than its parent
    /// </summary>
    /// <param name="child">the node in the heap tree to sift_up (i.e. swap with parent as long as cost of parent is larger than cost of child)</param>
    public void sift_up(int child)
    {
        // as long as the child is not the root
        while (child > 0)
        {
            // get the parent of child
            int parent = (child - 1) / 2;

            // out of min-heap order
            // swap the child with root if child is smaller
            if (comparer.Compare(heap[child], heap[parent]) < 0)
            {
                // swap items in the heap
                (heap[child], heap[parent]) = (heap[parent], heap[child]);

                // recurse
                child = parent;
            }
            else
            {
                return;
            }
        }
    }
#if TEST
    #region print
    public void PrintFailedCheck(string message)
    {
        Console.WriteLine(message); Console.ReadLine();
    }
    #endregion
#endif
}

/// <summary>
/// Dynamic heap implementation
/// </summary>
public interface IHeapPosition<in T> where T : allows ref struct
{
    void UpdatePosition(T x, int pos);
}
public struct Heap<T, C, P> where C : struct, IComparer<T> where P : struct, IHeapPosition<T>
{
    /// <summary>
    /// comparer to use when comparing two instances of type T
    /// </summary>
    private C comparer;

    /// <summary>
    /// position updater to use when changing the element's position in the heap
    /// </summary>
    private P positioner;

    /// <summary>
    /// Size of the heap = number of elements in the heap
    /// (may be different from heap.Length)
    /// </summary>
    public int heap_size;

    /// <summary>
    /// Collection of elements in the heap
    /// </summary>
    public T[] heap;

    /// <summary>
    /// Peek the root of the heap
    /// </summary>
    public T peek => heap[0];
#if TEST
    private bool check;
#endif
    public Heap(int size, int capacity, C comparer, P positioner
#if TEST
        , bool check
#endif
        )
    {
        this.heap_size = size;
        this.heap = new T[capacity];
        this.comparer = comparer;
        this.positioner = positioner;
#if TEST
        this.check = check;
#endif
    }

    public Heap(int size, C comparer, P positioner
#if TEST
        , bool check
#endif
        )
        : this(size, size, comparer, positioner
#if TEST
              , check
#endif
        )
    {
    }

    /// <summary>
    /// construct a heap from the first heap_size elements in heap[0..heap_size) 
    /// </summary>
    public void heapify()
    {
        // start is assigned the index of the last parent node
        int start = (heap_size - 2) / 2;

        while (start >= 0)
        {
            // sift down the node at index start to the proper place
            // such that all nodes below the start index are in heap order
            sift_down(start);

            // decrement to the next lowest parent node
            start--;
        }
        // after sifting down the root all nodes/elements are in heap order
#if TEST
        if (check)
        {
            for (int i = heap_size - 1; i > 0; --i)
            {
                if (comparer.Compare(heap[(i - 1) / 2], heap[i]) > 0) { PrintFailedCheck($"Cost of parent {heap[(i - 1) / 2]} is bigger than cost of child {heap[i]} in heap"); }
            }
        }
#endif
    }

    /// <summary>
    /// returns the top element out of the heap
    /// and removes it from the heap
    /// </summary>
    /// <returns>the top element out of the heap</returns>
    public T dequeue()
    {
        // get the root of the heap
        T root = heap[0];

        // swap the root of the heap with the last element in the heap            
        heap[0] = heap[--heap_size];
        positioner.UpdatePosition(heap[0], 0);
        heap[heap_size] = default;

        // restore the heap (push the root element down until the heap is restored)
        sift_down(0);
#if TEST
        if (check)
        {
            for (int i = heap_size - 1; i > 0; --i)
            {
                if (comparer.Compare(heap[(i - 1) / 2], heap[i]) > 0) { PrintFailedCheck($"Cost of parent {heap[(i - 1) / 2]} is bigger than cost of child {heap[i]} in heap"); }
            }
        }
#endif
        return root;
    }

    /// <summary>
    /// adds an element to the heap
    /// </summary>
    /// <param name="add">element to add to the heap</param>
    public void enqueue(T add)
    {
        // place the element at the end of the heap
        heap[heap_size++] = add;
        positioner.UpdatePosition(add, heap_size - 1);

        // restore the heap (pull the added element up towards the root until the heap is restored)
        sift_up(heap_size - 1);
#if TEST
        if (check)
        {
            for (int i = heap_size - 1; i > 0; --i)
            {
                if (comparer.Compare(heap[(i - 1) / 2], heap[i]) > 0) { PrintFailedCheck($"Cost of parent {heap[(i - 1) / 2]} is bigger than cost of child {heap[i]} in heap"); }
            }
        }
#endif
    }

    /// <summary>
    /// restores the heap when only heap[root] is out-of heap order
    /// heap[root] is compares positive to its parent, but might not compare negative to its children
    /// </summary>
    /// <param name="root">the node in the heap tree to sift_down (i.e. swap with children as long as cost of child compares negative with cost of root)</param>
    public void sift_down(int root)
    {
        // while the root has at least one child
        while ((2 * root + 1) < heap_size)
        {
            // root * 2 + 1 points to the left child
            int child = 2 * root + 1;

            // take the smallest of the left or right child
            if (child + 1 < heap_size && comparer.Compare(heap[child + 1], heap[child]) < 0)
            {
                // then point to the right child instead
                child += 1;
            }

            // out of heap order
            // swap the child with root if child is smaller
            if (comparer.Compare(heap[child], heap[root]) < 0)
            {
                // swap items in the heap
                (heap[child], heap[root]) = (heap[root], heap[child]);
                positioner.UpdatePosition(heap[child], child);
                positioner.UpdatePosition(heap[root], root);

                // recurse
                root = child;
            }
            else
            {
                return;
            }
        }
    }

    /// <summary>
    /// restores the heap when only heap[child] is out-of heap order
    /// heap[child] compares negative to its children, but might not compare positive to its parent
    /// </summary>
    /// <param name="child">the node in the heap tree to sift_up (i.e. swap with parent as long as cost of parent compares positive to the cost of child)</param>
    public void sift_up(int child)
    {
        // as long as the child is not the root
        while (child > 0)
        {
            // get the parent of child
            int parent = (child - 1) / 2;

            // out of heap order
            // swap the child with root if child is smaller
            if (comparer.Compare(heap[child], heap[parent]) < 0)
            {
                // swap items in the heap
                (heap[child], heap[parent]) = (heap[parent], heap[child]);
                positioner.UpdatePosition(heap[child], child);
                positioner.UpdatePosition(heap[parent], parent);

                // recurse
                child = parent;
            }
            else
            {
                return;
            }
        }
    }
#if TEST
    #region print
    public void PrintFailedCheck(string message)
    {
        Console.WriteLine(message); Console.ReadLine();
    }
    #endregion
#endif
}

#if INSTRUMENTATION
/// <summary>
/// Use this class to count the number of heap operations (enqueue, dequeue, sift_up, sift_down) performed by the heap.
/// </summary>
/// <typeparam name="T">The type of elements in the heap.</typeparam>
public struct HeapOpsCounter<T> : IHeapPosition<T>
{
    private Action counter;

    public HeapOpsCounter(Action counter)
    {
        this.counter = counter;
    }

    public void UpdatePosition(T label, int pos)
    {
        counter();
    }
}
#endif