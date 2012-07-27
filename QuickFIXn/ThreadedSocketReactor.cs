using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Collections.Generic;
using NLog;
using QuickFix.SSL;

namespace QuickFix
{
    /// <summary>
    /// Handles incoming connections on a single endpoint. When a socket connection
    /// is accepted, a ClientHandlerThread is created to handle the connection
    /// </summary>
    public class ThreadedSocketReactor
    {
        public enum State { RUNNING, SHUTDOWN_REQUESTED, SHUTDOWN_COMPLETE }

        #region Properties

        public State ReactorState
        {
            get { lock (sync_) { return state_; } }
        }

        #endregion

        #region Private Members
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private object sync_ = new object();
        private State state_ = State.RUNNING;
        private long nextClientId_ = 0;
        private Thread serverThread_ = null;
        private LinkedList<ClientHandlerThread> clientThreads_ = new LinkedList<ClientHandlerThread>();
        private TcpListener tcpListener_;
        private SocketSettings socketSettings_;
        private SSLSettings sslSettings_;
        #endregion

        public ThreadedSocketReactor(IPEndPoint serverSocketEndPoint, SocketSettings socketSettings, SSLSettings sslSettings)
        {
            socketSettings_ = socketSettings;
            sslSettings_ = sslSettings;
            tcpListener_ = new TcpListener(serverSocketEndPoint);
        }

        public void Start()
        {
            serverThread_ = new Thread(new ThreadStart(Run));
            serverThread_.Start();
        }

        public void Shutdown()
        {
            lock (sync_)
            {
                if (State.RUNNING == state_)
                {
                    try
                    {
                        state_ = State.SHUTDOWN_REQUESTED;
                        tcpListener_.Server.Close();
                        tcpListener_.Stop();
                    }
                    catch (System.Exception e)
                    {
                        this.Log("Eror while closing server socket: " + e.Message, e);
                    }
                }
            }
        }

        /// <summary>
        /// TODO apply networking options, e.g. NO DELAY, LINGER, etc.
        /// </summary>
        public void Run()
        {
            tcpListener_.Start();
            while (State.RUNNING == ReactorState)
            {
                try
                {
                    TcpClient client = tcpListener_.AcceptTcpClient();
                    ApplySocketOptions(client, socketSettings_);
                    ClientHandlerThread t = new ClientHandlerThread(client, nextClientId_++, sslSettings_);
                    lock (sync_)
                    {
                        clientThreads_.AddLast(t);
                    }
                    // FIXME set the client thread's exception handler here
                    t.Log("connected");
                    t.Start();

                    // Check existing clientThreads_.items for death ClientHandlerThreads and remove them from list
                    while (!t.IsAlive());
                    lock (sync_)
                    {
                        LinkedList<ClientHandlerThread> recycleBin = new LinkedList<ClientHandlerThread>();
                        foreach (ClientHandlerThread clientHandlerThread in clientThreads_)
                        {
                            if (clientHandlerThread == null || (clientHandlerThread != null && !clientHandlerThread.IsAlive()))
                                recycleBin.AddLast(clientHandlerThread);
                        }

                        foreach (ClientHandlerThread clientHandlerThread in recycleBin)
                        {
                            clientThreads_.Remove(clientHandlerThread);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    if (State.RUNNING == ReactorState)
                        this.Log("Error accepting connection: " + e.Message, e);
                }
            }
            ShutdownClientHandlerThreads();
        }

        /// <summary>
        /// FIXME get socket options from SessionSettings
        /// </summary>
        /// <param name="client"></param>
        public static void ApplySocketOptions(TcpClient client, SocketSettings socketSettings)
        {
            client.LingerState = new LingerOption(false, 0);
            client.NoDelay = socketSettings.SocketNodelay;
        }

        private void ShutdownClientHandlerThreads()
        {
            lock(sync_)
            {
                if(State.SHUTDOWN_COMPLETE != state_) 
                {
                    this.Log("shutting down...");
                    while(clientThreads_.Count > 0)
                    {
                        ClientHandlerThread t = clientThreads_.First.Value;
                        clientThreads_.RemoveFirst();
                        t.Shutdown("reactor is shutting down");
                        try
                        {
                            t.Join();
                        }
                        catch(Exception e)
                        {
                            t.Log("Error shutting down: " + e.Message, e);
                        }
                    }
                    state_ = State.SHUTDOWN_COMPLETE;
                }
            }
        }

        private void Log(string s)
        {
            logger.Debug(s);
        }

        private void Log(string s, Exception ex)
        {
            logger.ErrorException(s, ex);
        }
    }
}
