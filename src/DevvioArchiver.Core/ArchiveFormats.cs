using System;
using System.IO;

namespace Devvio.Archiver.Core
{
    /// <summary>
    /// The archive formats known to the application.
    /// </summary>
    public enum ArchiveFormatKind
    {
        Zip,
        SevenZip,
        Rar,
        Tar,
        TarGz,
        TarBz2,
        TarXz,
        Gzip,
        BZip2,
        Xz,
        Wim,
        Cab,
        Iso,
        Lzh,
        Arj,
        Cpio,
        Deb,
        Rpm,
        Jar,
        Split,
        Other
    }

    /// <summary>
    /// Format detection, extension tables and naming helpers for archives.
    /// </summary>
    public static class ArchiveFormats
    {
        /// <summary>
        /// File extensions for which Explorer will show archive operations.
        /// </summary>
        public static readonly string[] KnownExtensions =
        {
            ".7z", ".zip", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".tbz2", ".tbz", ".xz", ".txz",
            ".cab", ".iso", ".wim", ".lzh", ".arj", ".jar", ".war", ".ear", ".apk", ".deb", ".rpm",
            ".cpio", ".001"
        };

        /// <summary>
        /// Formats the application can offer for creating archives (7-Zip engine).
        /// </summary>
        public static readonly ArchiveFormatKind[] CreatableFormats =
        {
            ArchiveFormatKind.SevenZip,
            ArchiveFormatKind.Zip,
            ArchiveFormatKind.Tar,
            ArchiveFormatKind.TarGz,
            ArchiveFormatKind.TarBz2,
            ArchiveFormatKind.TarXz,
            ArchiveFormatKind.Gzip,
            ArchiveFormatKind.BZip2,
            ArchiveFormatKind.Xz,
            ArchiveFormatKind.Wim
        };

        public static bool IsArchivePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            try
            {
                string ext = Path.GetExtension(path);
                if (string.IsNullOrEmpty(ext))
                {
                    return false;
                }
                ext = ext.ToLowerInvariant();
                foreach (string known in KnownExtensions)
                {
                    if (known == ext)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// Guesses the format purely from the file name.
        /// </summary>
        public static ArchiveFormatKind KindFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return ArchiveFormatKind.Other;
            }
            string name;
            string ext;
            try
            {
                name = Path.GetFileName(path).ToLowerInvariant();
                ext = Path.GetExtension(name);
            }
            catch
            {
                return ArchiveFormatKind.Other;
            }

            switch (ext)
            {
                case ".7z":
                    return ArchiveFormatKind.SevenZip;
                case ".zip":
                case ".jar":
                case ".war":
                case ".ear":
                case ".apk":
                    return ArchiveFormatKind.Zip;
                case ".rar":
                    return ArchiveFormatKind.Rar;
                case ".tar":
                    return ArchiveFormatKind.Tar;
                case ".gz":
                    return name.EndsWith(".tar.gz", StringComparison.Ordinal) ? ArchiveFormatKind.TarGz : ArchiveFormatKind.Gzip;
                case ".tgz":
                    return ArchiveFormatKind.TarGz;
                case ".bz2":
                    return name.EndsWith(".tar.bz2", StringComparison.Ordinal) ? ArchiveFormatKind.TarBz2 : ArchiveFormatKind.BZip2;
                case ".tbz2":
                case ".tbz":
                    return ArchiveFormatKind.TarBz2;
                case ".xz":
                    return name.EndsWith(".tar.xz", StringComparison.Ordinal) ? ArchiveFormatKind.TarXz : ArchiveFormatKind.Xz;
                case ".txz":
                    return ArchiveFormatKind.TarXz;
                case ".cab":
                    return ArchiveFormatKind.Cab;
                case ".iso":
                    return ArchiveFormatKind.Iso;
                case ".wim":
                    return ArchiveFormatKind.Wim;
                case ".lzh":
                    return ArchiveFormatKind.Lzh;
                case ".arj":
                    return ArchiveFormatKind.Arj;
                case ".cpio":
                    return ArchiveFormatKind.Cpio;
                case ".deb":
                    return ArchiveFormatKind.Deb;
                case ".rpm":
                    return ArchiveFormatKind.Rpm;
                case ".001":
                    return ArchiveFormatKind.Split;
                default:
                    return ArchiveFormatKind.Other;
            }
        }

