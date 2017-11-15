using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Inedo.Diagnostics;

namespace Inedo.Romp.Data
{
    internal sealed class ScopedExecutionLog : IScopedExecutionLogNode
    {
        private List<IScopedExecutionLogNode> nodes;

        public ScopedExecutionLog(SQLiteDataReader reader)
        {
            this.Sequence = reader.GetInt32(1);
            this.ParentSequence = !reader.IsDBNull(2) ? (int?)reader.GetInt32(2) : null;
            this.Title = reader.GetString(3);
            this.StartTime = new DateTimeOffset(reader.GetInt64(4), TimeSpan.Zero).ToLocalTime();
            if (!reader.IsDBNull(5))
                this.EndTime = new DateTimeOffset(reader.GetInt64(5), TimeSpan.Zero).ToLocalTime();
        }

        public int? ParentSequence { get; }
        public string Title { get; }
        public int Sequence { get; }
        public DateTimeOffset StartTime { get; }
        public DateTimeOffset? EndTime { get; }
        public TimeSpan Duration => (this.EndTime ?? DateTimeOffset.Now) - this.StartTime;

        public override string ToString() => this.Title ?? string.Empty;

        public void WriteText(int level, TextWriter writer)
        {
            var title = new string(' ', level) + "** " + AH.CoalesceString(this.Title, "(unnamed scope)") + " **";
            if (writer == Console.Out)
                RompConsoleMessenger.WriteDirect(title, ConsoleColor.White);
            else
                writer.WriteLine(title);

            foreach (var node in this.nodes)
                node.WriteText(level + 1, writer);

            writer.WriteLine();
        }
        public void WriteErrors(TextWriter writer)
        {
            foreach (var node in this.nodes)
                node.WriteErrors(writer);
        }

        public static IList<ScopedExecutionLog> Build(IEnumerable<ScopedExecutionLog> scopes, IEnumerable<LogEntry> entries)
        {
            var topLevel = scopes
                .Where(s => s.ParentSequence == null)
                .OrderBy(s => s.Sequence)
                .ToList();

            foreach (var scope in topLevel)
                scope.BuildNode(scopes, entries);

            return topLevel;
        }

        private void BuildNode(IEnumerable<ScopedExecutionLog> allScopes, IEnumerable<LogEntry> allEntries)
        {
            var myScopes = allScopes
                .Where(s => s.ParentSequence == this.Sequence)
                .ToList();

            this.nodes = myScopes.AsEnumerable<IScopedExecutionLogNode>().Concat(allEntries.Where(e => e.ParentSequence == this.Sequence).AsEnumerable<IScopedExecutionLogNode>()).ToList();
            this.nodes.Sort();

            foreach (var scope in myScopes)
                scope.BuildNode(allScopes, allEntries);
        }

        int IComparable<IScopedExecutionLogNode>.CompareTo(IScopedExecutionLogNode other)
        {
            if (other is ScopedExecutionLog s)
                return this.Sequence.CompareTo(s.Sequence);
            else if (other is LogEntry e)
                return this.StartTime.UtcDateTime.CompareTo(e.Timestamp);
            else
                throw new ArgumentException();
        }
    }

    internal sealed class LogEntry : IScopedExecutionLogNode
    {
        public LogEntry(SQLiteDataReader reader)
        {
            this.Sequence = reader.GetInt32(1);
            this.ParentSequence = reader.GetInt32(2);
            this.Level = (MessageLevel)reader.GetInt32(3);
            this.Text = reader.GetString(4);
            this.Timestamp = new DateTime(reader.GetInt64(5), DateTimeKind.Utc);
        }

        public int ParentSequence { get; }
        public int Sequence { get; }
        public string Text { get; }
        public MessageLevel Level { get; }
        public DateTime Timestamp { get; }

        public override string ToString() => GetMessageLevelText(this.Level) + ": " + this.Text;

        public void WriteText(int level, TextWriter writer)
        {
            var text = new string(' ', level) + this.ToString();
            if (writer == Console.Out)
            {
                RompConsoleMessenger.WriteDirect(
                    text,
                    this.Level == MessageLevel.Warning ? (ConsoleColor?)ConsoleColor.Yellow :
                    this.Level == MessageLevel.Error ? (ConsoleColor?)ConsoleColor.Red :
                    null
                );
            }
            else
            {
                writer.WriteLine(text);
            }
        }
        public void WriteErrors(TextWriter writer)
        {
            if (this.Level >= MessageLevel.Warning)
            {
                if (writer == Console.Out)
                {
                    RompConsoleMessenger.WriteDirect(
                        this.ToString(),
                        this.Level == MessageLevel.Warning ? (ConsoleColor?)ConsoleColor.Yellow :
                        this.Level == MessageLevel.Error ? (ConsoleColor?)ConsoleColor.Red :
                        null
                    );
                }
                else
                {
                    writer.WriteLine(this.ToString());
                }
            }
        }

        int IComparable<IScopedExecutionLogNode>.CompareTo(IScopedExecutionLogNode other)
        {
            if (other is LogEntry e)
                return this.Sequence.CompareTo(e.Sequence);
            else if (other is ScopedExecutionLog s)
                return this.Timestamp.CompareTo(s.StartTime.UtcDateTime);
            else
                throw new ArgumentException();
        }

        private static string GetMessageLevelText(MessageLevel level)
        {
            return level == MessageLevel.Information ? " INFO"
                : level == MessageLevel.Warning ? " WARN"
                : level == MessageLevel.Debug ? "DEBUG"
                : "ERROR";
        }
    }

    internal interface IScopedExecutionLogNode : IComparable<IScopedExecutionLogNode>
    {
        void WriteText(int level, TextWriter writer);
        void WriteErrors(TextWriter writer);
    }
}
