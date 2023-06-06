using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistributedDLL
{
    public partial class DistributedAPI
    {
        /// <summary>
        /// Sends a packet over the serial port
        /// </summary>
        /// <param name="packet">Packet to send</param>
        /// <exception cref="InvalidOperationException">The specified port is not open.</exception>
        /// <exception cref="TimeoutException">The operation did not complete before the time-out period ended.</exception>
        private Packet? SendPacket(Packet packet)
        {
            Request? req = null;

            if (packet.PacketType == PacketType.TRANSACTION)
            {
                if (packet.Payload[0] != (byte)OpCode.CONNECT)
                {
                    req = new(packet.PacketID);
                    this._pendingRequests.Enqueue(req);
                }
            }

            lock (this._serialWriteLock)
            {
                this._serialPort.Write(packet.bytes, 0, packet.bytes.Length);
            }

            if (req != null)
            {
                if (req .RequestWaitHandle.WaitOne(1000))
                {
                    return req.response;
                }
                else
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Efficiently seeks a header sync from the serial port. 
        /// </summary>
        private void SeekHeaderSync()
        {
            int bytesRead = 0;
            byte[] recvBuffer = new byte[6];
            bool isValidHeader;

            // Keep reading until we get a good header sync.     
            while (true)
            {

                while (bytesRead < 3)
                {
                    bytesRead += this._serialPort.Read(recvBuffer, bytesRead, 3 - bytesRead);
                }

                isValidHeader = (recvBuffer[0] == 0xAA) && (recvBuffer[1] == 0x01) && (recvBuffer[2] == 0x02);

                if (isValidHeader)
                {
                    break;
                }

                // Shift everything down one index and read in the next byte.
                // e.g. 0x0D 0xAA 0x01 0x02 fails on the first try so we try matching the next set of 3 bytes.
                for (int i = 0; i < bytesRead - 1; i++)
                {
                    recvBuffer[i] = recvBuffer[i + 1];
                }

                if (bytesRead > 0)
                {
                    bytesRead--;
                }
                
            }
        }

        /// <summary>
        /// Recieve a packet over the serial port as a byte array.
        /// </summary>
        /// <returns>Packet recieved as a byte array.</returns>
        /// <exception cref="InvalidOperationException">The specified port is not open.</exception>
        /// <exception cref="TimeoutException">No bytes were available to read before the timeout period expired.</exception>
        private byte[]? RecvRawPacket()
        {
            // Step 1: Get a header
            int bytesRead = 3;
            byte[] headerBuffer = new byte[3];
            this.SeekHeaderSync();

            while (bytesRead < 6)
            {
                bytesRead += this._serialPort.Read(headerBuffer, bytesRead - 3, 6 - bytesRead);
            }

            int payloadLength = headerBuffer[2];

            // Don't bother if the requested length is more than 256
            if (payloadLength > 256)
            {
                return null;
            }

            byte[] recvBuffer = new byte[payloadLength + 7];
            recvBuffer[0] = 0xAA;
            recvBuffer[1] = 0x01;
            recvBuffer[2] = 0x02;
            headerBuffer.CopyTo(recvBuffer, 3);             

            while (bytesRead < payloadLength + 7)
            {
                bytesRead += this._serialPort.Read(recvBuffer, bytesRead, payloadLength + 7 - bytesRead);
            }

            return recvBuffer;
        }

    }
}
