using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace WeixinPay.Logging
{
    /// <summary>
    ///     App logger
    /// </summary>
    public sealed class AppLogger
    {
        private const int _maxQueuedMessages = 1024;
        private readonly BlockingCollection<string> _messageQueue = new BlockingCollection<string>(_maxQueuedMessages);
        private readonly Task _task;

        public AppLogger()
        {
            _task = Task.Factory.StartNew(ProcessMessageQueue, TaskCreationOptions.LongRunning);
        }

        private void ProcessMessageQueue()
        {
            foreach(var message in _messageQueue.GetConsumingEnumerable())
            {
                WriteMessage(message);
            }
        }

        /// <summary>
        ///     Enqueue message
        /// </summary>
        /// <param name="message"></param>
        public void EnqueueMessage(string message)
        {
            if(!_messageQueue.IsAddingCompleted)
            {
                try
                {
                    _messageQueue.Add(message);
                    return;
                }
                catch(InvalidCastException) { }
            }

            WriteMessage(message);
        }

        private static void WriteMessage(string message)
        {
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "logging");
            if(!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, $"wx_log_{DateTime.Now:yyyyMMdd}.txt");

            using var writer = new StreamWriter(path, true);
            writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]-{message}");
        }
    }
}