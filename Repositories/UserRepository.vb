Imports Microsoft.Data.Sqlite

Public Class UserRepository
    Private ReadOnly connectionString As String

    Public Sub New(connectionString As String)
        Me.connectionString = connectionString
    End Sub

    Public Function GetPasswordHashByUsername(UserName As String) As String
        Using connection As New SqliteConnection(connectionString)
            connection.Open()
            Dim command = connection.CreateCommand()
            command.CommandText = "SELECT PasswordHash FROM Users WHERE UserName = @username"
            command.Parameters.AddWithValue("@username", UserName)
            Dim storedPasswordHash = command.ExecuteScalar()?.ToString()
            Return storedPasswordHash
        End Using
    End Function
End Class
