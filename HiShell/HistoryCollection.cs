using System.Collections;

namespace MrHihi.HiShell;
public class HistoryCollection: ICollection<History>
{
    private List<History> _histories = new List<History>();
    public int Count => _histories.Count;

    public bool IsReadOnly => false;

    public void Add(History item)
    {
        _histories.Add(item);
    }

    public void Clear()
    {
        _histories.Clear();
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
        return _histories.Remove(item);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _histories.GetEnumerator();
    }
}