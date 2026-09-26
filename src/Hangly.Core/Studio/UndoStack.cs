//
//  UndoStack.cs
//  Hangly
//
//  Value-type undo and redo.
//

namespace Hangly.Core.Studio;

/// <summary>Linear undo history over a value; the macOS <c>UndoStack</c>.</summary>
/// <remarks>
/// The caller owns the current value; the stack holds only what came before and, after an
/// undo, what came after. Recording a new change discards the redo branch, which is the
/// behaviour every editor has and every user expects.
/// </remarks>
public sealed class UndoStack<T>
{
    private readonly List<T> past = [];
    private readonly List<T> future = [];

    /// <summary>Oldest entries are dropped beyond this.</summary>
    public int Limit { get; init; } = 100;

    public bool CanUndo => past.Count > 0;

    public bool CanRedo => future.Count > 0;

    /// <summary>Call with the value <em>before</em> a change is applied.</summary>
    public void Record(T previous)
    {
        past.Add(previous);
        if (past.Count > Limit)
        {
            past.RemoveRange(0, past.Count - Limit);
        }

        future.Clear();
    }

    /// <param name="current">The value now, which becomes redoable.</param>
    /// <returns>The value to restore, or false if there is nothing to undo.</returns>
    public bool TryUndo(T current, out T previous) => Move(past, future, current, out previous);

    public bool TryRedo(T current, out T next) => Move(future, past, current, out next);

    public void Clear()
    {
        past.Clear();
        future.Clear();
    }

    private static bool Move(List<T> from, List<T> to, T current, out T value)
    {
        if (from.Count == 0)
        {
            value = current;
            return false;
        }

        value = from[^1];
        from.RemoveAt(from.Count - 1);
        to.Add(current);
        return true;
    }
}
