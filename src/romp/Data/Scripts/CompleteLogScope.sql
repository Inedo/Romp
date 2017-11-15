UPDATE ScopedExecutionLogs
   SET End_Date = @End_Date
 WHERE Execution_Id = @Execution_Id AND Scope_Sequence = @Scope_Sequence;