        /// <summary>
        /// Detects the format from the file header (magic bytes) with a file-name fallback.
        /// </summary>
        public static ArchiveFormatKind Detect(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    var head = new byte[0x8010];
                    int read = 0;
                    while (read < head.Length)
                    {
                        int n = fs.Read(head, read, head.Length - read);
                        if (n <= 0)
                        {
                            break;
                        }
                        read += n;
                    }

                    if (StartsWith(head, read, 0x50, 0x4B, 0x03, 0x04)) return ArchiveFormatKind.Zip;
                    if (StartsWith(head, read, 0x50, 0x4B, 0x05, 0x06)) return ArchiveFormatKind.Zip;
                    if (StartsWith(head, read, 0x50, 0x4B, 0x07, 0x08)) return ArchiveFormatKind.Zip;
                    if (StartsWith(head, read, 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C)) return ArchiveFormatKind.SevenZip;
                    if (StartsWith(head, read, 0x52, 0x61, 0x72, 0x21, 0x1A)) return ArchiveFormatKind.Rar;
                    if (StartsWith(head, read, 0x1F, 0x8B))
                    {
                        return KindFromPath(path) == ArchiveFormatKind.TarGz ? ArchiveFormatKind.TarGz : ArchiveFormatKind.Gzip;
                    }
                    if (StartsWith(head, read, 0x42, 0x5A, 0x68))
                    {
                        return KindFromPath(path) == ArchiveFormatKind.TarBz2 ? ArchiveFormatKind.TarBz2 : ArchiveFormatKind.BZip2;
                    }
                    if (StartsWith(head, read, 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00))
                    {
                        return KindFromPath(path) == ArchiveFormatKind.TarXz ? ArchiveFormatKind.TarXz : ArchiveFormatKind.Xz;
                    }
                    if (StartsWith(head, read, 0x4D, 0x53, 0x43, 0x46)) return ArchiveFormatKind.Cab; // MSCF
                    if (StartsWith(head, read, 0x4D, 0x53, 0x57, 0x49, 0x4D, 0x00, 0x00, 0x00)) return ArchiveFormatKind.Wim; // MSWIM
                    if (StartsWith(head, read, 0x60, 0xEA)) return ArchiveFormatKind.Arj;
                    if (read > 6 && head[0] == (byte)'!' && head[1] == (byte)'<' && head[2] == (byte)'a' && head[3] == (byte)'r')
                        return ArchiveFormatKind.Deb;
                    if (read > 2 && head[0] == (byte)'-' && head[1] == (byte)'l' && head[2] == (byte)'h')
                        return ArchiveFormatKind.Lzh;
                    if (read >= 6 && (MatchAt(head, read, 0, (byte)'0', (byte)'7', (byte)'0', (byte)'7', (byte)'0', (byte)'1')
                        || MatchAt(head, read, 0, (byte)'0', (byte)'7', (byte)'0', (byte)'7', (byte)'0', (byte)'2')
                        || MatchAt(head, read, 0, (byte)'0', (byte)'7', (byte)'0', (byte)'7', (byte)'0', (byte)'7')))
                        return ArchiveFormatKind.Cpio;
                    if (read >= 4 && head[0] == 0xED && head[1] == 0xAB && head[2] == 0xEE && head[3] == 0xDB)
                        return ArchiveFormatKind.Rpm;
                    if (read > 0x805 && MatchAt(head, read, 0x8001, (byte)'C', (byte)'D', (byte)'0', (byte)'0', (byte)'1'))
                        return ArchiveFormatKind.Iso;
                    if (read > 261 && MatchAt(head, read, 257, (byte)'u', (byte)'s', (byte)'t', (byte)'a', (byte)'r'))
                        return ArchiveFormatKind.Tar;
                }
            }
            catch
            {
            }
            return KindFromPath(path);
        }

        /// <summary>
        /// "photos.tar.gz" -> "photos",  "backup.7z" -> "backup".
        /// </summary>
        public static string StripArchiveExtension(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return fileName;
            }
            string lower = fileName.ToLowerInvariant();
            string[] doubleExt = { ".tar.gz", ".tar.bz2", ".tar.xz", ".tar.lzma" };
            foreach (string d in doubleExt)
            {
                if (lower.EndsWith(d, StringComparison.Ordinal))
                {
                    return fileName.Substring(0, fileName.Length - d.Length);
                }
            }
            int dot = fileName.LastIndexOf('.');
            if (dot > 0 && dot < fileName.Length - 1)
            {
                return fileName.Substring(0, dot);
            }
            return fileName;
        }

        /// <summary>
        /// Default extension (including leading dot) for a creatable format.
        /// </summary>
        public static string DefaultExtension(ArchiveFormatKind kind)
        {
            switch (kind)
            {
                case ArchiveFormatKind.SevenZip: return ".7z";
                case ArchiveFormatKind.Zip: return ".zip";
                case ArchiveFormatKind.Tar: return ".tar";
                case ArchiveFormatKind.TarGz: return ".tar.gz";
                case ArchiveFormatKind.TarBz2: return ".tar.bz2";
                case ArchiveFormatKind.TarXz: return ".tar.xz";
                case ArchiveFormatKind.Gzip: return ".gz";
                case ArchiveFormatKind.BZip2: return ".bz2";
                case ArchiveFormatKind.Xz: return ".xz";
                case ArchiveFormatKind.Wim: return ".wim";
                default: return ".zip";
            }
        }

        private static bool StartsWith(byte[] buffer, int length, params byte[] prefix)
        {
            if (length < prefix.Length)
            {
                return false;
            }
            for (int i = 0; i < prefix.Length; i++)
            {
                if (buffer[i] != prefix[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static bool MatchAt(byte[] buffer, int length, int offset, params byte[] prefix)
        {
            if (length < offset + prefix.Length)
            {
                return false;
            }
            for (int i = 0; i < prefix.Length; i++)
            {
                if (buffer[offset + i] != prefix[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
