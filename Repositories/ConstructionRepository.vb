Imports Microsoft.Data.Sqlite

Public Class ConstructionRepository
  Private ReadOnly connectionString As String

  Public Sub New(connectionString As String)
    Me.connectionString = connectionString
  End Sub

  Public Function GetAllConstructions() As List(Of ConstructionDto)
    Dim constructions As New List(Of ConstructionDto)
    Using connection As New SqliteConnection(connectionString)
      connection.Open()
      Dim command = connection.CreateCommand()
      command.CommandText = "
          SELECT
              ConstructionId,
              ConstructionCode,
              ConstructionName
          FROM
              ConstructionNumbers
          WHERE
              IsActive = 1
      "
      Using reader = command.ExecuteReader()
          While reader.Read()
              constructions.Add(New ConstructionDto With {
                  .ConstructionId = reader.GetInt32(0),
                  .ConstructionCode = reader.GetString(1),
                  .ConstructionName = reader.GetString(2)
              })
          End While
      End Using
      Return constructions
    End Using
  End Function
End Class
