UPDATE Executions
SET End_Date = @End_Date,
    ExecutionStatus_Code = @ExecutionStatus_Code
WHERE Execution_Id = @Execution_Id;

UPDATE ScopedExecutionLogs
   SET End_Date = @End_Date
 WHERE Execution_Id = @Execution_Id AND End_Date IS NULL;