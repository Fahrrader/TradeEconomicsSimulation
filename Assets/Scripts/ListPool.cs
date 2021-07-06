using System.Collections.Generic;

public static class ListPool<T>
{
    private static readonly Stack<List<T>> Stack = new Stack<List<T>>();

    public static List<T> Get()
    {
        return Stack.Count > 0 ? Stack.Pop() : new List<T>();
    }

    public static void Add(List<T> list)
    {
        list.Clear();
        Stack.Push(list);
    }
}