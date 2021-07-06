using System.Collections.Generic;

namespace Pathfinding
{
    public class PathPriorityQueue
    {
        private readonly List<Cell> list = new List<Cell>();

        private int minimum = int.MaxValue;

        public int Count { get; private set; }

        public void Enqueue(Cell cell)
        {
            Count += 1;
            var priority = cell.SearchPriority;
            if (priority < minimum) minimum = priority;
            while (priority >= list.Count) list.Add(null);
            cell.NextWithSamePriority = list[priority];
            list[priority] = cell;
        }

        public Cell Dequeue()
        {
            Count -= 1;
            for (; minimum < list.Count; minimum++)
            {
                var cell = list[minimum];
                if (cell != null)
                {
                    list[minimum] = cell.NextWithSamePriority;
                    return cell;
                }
            }

            return null;
        }

        public void Change(Cell cell, int oldPriority)
        {
            var current = list[oldPriority];
            var next = current.NextWithSamePriority;
            if (current == cell)
            {
                list[oldPriority] = next;
            }
            else
            {
                while (next != cell)
                {
                    current = next;
                    next = current.NextWithSamePriority;
                }

                current.NextWithSamePriority = cell.NextWithSamePriority;
            }

            Enqueue(cell);
            Count -= 1;
        }

        public void Clear()
        {
            list.Clear();
            Count = 0;
            minimum = int.MaxValue;
        }
    }
}