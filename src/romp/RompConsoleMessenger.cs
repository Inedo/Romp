using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Romp.RompExecutionEngine;

namespace Inedo.Romp
{
    internal sealed class RompConsoleMessenger : ConsoleMessenger
    {
        public static MessageLevel MinimumLevel { get; set; } = MessageLevel.Warning;
        public static bool ShowProgress { get; set; }

        public override void Message(IMessage message)
        {
            if (!ShowProgress && message.Level >= MinimumLevel)
                base.Message(message);
        }
        public override void Message(MessageLevel messageLevel, string message)
        {
            if (!ShowProgress && messageLevel >= MinimumLevel)
                base.Message(messageLevel, message);
        }

        public static void WriteDirect(string message, ConsoleColor? color = null)
        {
            lock (ConsoleLock)
            {
                var originalColor = Console.ForegroundColor;
                try
                {
                    if (color != null)
                        Console.ForegroundColor = color.Value;
                    Console.WriteLine(message);
                }
                finally
                {
                    if (color != null)
                        Console.ForegroundColor = originalColor;
                }
            }
        }
        public static async Task MonitorStatus(RompExecutionEnvironment executer, CancellationToken cancel)
        {
            Console.CursorVisible = false;
            try
            {
                while (true)
                {
                    cancel.ThrowIfCancellationRequested();
                    var status = executer.GetStatus();
                    if (status != null)
                        WriteCurrentStatus(OperationStatus.GetOperationStatusText(status));

                    await Task.Delay(500, cancel).ConfigureAwait(false);
                }
            }
            finally
            {
                Console.WriteLine(new string(' ', Console.BufferWidth - 1));
                Console.Write(new string(' ', Console.BufferWidth - 1));
                Console.CursorLeft = 0;
                Console.CursorTop--;
                Console.CursorVisible = true;
            }
        }

        private static void WriteCurrentStatus(OperationStatus status)
        {
            lock (ConsoleLock)
            {
                int width = Console.BufferWidth;
                int top = Console.CursorTop;
                Console.SetCursorPosition(0, top);

                var info = new StatusDisplayInfo(status);
                Console.WriteLine(info.GetTitle(width));
                Console.Write(info.GetStatus(width));
                Console.SetCursorPosition(0, top);
            }
        }

        private struct StatusDisplayInfo
        {
            private readonly OperationStatus status;

            public StatusDisplayInfo(OperationStatus status) => this.status = status;

            public string GetTitle(int maxLength)
            {
                return Format("Operation: " + this.status.ShortDescription + (" " + this.status.LongDescription).TrimEnd(), maxLength);
            }
            public string GetStatus(int maxLength)
            {
                if (this.status.StatementPercentComplete == null && string.IsNullOrWhiteSpace(this.status.StatementMessage))
                    return Format("Status: executing", maxLength);

                var buffer = new StringBuilder(maxLength);
                buffer.Append("Status: ");
                if (this.status.StatementPercentComplete != null)
                {
                    buffer.Append(this.status.StatementPercentComplete.Value);
                    buffer.Append('%');
                    if (!string.IsNullOrWhiteSpace(this.status.StatementMessage))
                        buffer.Append(" (");
                }

                if (!string.IsNullOrWhiteSpace(this.status.StatementMessage))
                {
                    buffer.Append(this.status.StatementMessage);
                    if (this.status.StatementPercentComplete != null)
                        buffer.Append(')');
                }

                return Format(buffer.ToString(), maxLength);
            }

            private static string Format(string s, int maxLength)
            {
                s = s ?? string.Empty;
                if (s.Length >= maxLength)
                    s = s.Substring(0, maxLength - 4) + "...";

                if (s.Length < maxLength - 1)
                    s += new string(' ', maxLength - 1 - s.Length);

                return s;
            }
        }
    }
}
