INSERT INTO ScopedExecutionLogs (Execution_Id, Scope_Sequence, Parent_Scope_Sequence, Scope_Name, Start_Date)
SELECT @Execution_Id,
COALESCE((SELECT MAX(Scope_Sequence) FROM ScopedExecutionLogs WHERE Execution_Id = @Execution_Id), 0) + 1,
@Parent_Scope_Sequence,
@Scope_Name,
@Start_Date;

SELECT Scope_Sequence FROM ScopedExecutionLogs WHERE ROWID = last_insert_rowid();