using System.Runtime.InteropServices;

namespace NzbWebDAV.Par2Recovery.Packets
{

	/// <summary>
	/// Header structure for binary reading.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	public struct Par2PacketHeader
	{

		/// <summary>
		/// Magic sequence. Used to quickly identify location of packets. Value = {'P', 'A', 'R', '2', '\0', 'P', 'K', 'T'} (ASCII)
		/// </summary>
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
		public byte[] Magic;

		/// <summary>
		/// Length of the entire packet. Must be multiple of 4. (NB: Includes length of header.)
		/// </summary>
		public UInt64 PacketLength;

		/// <summary>
		/// MD5 Hash of packet. Used as a checksum for the packet.
		/// Calculation starts at first byte of Recovery Set ID and ends at last byte of body.
		/// Does not include the magic sequence, length field or this field.
		/// NB: The MD5 Hash, by its definition, includes the length as if it were appended to the packet.
		/// </summary>
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] PacketHash;

		/// <summary>
		/// Recovery Set ID. All packets that belong together have the same recovery set ID. (See "main packet" for how it is calculated.)
		/// </summary>
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] RecoverySetID;

		/// <summary>
		/// Type. Can be anything.
		/// All beginning "PAR " (ASCII) are reserved for specification-defined packets.
		/// Application-specific packets are recommended to begin with the ASCII name of the client.
		/// </summary>
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] PacketType;

	}

}