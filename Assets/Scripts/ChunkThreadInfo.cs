using System;

internal readonly struct ChunkThreadInfo<T> {
    public readonly Action<T> callback;
    public readonly T parameter;

    public ChunkThreadInfo (Action<T> callback, T parameter)
    {
        this.callback = callback;
        this.parameter = parameter;
    }
}