Imports Microsoft.Data.Sqlite

Public Class WorkRecordService
    Private ReadOnly _connectionString As String
    Public Sub New(connectionString As String)
        _connectionString = connectionString
    End Sub
    Public Function GetMonthlyRecords(userId As Integer, yearMonth As String) As List(Of WorkRecordDetailDto)
    Dim yearMonthDate As String = yearMonth & "%"
    Dim result As New List(Of WorkRecordDetailDto)()
    Using connection As New SqliteConnection(_connectionString)
        connection.Open()
        Dim command = connection.CreateCommand()
        command.CommandText = "
            SELECT
                WorkDate,
                StartTime,
                EndTime,
                OvertimeHours,
                ConstructionCode,
                ConstructionName
            FROM
                WorkRecords LEFT JOIN ConstructionNumbers ON WorkRecords.ConstructionId = ConstructionNumbers.ConstructionId
            WHERE
                WorkRecords.UserId = @userId AND WorkRecords.WorkDate LIKE @yearMonthDate ORDER BY WorkRecords.WorkDate ASC
        "
        command.Parameters.AddWithValue("@userId", userId)
        command.Parameters.AddWithValue("@yearMonthDate", yearMonthDate)
        Using reader = command.ExecuteReader()
            While reader.Read()
                result.Add(New WorkRecordDetailDto With {
                    .WorkDate  = reader.GetString(0),
                    .StartTime = reader.GetString(1),
                    .EndTime = reader.GetString(2),
                    .OvertimeHours = reader.GetDouble(3),
                    .ConstructionCode = If(reader.IsDBNull(4), "", reader.GetString(4)),
                    .ConstructionName = If(reader.IsDBNull(5), "", reader.GetString(5))
                })
            End While
        End Using
    End Using
    Return result
    End Function
End Class
