using System.Collections;
using System.Collections.Concurrent;
using System.Data;

namespace DistributedDLL
{
    /// <summary>
    /// Represents status of the connection after a call to <see cref="DistributedAPI.Connect(string)"/>.
    /// </summary>
    public enum ConnectStatus
    {
        CONN_FAILURE,
        CONN_SUCCESS,
        ERR_AREADY_CONNECTED
    }

    /// <summary>
    /// Represents the operation requested from the distributed system, encoded as a byte.
    /// </summary>
    internal enum OpCode : byte
    {
        CONNECT = 1,
        WATCHDOG_RESET = 2,
        STREAM_BEGIN = 3,
        STREAM_END = 4
    }

    /// <summary>
    /// Represents an error in the case of an invalid or corrupt request made to the distributed system, encoded as a byte.
    /// </summary>
    internal enum ErrCode : byte
    {
        BAD_CHECKSUM = 0,
        PAYLOAD_LENGTH_EXCEEDS_MAX = 1,
        ERR_BAD_PACKET_TYPE = 2,
        ERR_BAD_OPCODE = 3,
        ERR_ALREADY_CONNECTED = 4,
        ERR_ALREADY_STREAMING = 5,
        ERR_ALREADY_STOP_STREAMING = 6,
        ERR_NOT_CONNECTED = 7
    }

    /// <summary>
    /// Represents the type of packet, encoded as a byte. Different packet types are handled differently upon reciept by the API.
    /// </summary>
    internal enum PacketType : byte
    {
        /// <summary>
        /// Represents the type of packet whose contents indicate an invalid or corrupt packet.
        /// </summary>
        /// <seealso cref="ErrCode"/>.
        FAILURE = 0,

        /// <summary>
        /// Represents a packet that is a request or response to a transactional operation.
        /// </summary>
        /// <seealso cref="OpCode"/>.
        TRANSACTION = 1,

        /// <summary>
        /// Respresents a packet that of a data stream from the distributed system.
        /// </summary>
        /// <seealso cref="DistributedAPI.StartStreaming"/>
        /// <seealso cref="DistributedAPI.StopStreaming"/>
        STREAM = 2
    }

    /// <summary>
    /// Class which represents a packet sent/recieved over the serial port.
    /// </summary>
    internal class Packet
    {
        /// <summary>
        /// The type of packet. See <see cref="DistributedDLL.PacketType"/> for more information.
        /// </summary>
        public PacketType PacketType { get => (PacketType)this.bytes[3]; }

        /// <summary>
        /// The packet ID. This number increments as packets are sent by the API. Used internally by the API to keep track of <see cref="PacketType.TRANSACTION"/> responses or as a logging tool.
        /// </summary>
        public byte PacketID { get => this.bytes[4]; }


        /// <summary>
        /// The size of the payload in bytes. This size cannot exceed 249. See also <seealso cref="ErrCode.PAYLOAD_LENGTH_EXCEEDS_MAX"/>.
        /// </summary>
        public byte PayloadSize { get => this.bytes[5]; }

        /// <summary>
        /// The payload of this packet. Depending on the <see cref="PacketType"/> this will be varying types of information.
        /// </summary>
        /// <seealso cref="PacketType"/>
        public ReadOnlySpan<byte> Payload { get => new(this.bytes, 6, this.bytes[5]); }

        /// <summary>
        /// The sum of all bytes minus the checksum byte. Used by both the API and distributed system to confirm packets sent between them are not corrupt.
        /// </summary>
        /// <seealso cref="ErrCode.BAD_CHECKSUM"/>
        public byte Checksum { get => this.bytes[this.bytes.GetUpperBound(0)]; }

        public bool IsValidChecksum {
            get
            {
                byte sum = 0;

                for (int i = 0; i <  this.bytes.Length - 1; i++)
                {
                    sum += this.bytes[i];
                }

                return (sum == this.Checksum);
            } 
        }

        /// <summary>
        /// The packet as a byte array. Modifying the contents may require that you update the checksum byte.
        /// </summary>
        /// <seealso cref="ErrCode.BAD_CHECKSUM"/>
        public byte[] bytes;

        /// <summary>
        /// Constructs a Packet.
        /// </summary>
        /// <param name="packetType">The type of packet.</param>
        /// <param name="packetID">The packet ID.</param>
        /// <param name="payload">The payload.</param>
        public Packet(PacketType packetType, byte packetID, byte[] payload)
        {
            this.bytes = new byte[7 + payload.Length];

            this.bytes[0] = 0xAA;
            this.bytes[1] = 0x01;
            this.bytes[2] = 0x02;
            this.bytes[3] = (byte)packetType;
            this.bytes[4] = packetID;
            this.bytes[5] = Convert.ToByte(payload.Length);
            payload.CopyTo(this.bytes, 6);
            this.bytes[this.bytes.GetUpperBound(0)] = Convert.ToByte(
                0xAD +  // 0xAD = 0xAA + 0x01 + 0x02
                packetType +
                packetID +
                this.PayloadSize +
                payload.Aggregate((a, b) => Convert.ToByte(a + b))
            );
        }

        public Packet(byte[] rawPacket)
        {
            this.bytes = rawPacket;
        }

        /// <summary>
        /// Sets <see cref="Packet.PacketID"/> and updates <see cref="Packet.Checksum"/> correspondingly.
        /// </summary>
        public void UpdatePacketID(byte id)
        {
            this.bytes[this.bytes.GetUpperBound(0)] -= this.bytes[4];

            this.bytes[4] = id;
            this.bytes[this.bytes.GetUpperBound(0)] += id;
        }
    }


    /// <summary>
    /// Class which represents a transactional request
    /// </summary>
    internal class Request
    {
        /// <summary>
        /// The packet ID of the transactional request
        /// </summary>
        public byte PacketID { get; private set; }

        public Packet? response;

        /// <summary>
        /// An event which fires when the transaction request is responded to
        /// </summary>
        public EventWaitHandle RequestWaitHandle { get; private set; }

        /// <summary>
        /// Constructs a new <see cref="Request"/> object
        /// </summary>
        /// <param name="packetID">The packet ID of the transactional request.</param>
        public Request(byte packetID)
        {
            this.PacketID = packetID;
            RequestWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        }
    }
}
