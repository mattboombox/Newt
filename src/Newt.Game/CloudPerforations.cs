namespace Newt.Game;

internal sealed class CloudPerforations
{
    internal const double RecoverySeconds = 12;
    private readonly Dictionary<(int X, int Y, int Face), double> _expires = [];
    private readonly List<(int X, int Y, int Face)> _expired = [];
    private double _seconds;
    internal HashSet<(int X, int Y, int Face)> DirtyCells { get; } = [];

    internal bool Contains((int X, int Y, int Face) cell) => _expires.ContainsKey(cell);

    internal void Punch((int X, int Y, int Face) cell)
    {
        if (!_expires.ContainsKey(cell)) DirtyCells.Add(cell);
        _expires[cell] = _seconds + RecoverySeconds;
    }

    internal void Advance(float seconds)
    {
        _seconds += seconds;
        _expired.Clear();
        foreach (var (cell, until) in _expires)
            if (until <= _seconds) _expired.Add(cell);
        foreach (var cell in _expired)
        {
            _expires.Remove(cell);
            DirtyCells.Add(cell);
        }
    }

    internal void Clear()
    {
        _expires.Clear();
        _expired.Clear();
        DirtyCells.Clear();
        _seconds = 0;
    }
}
