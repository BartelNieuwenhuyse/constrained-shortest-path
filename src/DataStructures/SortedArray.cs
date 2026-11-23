namespace DataStructures;

/// <summary>
/// A class that keeps a sorted set in a array
/// lookup predecessor/successor is done with binary search, so O(log(n))
/// insert/remove needs to do a move of subarray, so O(n)
/// </summary>
public interface IComparer<T, U>
{
    int Compare(T x, U y);
    int Compare(U y, T x);
}
#if INSTRUMENTATION
public interface ISortedArrayCounter<in T> where T : allows ref struct
{
    void UpdateWrites(int number_of_writes);
    void UpdateCompares();
}
#endif
public struct SortedArray<T, C
#if INSTRUMENTATION
    , P
#endif
    > where C : struct, IComparer<T>
#if INSTRUMENTATION
    where P : struct, ISortedArrayCounter<T>
#endif
{
    /// <summary>
    /// comparer to use when comparing two instances of type T
    /// </summary>
    private C comparer;
#if INSTRUMENTATION
    private P positioner;
#endif
    /// <summary>
    /// Size of the array = number of elements in the sorted array
    /// (may be different from items.Length, which is the capacity)
    /// </summary>
    public int array_size;

    /// <summary>
    /// Collection of elements in the sorted array
    /// this array is kept sorted
    /// </summary>
    public T[] sorted_array;
#if TEST
    private bool check;
#endif
    public SortedArray(int size, int capacity, C comparer
#if INSTRUMENTATION
        , P positioner
#endif
#if TEST
        , bool check
#endif
        )
    {
        this.array_size = size;
        this.sorted_array = new T[capacity];
        this.comparer = comparer;
#if INSTRUMENTATION
        this.positioner = positioner;
#endif
#if TEST
        this.check = check;
#endif
    }

    public SortedArray(int size, C comparer
#if INSTRUMENTATION
        , P positioner
#endif
#if TEST
        , bool check
#endif
        )
        : this(size, size, comparer
#if INSTRUMENTATION
        , positioner
#endif
#if TEST
        , check
#endif
        )
    {
    }

    /// <summary>
    /// Find the index of the predecessor:
    /// sorted_array[index], target, sorted_array[index + 1]
    /// </summary>
    /// <param name="target"></param>
    /// <returns>index in the sorted_array where to find the predecessor</returns>
    public int predecessor<U, CU>(U target, CU comparer_u) where CU : struct, IComparer<T, U>
    {
#if TEST
        int index =
#else
        return
#endif
            predecessor(0, array_size - 1, target, comparer_u);
#if TEST
        if (check)
        {
            if (index >= array_size || index < -1) { PrintFailedCheck($"Invalid binary search result: {index} should be at most {array_size - 1} and at least -1"); }
            if (index < 0 && array_size > 0 && comparer_u.Compare(target, sorted_array[0]) >= 0) { PrintFailedCheck($"Invalid binary search result: first item smaller than target {target}"); }
            if (index < array_size && index >= 0 && comparer_u.Compare(sorted_array[index], target) > 0) { PrintFailedCheck($"Invalid binary search result: item at {index} is not predecessor of target {target}"); }
            if (index + 1 < array_size && index >= 0 && comparer_u.Compare(target, sorted_array[index + 1]) > 0) { PrintFailedCheck($"Invalid binary search result: item at {index + 1} is not successor of target {target}"); }
            if (index == array_size && array_size > 0 && comparer_u.Compare(sorted_array[array_size - 1], target) > 0) { PrintFailedCheck($"Invalid binary search result: last is not predecessor of target {target}"); }
        }
        return index;
#endif
    }
    private int predecessor<U, CU>(int left, int right, U target, CU comparer_u) where CU : struct, IComparer<T, U>
    {
#if TEST
        if (check)
        {
            if (left < 0 || right >= array_size) { PrintFailedCheck($"Invalid binary search bounds: left {left}, right {right}, array length {array_size}"); }
        }
#endif
        if (array_size == 0) return left - 1;

        // find the first index in the array where the item is smaller than target
        // if all items are larger than target, return right + 1
        int index;
        do
        {
            index = left + (right - left) / 2;
            int comparison = comparer_u.Compare(sorted_array[index], target);
#if INSTRUMENTATION
            positioner.UpdateCompares();
#endif
            if (comparison == 0)
                return index;
            if (comparison < 0)
                left = index + 1;
            else
                right = index - 1;
        } while (left <= right);

        return right; // return the first item < target
    }
    private int predecessor(int left, int right, T target)
    {
#if TEST
        if (check)
        {
            if (left < 0 || right >= array_size) { PrintFailedCheck($"Invalid binary search bounds: left {left}, right {right}, array length {array_size}"); }
        }
#endif
        if (array_size == 0) return left - 1;

        // find the first index in the array where the item is smaller than target
        // if all items are larger than target, return right + 1
        int index;
        do
        {
            index = left + (right - left) / 2;
            int comparison = comparer.Compare(sorted_array[index], target);
#if INSTRUMENTATION
            positioner.UpdateCompares();
#endif
            if (comparison == 0)
                return index;
            if (comparison < 0)
                left = index + 1;
            else
                right = index - 1;
        } while (left <= right);

        return right; // return the first item < target
    }

    /// <summary>
    /// Find the index of the successor:
    /// sorted_array[index - 1], target, sorted_array[index]
    /// </summary>
    /// <param name="target"></param>
    /// <returns>index in the sorted_array where to find the successor</returns>
    public int successor(T target)
    {
#if TEST
        int index =
#else
        return
#endif
        successor(0, array_size - 1, target);
#if TEST
        if (check)
        {
            if (index > array_size || index < 0) { PrintFailedCheck($"Invalid binary search result: {index} should be at most {array_size} and at least 0"); }
            if (index == 0 && array_size > 0 && comparer.Compare(target, sorted_array[0]) > 0) { PrintFailedCheck($"Invalid binary search result: first item smaller than target {target}"); }
            if (index == array_size && array_size > 0 && comparer.Compare(sorted_array[array_size - 1], target) >= 0) { PrintFailedCheck($"Invalid binary search result: last item is larger than target {target}"); }
            if (index < array_size && index > 0 && comparer.Compare(sorted_array[index - 1], target) >= 0) { PrintFailedCheck($"Invalid binary search result: item at {index - 1} is not predecessor of target {target}"); }
            if (index < array_size && index >= 0 && comparer.Compare(target, sorted_array[index]) > 0) { PrintFailedCheck($"Invalid binary search result: item at {index} is not successor of target {target}"); }
        }
        return index;
#endif
    }
    private int successor(int left, int right, T target)
    {
#if TEST
        if (check)
        {
            if (left < 0 || right >= array_size) { PrintFailedCheck($"Invalid binary search bounds: left {left}, right {right}, array length {array_size}"); }
        }
#endif
        if (array_size == 0) return left;

        // find the first index in the array where the item is smaller than target
        // if all items are larger than target, return right + 1
        int index;
        do
        {
            index = left + (right - left) / 2;
            int comparison = comparer.Compare(sorted_array[index], target);
#if INSTRUMENTATION
            positioner.UpdateCompares();
#endif
            if (comparison == 0)
                return index;
            if (comparison < 0)
                left = index + 1;
            else
                right = index - 1;
        } while (left <= right);

        return left; // return the first item > target
    }
    public int successor<U, CU>(U target, CU comparer_u) where CU : struct, IComparer<T, U>
    {
#if TEST
        int index =
#else
        return
#endif
        successor(0, array_size - 1, target, comparer_u);
#if TEST
        if (check)
        {
            if (index > array_size || index < 0) { PrintFailedCheck($"Invalid binary search result: {index} should be at most {array_size} and at least 0"); }
            if (index == 0 && array_size > 0 && comparer_u.Compare(target, sorted_array[0]) > 0) { PrintFailedCheck($"Invalid binary search result: first item smaller than target {target}"); }
            if (index == array_size && array_size > 0 && comparer_u.Compare(sorted_array[array_size - 1], target) >= 0) { PrintFailedCheck($"Invalid binary search result: last item is larger than target {target}"); }
            if (index < array_size && index > 0 && comparer_u.Compare(sorted_array[index - 1], target) >= 0) { PrintFailedCheck($"Invalid binary search result: item at {index - 1} is not predecessor of target {target}"); }
            if (index < array_size && index >= 0 && comparer_u.Compare(target, sorted_array[index]) > 0) { PrintFailedCheck($"Invalid binary search result: item at {index} is not successor of target {target}"); }
        }
        return index;
#endif
    }
    private int successor<U, CU>(int left, int right, U target, CU comparer_u) where CU : struct, IComparer<T, U>
    {
#if TEST
        if (check)
        {
            if (left < 0 || right >= array_size) { PrintFailedCheck($"Invalid binary search bounds: left {left}, right {right}, array length {array_size}"); }
        }
#endif
        if (array_size == 0) return left;

        // find the first index in the array where the item is smaller than target
        // if all items are larger than target, return right + 1
        int index;
        do
        {
            index = left + (right - left) / 2;
            int comparison = comparer_u.Compare(sorted_array[index], target);
#if INSTRUMENTATION
            positioner.UpdateCompares();
#endif
            if (comparison == 0)
                return index;
            if (comparison < 0)
                left = index + 1;
            else
                right = index - 1;
        } while (left <= right);

        return left; // return the first item > target
    }

    /// <summary>
    /// insert element target into the array
    /// </summary>
    /// <param name="target">item to insert</param>
    /// <returns>index where item is inserted</returns>
    public int insert(T target)
    {
        int index = successor(0, array_size - 1, target);
        insert(index, target);
        return index;
    }
    public void insert(int index, T target)
    {
        Array.Copy(sorted_array, index, sorted_array, index + 1, array_size - index);
        sorted_array[index] = target;
#if INSTRUMENTATION
        positioner.UpdateWrites(array_size - index + 1);
#endif
        ++array_size;
#if TEST
        if (check)
        {
            for (int k = 1; k < array_size; ++k)
            {
                if (comparer.Compare(sorted_array[k - 1], sorted_array[k]) > 0) { PrintFailedCheck("SortedArray not sorted"); }
            }
        }
#endif
    }

    /// <summary>
    /// remove element at index
    /// </summary>
    /// <param name="index"></param>
    public void remove(int index)
    {
        Array.Copy(sorted_array, index + 1, sorted_array, index, array_size - index - 1);
        sorted_array[array_size - 1] = default;
#if INSTRUMENTATION
        positioner.UpdateWrites(array_size - index);
#endif
        --array_size;
#if TEST
        if (check)
        {
            for (int k = 1; k < array_size; ++k)
            {
                if (comparer.Compare(sorted_array[k - 1], sorted_array[k]) > 0) { PrintFailedCheck("SortedArray not sorted"); }
            }
        }
#endif
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
public struct SortedArrayOpsCounter<T> : ISortedArrayCounter<T>
{
    private Action<int> write_counter;
    private Action compare_counter;

    public SortedArrayOpsCounter(Action<int> write_counter, Action compare_counter)
    {
        this.write_counter = write_counter;
        this.compare_counter = compare_counter;
    }

    public void UpdateWrites(int number_of_writes)
    {
        write_counter(number_of_writes);
    }

    public void UpdateCompares()
    {
        compare_counter();
    }
}
#endif