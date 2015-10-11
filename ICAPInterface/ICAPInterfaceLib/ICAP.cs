using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;


namespace ICAPInterfaceLib
{

    public class ICAP : IDisposable
    {
        private const String ICAPTERMINATOR = "\r\n\r\n";
        private const String HTTPTERMINATOR = "0\r\n\r\n";

        private String serverIP;
        private int port;

        private Socket sender;
        private String service;
        private String version;
        private int stdSendLength;
        private int stdRecieveLength;

        public ICAP(string serverIP, int port, string service = "RESPMOD", string version = "ICAP/1.0", int stdSendLength = 8024, int stdRecieveLength = 8024)
        {
            this.serverIP = serverIP;
            this.port = port;
            this.service = service;
            this.version = version;
            this.stdSendLength = stdSendLength;
            this.stdRecieveLength = stdRecieveLength;


            //Initialize connection
            IPAddress ipAddress = IPAddress.Parse(serverIP);
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

            // Create a TCP/IP  socket.
            sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sender.Connect(remoteEP);
        }

        public AVReturnMessage ScanFile(String filepath)
        {
            using (FileStream fileStream = new FileStream(filepath, FileMode.Open))
            {
                int fileSize = (int)fileStream.Length;
                //First part of header
                String resBody = "HTTP/1.1 200 OK\r\n" +
                                "Transfer-Encoding: chunked\r\n" +
                                "Content-Length: " + fileSize + "\r\n\r\n";

                String httpRequest = "GET http://" + serverIP + "/" + DateTime.Now.ToString("yyyyMMddHHmm") + "/" + Path.GetFileName(filepath) + " HTTP/1.1\r\n"
                                        + "Host: " + serverIP + "\r\n\r\n";

                byte[] requestBuffer = Encoding.ASCII.GetBytes(
                    "RESPMOD icap://" + serverIP + ":" + port.ToString() + "/" + service + " " + version + "\r\n"
                    + "Allow: 204\r\n"
                    + "Connection: close\r\n"
                    + "Host: " + serverIP + "\r\n"
                    + "Encapsulated: req-hdr=0, res-hdr=" + httpRequest.Length + " res-body=" + (resBody.Length + httpRequest.Length) + "\r\n"
                    + "\r\n"
                    + httpRequest
                    + resBody);

                sender.Send(requestBuffer);

               

                int n;
                byte[] buffer = new byte[stdSendLength];
                while ((n = fileStream.Read(buffer, 0, stdSendLength)) > 0)
                {
                    sender.Send(Encoding.ASCII.GetBytes(buffer.Length.ToString("X") + "\r\n"));
                    sender.Send(buffer);
                    sender.Send(Encoding.ASCII.GetBytes("\r\n"));

                }
                sender.Send(Encoding.ASCII.GetBytes("0\r\n\r\n"));

                //---------

                Dictionary<String, String> responseMap = new Dictionary<string, string>();
                String response = GetHeader(ICAPTERMINATOR);
                responseMap = ParseHeader(response);

                String tempString;
                int status;

                responseMap.TryGetValue("StatusCode", out tempString);
                if (tempString != null)
                {
                    status = Convert.ToInt16(tempString);

                    if (status == 204) 
                    {
                        return new AVReturnMessage() { Success = true, ICAPStatusCode = status, Message = "File Scanned Successfully" }; 
                    } //Unmodified

                    if (status == 200) //OK - The ICAP status is ok, but the encapsulated HTTP status will likely be different
                    {
                        response = GetHeader(HTTPTERMINATOR);
                        var retMsg = GetHttpResponseMessage(response);
                        if (retMsg != null)
                        {
                            return new AVReturnMessage() { Success = false, ICAPStatusCode = status, Message = retMsg }; 
                        }
                    }    
                }
                throw new ICAPException("Unrecognized or no status code in response header.");
            }

            //return true;
        }

        private String GetHttpResponseMessage(string httpResponse)
        {
            int firstIndex = httpResponse.IndexOf("contentData");
            int secondIndex = httpResponse.IndexOf("</td>", firstIndex);
            int offset = 14;
            string content = httpResponse.Substring(firstIndex+offset, secondIndex - firstIndex - offset);

            return content.Trim();
        }

        private String GetHeader(String terminator)
        {
            byte[] endofheader = System.Text.Encoding.UTF8.GetBytes(terminator);
            byte[] buffer = new byte[stdRecieveLength];

            int n;
            int offset = 0;
            //stdRecieveLength-offset is replaced by '1' to not receive the next (HTTP) header.
            while ((offset < stdRecieveLength) && ((n = sender.Receive(buffer, offset, 1, SocketFlags.None)) != 0)) // first part is to secure against DOS
            {
                offset += n;
                if (offset > endofheader.Length + 13) // 13 is the smallest possible message (ICAP/1.0 xxx\r\n) or (HTTP/1.0 xxx\r\n)
                {
                    byte[] lastBytes = new byte[endofheader.Length];
                    Array.Copy(buffer, offset - endofheader.Length, lastBytes, 0, endofheader.Length);
                    if (endofheader.SequenceEqual(lastBytes))
                    {
                        return Encoding.ASCII.GetString(buffer, 0, offset);
                    }
                }
            }
            throw new ICAPException("Error in getHeader() method -  try increasing the size of stdRecieveLength");
        }

        private Dictionary<String, String> ParseHeader(String response)
        {
            Dictionary<String, String> headers = new Dictionary<String, String>();

            /****SAMPLE:****
             * ICAP/1.0 204 Unmodified
             * Server: C-ICAP/0.1.6
             * Connection: keep-alive
             * ISTag: CI0001-000-0978-6918203
             */
            // The status code is located between the first 2 whitespaces.
            // Read status code
            int x = response.IndexOf(" ", 0);
            int y = response.IndexOf(" ", x + 1);
            String statusCode = response.Substring(x + 1, y - x - 1);
            headers.Add("StatusCode", statusCode);

            // Each line in the sample is ended with "\r\n". 
            // When (i+2==response.length()) The end of the header have been reached.
            // The +=2 is added to skip the "\r\n".
            // Read headers
            int i = response.IndexOf("\r\n", y);
            i += 2;
            while (i + 2 != response.Length && response.Substring(i).Contains(':'))
            {
                int n = response.IndexOf(":", i);
                String key = response.Substring(i, n - i);

                n += 2;
                i = response.IndexOf("\r\n", n);
                String value = response.Substring(n, i - n);

                headers.Add(key, value);
                i += 2;
            }
            return headers;
        }

        public void Dispose()
        {
            sender.Shutdown(SocketShutdown.Both);
            sender.Close();
            //sender.Dispose();
        }
    }
}
