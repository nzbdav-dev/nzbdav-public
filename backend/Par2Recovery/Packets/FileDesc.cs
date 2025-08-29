using System.Text;

namespace NzbWebDAV.Par2Recovery.Packets
{
    public class FileDesc : Par2Packet
    {
        public const string PacketType = "PAR 2.0\0FileDesc";

        public byte[] FileID { get; protected set; }
        public byte[] FileHash { get; protected set; }
        public byte[] File16kHash { get; protected set; }
        public ulong FileLength { get; protected set; }
        public string FileName { get; protected set; }

        public FileDesc(Par2PacketHeader header) : base(header)
        {
        }

        protected override void ParseBody(byte[] body)
        {
            // 16	MD5 Hash	The File ID.
            FileID = new byte[16];
            Buffer.BlockCopy(body, 0, FileID, 0, 16);

            // 16	MD5 Hash	The MD5 hash of the entire file.
            FileHash = new byte[16];
            Buffer.BlockCopy(body, 16, FileHash, 0, 16);

            // 16	MD5 Hash	The MD5-16k. That is, the MD5 hash of the first 16kB of the file.
            File16kHash = new byte[16];
            Buffer.BlockCopy(body, 32, File16kHash, 0, 16);

            // 8	8-byte uint	Length of the file.
            FileLength = BitConverter.ToUInt64(body, 48);

            // ?*4	ASCII char array	Name of the file. This array is not guaranteed to be null terminated! Subdirectories are indicated by an HTML-style '/' (a.k.a. the UNIX slash). The filename must be unique.
            var nameBuffer = new byte[body.Length - 56];
            Buffer.BlockCopy(body, 56, nameBuffer, 0, nameBuffer.Length);

            // Use UTF8 encoding if it is either ASCII or has valid byte sequences. Otherwise, go with Windows-1252.
            var encoding = IsUTF8(nameBuffer) ? Encoding.UTF8 : Encoding.GetEncoding(1252);
            FileName = encoding.GetString(nameBuffer).Normalize().TrimEnd('\0');
        }

        private static bool IsUTF8(byte[] input)
        {
            if (input == null)
                return false;
            if (input.Length == 0)
                return false;

            // TODO: Check for a BOM.

            for (int i = 0; i < input.Length; i++)
            {
                // Skip low bytes.
                if (input[i] < 0x80)
                    continue;

                // Start of 4-byte sequence
                if ((input[i] & 0xF8) == 0xF0)
                {
                    if (i + 3 >= input.Length)
                        return false; // Invalid sequence length.
                    if ((input[i + 1] & 0xC0) != 0x80)
                        return false; // Invalid sequence.
                    if ((input[i + 2] & 0xC0) != 0x80)
                        return false; // Invalid sequence.
                    if ((input[i + 3] & 0xC0) != 0x80)
                        return false; // Invalid sequence.
                    i += 3; // Valid sequence. Skip to the next character.
                    continue;
                }

                // Start of 3-byte sequence
                if ((input[i] & 0xF0) == 0xE0)
                {
                    if (i + 2 >= input.Length)
                        return false; // Invalid sequence length.
                    if ((input[i + 1] & 0xC0) != 0x80)
                        return false; // Invalid sequence.
                    if ((input[i + 2] & 0xC0) != 0x80)
                        return false; // Invalid sequence.
                    i += 2; // Valid sequence. Skip to the next character.
                    continue;
                }

                // Start of 2-byte sequence
                if ((input[i] & 0xE0) == 0xC0)
                {
                    if (i + 1 >= input.Length)
                        return false; // Invalid sequence length.
                    if ((input[i + 1] & 0xC0) != 0x80)
                        return false; // Invalid sequence.
                    i += 1; // Valid sequence. Skip to the next character.
                    continue;
                }
            }

            // Either all high bytes are valid sequences, or there are no high bytes, and US-ASCII is valid UTF-8.
            return true;
        }

        public override string ToString()
        {
            return FileName ?? "FileDesc";
        }
    }
}