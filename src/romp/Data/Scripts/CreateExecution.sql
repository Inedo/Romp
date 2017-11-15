INSERT INTO Executions (Start_Date, ExecutionStatus_Code, ExecutionRunState_Code, Simulation_Indicator)
VALUES (@Start_Date, @ExecutionStatus_Code, @ExecutionRunState_Code, @Simulation_Indicator);

SELECT last_insert_rowid();