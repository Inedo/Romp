using System;
using System.Diagnostics;
using Gibraltar.Agent.Metrics;
using Inedo.Romp.Configuration;

namespace Inedo.Romp.Data
{
    [EventMetric("Inedo", "Database", "Query")]
    internal sealed class DbEventMetric
    {
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();

        public DbEventMetric(string name) => this.Name = name;

        [EventMetricValue("Name", SummaryFunction.Count, null)]
        public string Name { get; }
        [EventMetricValue("StartTime", SummaryFunction.Count, null, Caption = "Start Time")]
        public DateTimeOffset StartTime { get; } = DateTimeOffset.Now;
        [EventMetricValue("EndTime", SummaryFunction.Count, null, Caption = "End Time")]
        public DateTimeOffset EndTime => this.StartTime + this.Duration;
        [EventMetricValue("Duration", SummaryFunction.Average, "ms")]
        public TimeSpan Duration => this.stopwatch.Elapsed;
        [EventMetricValue("Error", SummaryFunction.Count, null)]
        public Exception Error { get; set; }

        public void Write()
        {
            this.stopwatch.Stop();
            if (RompConfig.CeipEnabled)
                EventMetric.Write(this);
        }
    }
}
