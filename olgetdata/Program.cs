using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace olgetdata
{
    static class Program
    {
        static string FindGameDir()
        {
            if (Directory.Exists("Overload_Data"))
                return Directory.GetCurrentDirectory();
            try
            {
                var dir = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 448850",
                    "InstallLocation", null);
                if (dir != null)
                    return dir;
                dir = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\\WOW6432Node\\GOG.com\\Games\\1309632191",
                    "path", null);
                if (dir != null)
                    return dir;
            }
            catch (Exception)
            {
            }
            return null;
        }

        static string ReadStringToNull(this BinaryReader s)
        {
            var a = new List<byte>();
            byte b;
            while ((b = s.ReadByte()) != 0)
                a.Add(b);
            return UTF8Encoding.UTF8.GetString(a.ToArray());
        }

        static void AlignStream(this BinaryReader s, int alignment)
        {
            long pos = s.BaseStream.Position;
            int ofs = (int)(pos % alignment);
            if (ofs != 0)  
                s.BaseStream.Position += alignment - ofs;
        }

        static string ReadStringAlign(this BinaryReader s)
        {
            var ssize = s.ReadInt32();
            var data = s.ReadBytes(ssize);
            s.AlignStream(4);
            return UTF8Encoding.UTF8.GetString(data);
        }

        static uint SwapUInt(uint n)
        {
            return (n >> 24) | (((n >> 16) & 0xff) << 8) | (((n >> 8) & 0xff) << 16) | ((n & 0xff) << 24);
        }

        private class AssetObject
        {
            public UInt64 PathId;
            public uint DataOfs;
            public uint Size;
            public int TypeId, ClassId;
        }

        static AssetObject ReadAssetObject(BinaryReader es, uint format, List<int> ClassIds)
        {
            var obj = new AssetObject();
            obj.PathId = es.ReadUInt64();
            obj.DataOfs = es.ReadUInt32();
            obj.Size = es.ReadUInt32();
            if (format < 17)
            {
                obj.TypeId = es.ReadInt32();
                obj.ClassId = es.ReadInt16();
            }
            else
            {
                obj.TypeId = es.ReadInt32();
                obj.ClassId = ClassIds[obj.TypeId];
            }
            if (format <= 16)
                es.ReadInt16();
            if (format == 15 || format == 16)
                es.ReadByte();
            return obj;
        }

        static AssetObject[] ReadAssetObjects(BinaryReader es, out uint dataOffset)
        {
            var metaSize = SwapUInt(es.ReadUInt32());
            var fileSize = SwapUInt(es.ReadUInt32());
            var format = SwapUInt(es.ReadUInt32());
            dataOffset = SwapUInt(es.ReadUInt32());

            if (format >= 9)
            {
                var endianness = es.ReadUInt32();
                if (endianness != 0)
                    throw new Exception("Only little-endian assets supported");
            }

            var ClassIds = new List<int>();
            var Hashes = new Dictionary<int, byte[]>();
            var version = es.ReadStringToNull();
            var target = es.ReadInt32();
            //Debug.WriteLine(version + " target " + target);
            if (format >= 13)
            {
                var hasTypeTrees = es.ReadByte() != 0;
                if (hasTypeTrees)
                    throw new Exception("Assets with type trees not supported");
                var num = es.ReadInt32();
                for (int i = 0; i < num; i++)
                {
                    int classId = es.ReadInt32();
                    if (format >= 17)
                    {
                        var unk0 = es.ReadByte();
                        var scriptId = es.ReadInt16();
                        if (classId == 114)
                        {
                            if (scriptId >= 0)
                                classId = -2 - scriptId;
                            else
                                classId = -1;
                        }
                    }
                    ClassIds.Add(classId);
                    var hash = es.ReadBytes(classId < 0 ? 32 : 16);
                    if (classId != -1)
                        Hashes.Add(classId, hash);
                    //if (hasTypeTrees)
                    //    TypeTrees.Add(classId, LoadTree(es, format));
                }
            }

            var longObjIds = format >= 14 || (format >= 7 && es.ReadInt32() != 0);

            var numObjs = es.ReadUInt32();
            var objs = new AssetObject[numObjs];
            for (uint i = 0; i < numObjs; i++)
            {
                if (format >= 14)
                    es.AlignStream(4);
                objs[i] = ReadAssetObject(es, format, ClassIds);
            }
            return objs;
        }  

        static int SaveTextAssets(string filename)
        {
            int n = 0;
            using (var s = new BinaryReader(File.OpenRead(filename)))
            {
                var objs = ReadAssetObjects(s, out uint dataOffset);
                foreach (var obj in objs)
                {
                    if (obj.ClassId == 49) // TextAsset
                    {
                        s.BaseStream.Position = dataOffset + obj.DataOfs;
                        string name = s.ReadStringAlign();
                        string fn = null;
                        if (name == "projectile_data")
                            fn = "projdata.txt";
                        else if (name == "robot_data")
                            fn = "robotdata.txt";
                        if (fn == null)
                            continue;
                        if (File.Exists(fn)) {
                            Console.WriteLine(fn + " already exists: ignored.");
                        } else {
                            File.WriteAllText(fn, s.ReadStringAlign());
                            Console.WriteLine(fn + " written.");
                            n++;
                        }
                    }
                }
            }
            return n;
        }

        static void Main(string[] args)
        {
            string gameDir = args.Length != 0 ? args[0] : FindGameDir();
            if (gameDir == null)
                throw new Exception("Overload directiory not found, try copying olgetdata.exe to the Overload directory or pass the directory as argument.");
            Console.WriteLine("Using Overload directory " + gameDir);
            int n = SaveTextAssets(gameDir + Path.DirectorySeparatorChar + "Overload_Data" + Path.DirectorySeparatorChar + "resources.assets");
            Console.WriteLine(n + " assets written.");
        }
    }
}
