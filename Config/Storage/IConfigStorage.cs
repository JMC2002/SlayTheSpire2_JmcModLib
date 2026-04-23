using System.Reflection;

namespace JmcModLib.Config.Storage;

public interface IConfigStorage
{
    string GetFileName(Assembly? assembly = null);

    string GetFilePath(Assembly? assembly = null);

    bool Exists(Assembly? assembly = null);

    void Save(string key, string group, object? value, Assembly? assembly = null);

    bool TryLoad(string key, string group, Type valueType, out object? value, Assembly? assembly = null);

    void Flush(Assembly? assembly = null);
}
