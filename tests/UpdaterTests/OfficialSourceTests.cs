using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using PCLRestore.Runtime;

internal static class OfficialSourceTests
{
    private const string Sha = "93ede5d9e638c99fcec853a9ba1eecd91ceee7f5";
    private const string ExeName = "Plain Craft Launcher 2.exe";

    internal static void Run(Action<string, Action> test)
    {
        test("official metadata pins archive to first commit", () => {
            var release = OfficialSource.ParseCommitList("[{\"sha\":\"" + Sha + "\",\"commit\":{\"message\":\"Release title\\nMore details\"},\"html_url\":\"https://evil.example/a\"},{\"sha\":\"" + new string('a', 40) + "\"}]");
            Assert(release.Commit == Sha, "wrong commit");
            Assert(release.ArchiveUrl == "https://raw.githubusercontent.com/Meloong-Git/PCL/93ede5d9e638c99fcec853a9ba1eecd91ceee7f5/%E6%9C%80%E6%96%B0%E6%AD%A3%E5%BC%8F%E7%89%88.zip", "archive was not pinned to official commit");
            Assert(release.Message == "Release title", "message must be first line");
        });
        test("official metadata permits absent message and uppercase hex", () => {
            var release = OfficialSource.ParseCommitList("[{\"sha\":\"" + Sha.ToUpperInvariant() + "\"}]");
            Assert(release.Commit == Sha && release.Message == "", "optional message or hash normalization failed");
        });
        test("official metadata removes message controls", () => {
            var release = OfficialSource.ParseCommitList("[{\"sha\":\"" + Sha + "\",\"commit\":{\"message\":\"Title\\u0000\\u001b end\\rignored\"}}]");
            Assert(release.Message == "Title end", "unsafe message displayed");
        });
        test("official metadata rejects malformed hashes", () => {
            foreach (var sha in new[] { "", new string('a', 39), new string('a', 41), new string('g', 40), new string('a', 39) + "\n", "../../" + new string('a', 34) })
                Reject(() => OfficialSource.ParseCommitList("[{\"sha\":\"" + sha.Replace("\n", "\\n") + "\"}]"));
        });
        test("official metadata rejects wrong JSON shape", () => {
            foreach (var json in new[] { null, "", "[", "null", "{}", "[]", "[null]", "[{}]", "[{\"sha\":4}]", "[{\"sha\":null}]", "[{}, {\"sha\":\"" + Sha + "\"}]" })
                Reject(() => OfficialSource.ParseCommitList(json));
        });
        test("official extraction reads only root executable", () => InDirectory(dir => {
            string zip = Path.Combine(dir, "official.zip"), output = Path.Combine(dir, "candidate.bin");
            MakeZip(zip, archive => {
                WriteEntry(archive, "nested/" + ExeName, "wrong");
                WriteEntry(archive, "../escaped.txt", "escape");
                WriteEntry(archive, "folder/other.txt", "other");
                WriteEntry(archive, ExeName.ToUpperInvariant(), "official executable");
            });
            OfficialSource.ExtractOriginal(zip, output);
            Assert(File.ReadAllText(output) == "official executable", "wrong executable extracted");
            Assert(Directory.GetFiles(dir).Length == 2 && Directory.GetDirectories(dir).Length == 0, "other ZIP contents were extracted");
        }));
        test("official extraction replaces existing bin only after success", () => InDirectory(dir => {
            string zip = Path.Combine(dir, "official.zip"), output = Path.Combine(dir, "candidate.bin");
            File.WriteAllText(output, "keep old candidate");
            MakeZip(zip, archive => WriteEntry(archive, ExeName, "new candidate"));
            OfficialSource.ExtractOriginal(zip, output);
            Assert(File.ReadAllText(output) == "new candidate", "complete output not promoted");
            Assert(Directory.GetFiles(dir).Length == 2, "temporary output leaked");
        }));
        test("official extraction rejects missing root executable", () => RejectArchive(archive => WriteEntry(archive, "nested/" + ExeName, "nested")));
        test("official extraction rejects duplicate root executable", () => RejectArchive(archive => {
            WriteEntry(archive, ExeName, "first");
            WriteEntry(archive, ExeName.ToUpperInvariant(), "second");
        }));
        test("official extraction rejects oversized declared entry", () => RejectArchive(archive => WriteEntry(archive, ExeName, "small"), zip => ChangeDeclaredLength(zip, 128U * 1024 * 1024 + 1)));
        test("official extraction rejects oversized actual stream", () => RejectArchive(archive => {
            using (var stream = archive.CreateEntry(ExeName, CompressionLevel.Optimal).Open()) {
                var zeros = new byte[65536];
                for (int i = 0; i < 2048; i++) stream.Write(zeros, 0, zeros.Length);
                stream.WriteByte(0);
            }
        }, zip => ChangeDeclaredLength(zip, 1)));
        test("official extraction rejects truncated actual stream", () => RejectArchive(archive => WriteEntry(archive, ExeName, "small"), zip => ChangeDeclaredLength(zip, 100)));
        test("official extraction rejects malformed zip preserving output", () => InDirectory(dir => {
            string zip = Path.Combine(dir, "official.zip"), output = Path.Combine(dir, "candidate.bin");
            File.WriteAllText(zip, "not a ZIP"); File.WriteAllText(output, "keep");
            Reject(() => OfficialSource.ExtractOriginal(zip, output));
            Assert(File.ReadAllText(output) == "keep" && Directory.GetFiles(dir).Length == 2, "failed extraction changed output or leaked temp");
        }));
        test("official extraction refuses same input output", () => InDirectory(dir => {
            string zip = Path.Combine(dir, "official.zip");
            MakeZip(zip, archive => WriteEntry(archive, ExeName, "content"));
            byte[] original = File.ReadAllBytes(zip);
            Reject(() => OfficialSource.ExtractOriginal(zip, Path.Combine(dir, ".", "official.zip")));
            Assert(Convert.ToBase64String(File.ReadAllBytes(zip)) == Convert.ToBase64String(original), "input ZIP overwritten");
        }));
    }

    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new Exception("invalid official input accepted");
    }
    private static void InDirectory(Action<string> action)
    {
        string directory = Path.GetFullPath(Path.Combine("tests/UpdaterTests/fixtures", "official-source-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        try { action(directory); } finally { Directory.Delete(directory, true); }
    }
    private static void MakeZip(string path, Action<ZipArchive> write)
    {
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create)) write(archive);
    }
    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        using (var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false))) writer.Write(content);
    }
    private static void RejectArchive(Action<ZipArchive> write, Action<string> corrupt = null)
    {
        InDirectory(dir => {
            string zip = Path.Combine(dir, "official.zip"), output = Path.Combine(dir, "candidate.bin");
            MakeZip(zip, write);
            if (corrupt != null) corrupt(zip);
            File.WriteAllText(output, "keep");
            Reject(() => OfficialSource.ExtractOriginal(zip, output));
            Assert(File.ReadAllText(output) == "keep", "failed extraction modified existing output");
            Assert(Directory.GetFiles(dir).Length == 2, "failed extraction leaked temp files");
        });
    }
    private static void ChangeDeclaredLength(string path, uint size)
    {
        byte[] zip = File.ReadAllBytes(path);
        for (int i = zip.Length - 46; i >= 0; i--) {
            if (zip[i] != 0x50 || zip[i + 1] != 0x4b || zip[i + 2] != 0x01 || zip[i + 3] != 0x02) continue;
            Buffer.BlockCopy(BitConverter.GetBytes(size), 0, zip, i + 24, 4);
            File.WriteAllBytes(path, zip);
            return;
        }
        throw new Exception("test ZIP has no central entry");
    }
}
