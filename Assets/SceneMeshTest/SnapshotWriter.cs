using System.IO;
using System.Text;
using UnityEngine;

public static class SnapshotWriter
{
    [System.Serializable] class SnapshotWrapper { public SceneSnapshot snapshot; }

    public static string GetSnapshotPath(string fileName)
    {
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    public static void WriteSnapshotJson(string fileName, SceneSnapshot snapshot)
    {
        var path = GetSnapshotPath(fileName);

        // JsonUtility는 root object가 필요해서 wrapper로 감싼다
        var wrap = new SnapshotWrapper { snapshot = snapshot };
        var json = JsonUtility.ToJson(wrap, prettyPrint: true);

        // atomic write
        var tmp = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(tmp, json, Encoding.UTF8);
        File.Copy(tmp, path, overwrite: true);
        File.Delete(tmp);
    }
}
