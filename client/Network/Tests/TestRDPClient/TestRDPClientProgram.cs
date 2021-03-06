/********************************************************************

The Multiverse Platform is made available under the MIT License.

Copyright (c) 2012 The Multiverse Foundation

Permission is hereby granted, free of charge, to any person 
obtaining a copy of this software and associated documentation 
files (the "Software"), to deal in the Software without restriction, 
including without limitation the rights to use, copy, modify, 
merge, publish, distribute, sublicense, and/or sell copies 
of the Software, and to permit persons to whom the Software 
is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be 
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES 
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE 
OR OTHER DEALINGS IN THE SOFTWARE.

*********************************************************************/

#region Using directives

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Net;
using System.IO;
using System.Threading;

using Multiverse.Config;
using Multiverse.Network;
using Multiverse.Network.Rdp;
using Multiverse.Base;

#endregion

namespace TestRDPClient
{
    public class Program {

        public static void RunIncoming() {
            int receiveCount = 0;
            long lastReceiveCounterResetTime = CurrentTime;
            try {
                while (true) {
                    long currentTime = CurrentTime;
                    long interval = currentTime - lastReceiveCounterResetTime;
                    if (interval > 1000) {
                        lastReceiveCounterResetTime = currentTime;
                        Logit("Received " + receiveCount + " messages" + " in the last " + interval + "ms");
                        receiveCount = 0;
                    }                    
                    Debug.Assert(rdpConnection != null, "In TestRDPClient.RunIncoming, rdpConnection is null!");
                    IncomingMessage inMessage = new IncomingMessage(rdpConnection);
                    receiveCount++;
                    Trace.TraceInformation("TestRDPClient got message '" + inMessage.ReadString());
                }
            }
            catch(Exception e) {
                Logit("TestRDPClient.RunIncoming got error: " + e.ToString());
            }
        }

        public static void RunOutgoing() {
            SocketAddress addr = null;
            int sentCount = 0;
            try {
                int runningCounter = 0;
                long lastTime = CurrentTime;
                for (int i=0; i<messageCount; i++) {
                    long currentTime = CurrentTime;
                    long interval = currentTime - lastTime;
                    if (interval > 1000) {
                        lastTime = currentTime;
                        Logit("Sent " + sentCount + " messages" + " in the last " + interval + "ms");
                        sentCount = 0;
                    }
                    OutgoingMessage outMessage = new OutgoingMessage();
                    Debug.Assert(rdpConnection != null, "In TestRDPClient.RunOutgoing, rdpConnection is null!");
                    outMessage.Write("Hello World from CLIENT! - MSG " + runningCounter++);
                    bool trying = true;
                    while (trying) {
                        try {
                            outMessage.Send(rdpConnection);
                            trying = false;
                        }
                        catch (Exception e) {
                            if (e.Message == "Error - insufficient resources to send data")
                                Thread.Sleep(10);
                            else
                                throw;
                        }
                    }
                    sentCount++;
                }
                Console.WriteLine("RunOutgoing sent a total of " + messageCount + " messages");
            }
            catch(Exception e) {
                Logit("TestRDPClient.RunOutgoing got error: " + e.ToString());
                Logit("TestRDPClient.RunOutgoing sentCount = " + sentCount);
            }
        }

		static System.Timers.Timer traceFlushTimer;

        protected static void SetupDebug() {
            Logger.LogLevel = 2;
            // Create a file for output named TestFile.txt.
			string TraceFile = "TestRDPClient.txt";
            File.Delete(TraceFile);
			Stream myFile = File.Open(TraceFile, FileMode.Create, 
									  FileAccess.ReadWrite, FileShare.Read);

			/* Create a new text writer using the output stream, and add it to
             * the trace listeners. */
            Trace.Listeners.Add(new TimestampedTextWriterTraceListener(myFile));

			traceFlushTimer = new System.Timers.Timer();
			traceFlushTimer.Enabled = true;
			traceFlushTimer.Interval = 50; // .05 seconds
			traceFlushTimer.Elapsed +=
			   new System.Timers.ElapsedEventHandler(Flush);
			Trace.WriteLine("Starting TestRDPClient");
        }

		protected static void Flush(object sender, System.Timers.ElapsedEventArgs e) {
			Trace.Flush();
		}

		protected static void Logit(string msg) {
            Console.WriteLine(msg);
            Trace.TraceInformation(msg);
        }
        
        public static long CurrentTime {
			get {
				long timestamp = System.Environment.TickCount;
				while (timestamp < 0) {
					timestamp += int.MaxValue;
					timestamp -= int.MinValue;
				}
				return timestamp;
			}
		}

		private static void Main(string[] args) {
            SetupDebug();
            if (args.Length != 3 && args.Length != 4) {
                Logit("usage: TestRDPClient hostname remotePort localPort <run-incoming-thread>");
                Trace.Flush();
            }

            remoteHostname = args[0];
            remotePort = int.Parse(args[1]);
            localPort = int.Parse(args[2]);

            for (int i=0; i<1; i++) {
                rdpClient = new RdpClient(localPort + i, 100, 1000, true);
                IPHostEntry IPHost = Dns.GetHostEntry(remoteHostname);
                IPAddress[] addr = IPHost.AddressList;
                IPEndPoint sendpt = new IPEndPoint(addr[0], remotePort);
                Logit("Connecting to host " + remoteHostname + ", remotePort " + remotePort + ", localPort " + localPort);
                rdpConnection = rdpClient.Connect(sendpt);
                rdpConnection.WaitForState(ConnectionState.Open);
                Logit("Connection successful");
                Thread outgoingThread = new Thread(RunOutgoing);
                outgoingThread.Start();
                if (args.Length == 4) {
                    Thread incomingThread = new Thread(RunIncoming);
                    incomingThread.Start();
                }
                while (outgoingThread.IsAlive)
                    Thread.Sleep(500);
            }
            Trace.Flush();
        }

        public static RdpClient rdpClient;
        public static RdpConnection rdpConnection;
        public static string remoteHostname;
        public static int remotePort = -1;
        public static int localPort = -1;
        public static int messageCount = 10000000;
    }
    
}
