Imports Microsoft.Data.Sqlite

Public Class WorkRecordRepository
  Private ReadOnly connectionString As String

  Public Sub New(connectionString As String)
    Me.connectionString = connectionString
  End Sub

  Public Function SaveWorkRecord(req As WorkRecordRequest) As Boolean
    Using connection As New SqliteConnection(connectionString)
      connection.Open()
      Dim command = connection.CreateCommand()
      command.CommandText = "
          INSERT INTO WorkRecords (
              UserId,
              WorkDate,
              StartTime,
              EndTime,
              OvertimeHours,
              ConstructionId,
              Remarks
          )
          VALUES (
              @userId,
              @workDate,
              @startTime,
              @endTime,
              @overtimeHours,
              @constructionId,
              @remarks
          )
      "
      command.Parameters.AddWithValue("@userId", req.UserId)
      command.Parameters.AddWithValue("@workDate", req.WorkDate)
      command.Parameters.AddWithValue("@startTime", req.StartTime)
      command.Parameters.AddWithValue("@endTime", req.EndTime)
      command.Parameters.AddWithValue("@overtimeHours", req.OvertimeHours)
      command.Parameters.AddWithValue("@constructionId", req.ConstructionId)
      command.Parameters.AddWithValue("@remarks", req.Remarks)
      Dim rows = command.ExecuteNonQuery()
      Return rows > 0
    End Using
  End Function

  Public Function GetWorkRecordsByMonth(userId As Integer, yearMonth As String) As List(Of ConstructionDto)
    Dim totalWorkDays As Integer = 0
    Dim totalOvertimeHours As Double = 0.0
    Dim yearMonthPattern As String = yearMonth & "%"
    Dim constructionHours As New Dictionary(Of Integer, Double)
    Dim breakdownList As New List(Of ConstructionDto)
    Dim constructionOvertimeHours As New Dictionary(Of Integer, Double)
    Using connection As New SqliteConnection(connectionString)
      connection.Open()
      Dim command = connection.CreateCommand()
      command.CommandText = "
          SELECT
              WorkDate,
              OvertimeHours,
              ConstructionId,
              StartTime,
              EndTime
          FROM
              WorkRecords
          WHERE
              UserId = @userId
          AND
              WorkDate LIKE @yearMonthPattern
      "
      command.Parameters.AddWithValue("@userId", userId)
      command.Parameters.AddWithValue("@yearMonthPattern", yearMonthPattern)
      Using reader = command.ExecuteReader()
          While reader.Read()
              totalWorkDays += 1
              totalOvertimeHours += reader.GetDouble(1)
              Dim overtime As Double = reader.GetDouble(1)
              Dim startTime = reader.GetString(3)
              Dim endTime = reader.GetString(4)
              If Not reader.IsDBNull(2) Then
                  Dim constructionId As Integer = reader.GetInt32(2)
                  Dim workTime As Double = (TimeSpan.Parse(endTime) - TimeSpan.Parse(startTime)).TotalHours - 1.0 + overtime
                  If constructionHours.ContainsKey(constructionId) Then
                      constructionHours(constructionId) += workTime
                  Else
                      constructionHours(constructionId) = workTime
                  End If
                  If constructionOvertimeHours.ContainsKey(constructionId) Then
                      constructionOvertimeHours(constructionId) += overtime
                  Else
                      constructionOvertimeHours(constructionId) = overtime
                  End If
              End If
          End While
      End Using

      command.Parameters.Clear()
      command.CommandText = "
          SELECT ConstructionId, ConstructionCode, ConstructionName
          FROM ConstructionNumbers
          WHERE IsActive = 1
      "
      Using reader = command.ExecuteReader()
          While reader.Read()
              Dim cId As Integer = reader.GetInt32(0)
              Dim cCode As String = reader.GetString(1)
              Dim cName As String = reader.GetString(2)
              Dim hours As Double = 0.0
              Dim overtimeHours As Double = 0.0
              If constructionHours.ContainsKey(cId) Then
                  hours = constructionHours(cId)
              End If
              If constructionOvertimeHours.ContainsKey(cId) Then
                  overtimeHours = constructionOvertimeHours(cId)
              End If
              breakdownList.Add(New ConstructionDto With {
                  .ConstructionId = cId,
                  .ConstructionCode = cCode,
                  .ConstructionName = cName,
                  .TotalHours = hours,
                  .OvertimeHours = overtimeHours
              })
          End While
      End Using
      Return breakdownList
    End Using
  End Function
End Class
