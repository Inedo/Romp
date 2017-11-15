INSERT INTO ScopedExecutionLogEntries (Execution_Id, LogEntry_Sequence, Scope_Sequence, LogEntry_Level, LogEntry_Text, LogEntry_Date)
SELECT @Execution_Id,
COALESCE((SELECT MAX(LogEntry_Sequence) FROM ScopedExecutionLogEntries WHERE Execution_Id = @Execution_Id), 0) + 1,
@Scope_Sequence,
@LogEntry_Level,
@LogEntry_Text,
@LogEntry_Date