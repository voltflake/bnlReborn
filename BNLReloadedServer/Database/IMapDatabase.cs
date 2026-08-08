using BNLReloadedServer.BaseTypes;

namespace BNLReloadedServer.Database;

public interface IMapDatabase
{
    public List<string> GetMapIds();
    public bool HasMap(Key key);
    public MapData? LoadMapData(Key key);
    public void SaveMap(string key, MapData mapData);
}
