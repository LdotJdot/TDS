using System.Text;
using TDSNET.Engine.Actions.USN;
using K4os.Compression.LZ4.Streams;


namespace EngineCore.Engine.Actions.USN
{

    public class DiskDataCache
    {
        string path;
        const ulong ENDTAG = ulong.MinValue;
        const string FINALENDTAG = "#FINALEND";
        public DiskDataCache(string path)
        {
            this.path = path;
        }


        public void Discard()
        {
            if (File.Exists(path)) { File.Delete(path); }
        }

        public void DumpToDisk(List<FileSys> fileSys)
        {
            Discard();
            
            using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete);
            using var lz4s = LZ4Stream.Encode(fileStream, K4os.Compression.LZ4.LZ4Level.L00_FAST);
            //   using GZipStream gz = new GZipStream(fs, CompressionLevel.Fastest);

            using var writer = new BinaryWriter(lz4s, Encoding.UTF8);
            foreach (FileSys file in fileSys)
            {
                DumpToDisk(file, writer);
            }
            writer.Write(FINALENDTAG);
            writer.Flush();
            writer.Close();
            lz4s.Close();
            fileStream.Close();
        }


        private void DumpToDisk(FileSys fileSys, BinaryWriter writer)
        {
            writer.Write(fileSys.driveInfoData.Name);
            writer.Write(fileSys.driveInfoData.DriveFormat);
            writer.Write(fileSys.usnStates.UsnJournalID);
            writer.Write(fileSys.usnStates.FirstUsn);
            writer.Write(fileSys.usnStates.NextUsn);
            writer.Write(fileSys.usnStates.LowestValidUsn);
            writer.Write(fileSys.usnStates.MaxUsn);
            writer.Write(fileSys.usnStates.MaximumSize);
            writer.Write(fileSys.usnStates.AllocationDelta);

            foreach (var file in fileSys.files.Values)
            {
                writer.Write(file.fileReferenceNumber);
                writer.Write(file.parentFileReferenceNumber);
                writer.Write(file.fileName);
                writer.Write(file.keyindex);
            }
            writer.Write(ENDTAG);
        }


        public List<FileSys>? TryLoadFromDisk()
        {
            if (!File.Exists(path)) return null;
            else return LoadFromDiskSync();
        }

        private List<FileSys>? LoadFromDiskSync()
        {            
            try
            {

                using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                using var lz4s = LZ4Stream.Decode(fileStream);

                // using GZipStream gz = new GZipStream(files, CompressionMode.Decompress, false);

                using var reader = new BinaryReader(lz4s, Encoding.UTF8);

                var fileSys = new List<FileSys>();
start:;
                var fs = new FileSys(new DriveInfoData());

                var firstLine = reader.ReadString();
                if (firstLine == FINALENDTAG)
                {
                    reader.Close();
                    return fileSys;
                }
                fs.driveInfoData.Name = firstLine;
                fs.driveInfoData.DriveFormat = reader.ReadString();

                fs.usnStates.UsnJournalID = reader.ReadUInt64();
                fs.usnStates.FirstUsn = reader.ReadInt64();
                fs.usnStates.NextUsn = reader.ReadInt64();
                fs.usnStates.LowestValidUsn = reader.ReadInt64();
                fs.usnStates.MaxUsn = reader.ReadInt64();
                fs.usnStates.MaximumSize = reader.ReadUInt64();
                fs.usnStates.AllocationDelta = reader.ReadUInt64();

                while (true)
                {
                    var nextId = reader.ReadUInt64();
                    if (nextId == ENDTAG)
                    {
                        fileSys.Add(fs);

                        goto start;
                    }
                    else
                    {
                        var refNum = nextId;
                        var parentRefNum = reader.ReadUInt64();
                        var name = reader.ReadString();
                        var keyIndex = reader.ReadUInt64();
                        var newF = FrnFileOrigin.Create(name, refNum, parentRefNum);
                        newF.keyindex = keyIndex;
                        fs.files.Add(refNum, newF);
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }





}
