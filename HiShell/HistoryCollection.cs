using System.Collections;

namespace MrHihi.HiShell;
public class HistoryCollection: ICollection<History>
{
    private int _seekIndex = -1;
    private List<History> _histories = new List<History>();
    public int Count => _histories.Count;
    public int SeekIndex => _seekIndex;

    public bool IsReadOnly => false;

    public void Add(History item)
    {
        _histories.Add(item);
        _seekIndex = _histories.Count;
    }

    public History this[int index]
    {
        get => _histories[index];
    }

    public void Clear()
    {
        _histories.Clear();
        _seekIndex = -1;
    }

    public History? SeekNext()
    {
        if (_seekIndex < _histories.Count)
        {
            _seekIndex++;
        }
        return Current;
    }
    public History? SeekPrevious()
    {
        if (_seekIndex > 0)
        {
            _seekIndex--;
        }
        return Current;
    }

    public History? Current
    {
        get
        {
            if (_seekIndex >= 0 && _seekIndex < _histories.Count)
            {
                return _histories[_seekIndex];
            }
            return null;
        }
    }

    public bool Contains(History item)
    {
        return _histories.Contains(item);
    }

    public void CopyTo(History[] array, int arrayIndex)
    {
        _histories.CopyTo(array, arrayIndex);
    }

    public IEnumerator<History> GetEnumerator()
    {
        return _histories.GetEnumerator();
    }

    public bool Remove(History item)
    {
        var result = _histories.Remove(item);
        _seekIndex = _histories.Count;
        return result;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _histories.GetEnumerator();
    }
}