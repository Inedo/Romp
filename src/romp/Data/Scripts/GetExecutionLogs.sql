SELECT *
  FROM ScopedExecutionLogs
 WHERE Execution_Id = @Execution_Id;

SELECT *
  FROM ScopedExecutionLogEntries
 WHERE Execution_Id = @Execution_Id;
